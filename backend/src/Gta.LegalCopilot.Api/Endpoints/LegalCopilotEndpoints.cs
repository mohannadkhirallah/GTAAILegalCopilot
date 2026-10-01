using System.Text.Json;
using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Application.Common;
using Gta.LegalCopilot.Application.Services;
using Gta.LegalCopilot.Domain.Models;
using Gta.LegalCopilot.Infrastructure;
using Gta.LegalCopilot.Infrastructure.Documents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Gta.LegalCopilot.Api.Endpoints;

public static class LegalCopilotEndpoints
{
    private static readonly HashSet<string> UploadExtensions = [".pdf", ".docx", ".json"];
    private const int MaxFilesPerUpload = 5;

    public static IEndpointRouteBuilder MapLegalCopilotEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", (IDossierExtractor extractor, ILegalNarrativeGenerator narrative) =>
            Results.Ok(new { status = "ok", aiExtractionEnabled = extractor.IsEnabled, aiNarrativeEnabled = narrative.IsEnabled }));

        api.MapGet("/demo-cases", (IDemoCaseCatalog catalog) => Results.Ok(catalog.List()));

        api.MapPost("/demo-cases/{key}/load", async (string key, IDemoCaseCatalog catalog, IDossierRepository repo, CancellationToken ct) =>
        {
            var dossier = catalog.Get(key);
            if (dossier is null) return Results.NotFound();
            await repo.SaveAsync(new StoredDossier(dossier, $"demo:{key}", DateTimeOffset.UtcNow, []), ct);
            return Results.Ok(dossier);
        });

        var dossiers = api.MapGroup("/dossiers");

        dossiers.MapGet("/", async (IDossierRepository repo, CancellationToken ct) => Results.Ok(await repo.ListAsync(ct)));

        dossiers.MapGet("/{id}", async (string id, IDossierRepository repo, CancellationToken ct) =>
            await repo.GetAsync(id, ct) is { } s ? Results.Ok(s.Dossier) : Results.NotFound());

        dossiers.MapPost("/", async ([FromBody] DisputeDossier dossier, IDossierRepository repo, CancellationToken ct) =>
        {
            var normalized = DossierValidator.Normalize(dossier, Identifiers.NewDossierId());
            DossierValidator.EnsureValid(normalized);
            await repo.SaveAsync(new StoredDossier(normalized, "json", DateTimeOffset.UtcNow, []), ct);
            return Results.Created($"/api/dossiers/{normalized.DossierId}", normalized);
        });

        dossiers.MapPost("/upload", UploadAsync).DisableAntiforgery();

        dossiers.MapPost("/{id}/analysis", async (string id, IDossierRepository repo, CaseAnalysisService analysis, CancellationToken ct) =>
            await repo.GetAsync(id, ct) is { } s ? Results.Ok(analysis.Analyze(s.Dossier)) : Results.NotFound());

        dossiers.MapPost("/{id}/memo", async (string id, bool? useAi, IDossierRepository repo, MemoOrchestrator memo, CancellationToken ct) =>
            await repo.GetAsync(id, ct) is { } s ? Results.Ok(await memo.BuildAsync(s.Dossier, useAi ?? true, ct)) : Results.NotFound());

        dossiers.MapGet("/{id}/memo", async (string id, IDossierRepository repo, CancellationToken ct) =>
            await repo.GetMemoAsync(id, ct) is { } m ? Results.Ok(m) : Results.NotFound());

        dossiers.MapGet("/{id}/memo/stream", StreamMemoAsync);

        dossiers.MapGet("/{id}/memo/docx", async (string id, IDossierRepository repo, MemoOrchestrator orchestrator,
            IMemoDocumentRenderer renderer, IFileStore files, CancellationToken ct) =>
        {
            var stored = await repo.GetAsync(id, ct);
            if (stored is null) return Results.NotFound();
            var memo = await repo.GetMemoAsync(id, ct) ?? await orchestrator.BuildAsync(stored.Dossier, useAi: false, ct);
            var bytes = renderer.Render(memo);
            var fileName = $"memo-{id}-{DateTime.UtcNow:yyyyMMddHHmmss}.docx";
            await files.SaveExportAsync(id, fileName, bytes, ct);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        });

        return app;
    }

    private static async Task<IResult> UploadAsync(HttpRequest request, IFileStore files, IDocumentTextExtractor extractor,
        IDossierExtractor aiExtractor, IDossierRepository repo, IOptions<StorageOptions> storage, CancellationToken ct)
    {
        if (!request.HasFormContentType) return Results.BadRequest(new { error = "multipart/form-data expected." });
        var form = await request.ReadFormAsync(ct);
        if (form.Files.Count is 0 or > MaxFilesPerUpload)
            return Results.BadRequest(new { error = $"Upload between 1 and {MaxFilesPerUpload} files." });

        // Pass 1: validate and buffer every file before anything touches disk.
        var buffered = new List<(string Ext, byte[] Bytes)>();
        foreach (var file in form.Files)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!UploadExtensions.Contains(ext)) return Results.BadRequest(new { error = $"Unsupported file type '{ext}'. Allowed: PDF, DOCX, JSON." });
            if (file.Length == 0 || file.Length > storage.Value.MaxUploadBytes) return Results.BadRequest(new { error = "File is empty or exceeds the size limit." });

            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();
            if (!DocumentTextExtractor.HasValidSignature(bytes.AsSpan(0, Math.Min(8, bytes.Length)), ext))
                return Results.BadRequest(new { error = $"File content does not match its '{ext}' extension." });
            buffered.Add((ext, bytes));
        }
        if (buffered.Count(b => b.Ext == ".json") > 1) return Results.BadRequest(new { error = "Only one JSON dossier may be uploaded at a time." });

        // Pass 2: build the dossier — a JSON dossier wins; PDFs/DOCX are then archived as attachments only.
        DisputeDossier? jsonDossier = null;
        var texts = new List<string>();
        if (buffered.FirstOrDefault(b => b.Ext == ".json") is { Bytes: not null } json)
        {
            try { jsonDossier = JsonSerializer.Deserialize<DisputeDossier>(json.Bytes, JsonDefaults.Options); }
            catch (JsonException ex) { throw new DossierValidationException([$"Invalid JSON dossier: {ex.Message}"]); }
        }
        else
        {
            if (!aiExtractor.IsEnabled)
                throw new AiNotConfiguredException("Azure OpenAI is not configured; PDF/DOCX petitions cannot be parsed. Upload a JSON dossier or use a demo case.");
            foreach (var (ext, bytes) in buffered)
            {
                try { texts.Add(await extractor.ExtractTextAsync(new MemoryStream(bytes), ext, ct)); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new DossierValidationException([$"Could not read the uploaded {ext} document."]);
                }
            }
        }

        var dossier = jsonDossier ?? await aiExtractor.ExtractAsync(string.Join("\n\n", texts), ct);
        var dossierId = Identifiers.NewDossierId();
        var normalized = DossierValidator.Normalize(dossier, dossierId);
        DossierValidator.EnsureValid(normalized);

        var savedNames = new List<string>();
        foreach (var (ext, bytes) in buffered)
            savedNames.Add(await files.SaveUploadAsync(dossierId, ext, new MemoryStream(bytes), ct));
        await repo.SaveAsync(new StoredDossier(normalized, jsonDossier is not null ? "upload:json" : "upload:ai-extraction", DateTimeOffset.UtcNow, savedNames), ct);
        return Results.Created($"/api/dossiers/{dossierId}", normalized);
    }

    private static async Task StreamMemoAsync(string id, bool? useAi, HttpContext ctx, IDossierRepository repo, MemoOrchestrator orchestrator, ILoggerFactory loggers)
    {
        var ct = ctx.RequestAborted;
        var stored = await repo.GetAsync(id, ct);
        if (stored is null) { ctx.Response.StatusCode = StatusCodes.Status404NotFound; return; }

        ctx.Response.Headers.ContentType = "text/event-stream; charset=utf-8";
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var e in orchestrator.StreamAsync(stored.Dossier, useAi ?? true, ct))
                await WriteSseAsync(ctx.Response, e.Type, e, ct);
            await WriteSseAsync(ctx.Response, "done", new { }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            loggers.CreateLogger("MemoStream").LogError(ex, "Memo stream failed for {Id}", id);
            await WriteSseAsync(ctx.Response, MemoStreamEventTypes.Error, new { message = "تعذر إكمال إعداد المذكرة." }, ct);
        }
    }

    private static async Task WriteSseAsync(HttpResponse response, string eventName, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, payload.GetType(), JsonDefaults.Options);
        await response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
}
