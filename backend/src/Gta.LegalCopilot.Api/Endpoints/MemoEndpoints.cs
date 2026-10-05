using System.Text.Json;
using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Application.Common;
using Gta.LegalCopilot.Application.Services;

namespace Gta.LegalCopilot.Api.Endpoints;

public static class MemoEndpoints
{
    public static IEndpointRouteBuilder MapMemoEndpoints(this IEndpointRouteBuilder app)
    {
        var dossiers = app.MapGroup("/api/dossiers");
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
