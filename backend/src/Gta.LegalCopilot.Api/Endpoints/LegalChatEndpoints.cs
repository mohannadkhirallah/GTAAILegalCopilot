using System.Text.Json;
using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Application.Chat;
using Gta.LegalCopilot.Application.Common;
using Gta.LegalCopilot.Application.Services;

namespace Gta.LegalCopilot.Api.Endpoints;

public static class LegalChatEndpoints
{
    public static IEndpointRouteBuilder MapLegalChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/dossiers/{id}/chat", async (string id, LegalChatRequest request, LegalChatService chat,
            IDossierRepository repo, CaseAnalysisService analysis, HttpContext http) =>
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            try
            {
                var context = await LoadContextAsync(id, repo, analysis, timeout.Token);
                return context is null ? Results.NotFound()
                    : Results.Ok(await chat.AnswerAsync(request, timeout.Token, context: context));
            }
            catch (OperationCanceledException) when (!http.RequestAborted.IsCancellationRequested)
            {
                return Results.Problem(statusCode: StatusCodes.Status504GatewayTimeout,
                    title: "استغرق البحث وقتاً أطول من المتوقع. أعد المحاولة بسؤال أكثر تحديداً.");
            }
        });
        app.MapPost("/api/dossiers/{id}/chat/stream", StreamChatAsync);
        return app;
    }

    private static async Task StreamChatAsync(string id, LegalChatRequest request, LegalChatService chat,
        IDossierRepository repo, CaseAnalysisService analysis, HttpContext http)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        var ct = timeout.Token;
        var context = await LoadContextAsync(id, repo, analysis, ct);
        if (context is null) { http.Response.StatusCode = StatusCodes.Status404NotFound; return; }
        chat.ValidateRequest(request);
        http.Response.ContentType = "text/event-stream; charset=utf-8";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";

        async Task SendAsync(string name, object payload)
        {
            await http.Response.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(payload, JsonDefaults.Options)}\n\n", http.RequestAborted);
            await http.Response.Body.FlushAsync(http.RequestAborted);
        }

        try
        {
            await SendAsync("start", new { });
            var answer = await chat.AnswerAsync(request, ct, text => SendAsync("delta", new { text }), context);
            // The final answer replaces the draft after the existing citation checks.
            await SendAsync("done", answer);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { }
        catch (OperationCanceledException)
        {
            await SendAsync("error", new { message = "استغرقت الإجابة وقتاً أطول من المتوقع. أعد المحاولة." });
        }
        catch (Exception)
        {
            await SendAsync("error", new { message = "تعذر إكمال الإجابة. أعد المحاولة." });
        }
    }

    private static async Task<LegalChatContext?> LoadContextAsync(string id, IDossierRepository repo,
        CaseAnalysisService analysis, CancellationToken ct)
    {
        var stored = await repo.GetAsync(id, ct);
        return stored is null ? null : new LegalChatContext(stored.Dossier, analysis.Analyze(stored.Dossier),
            await repo.GetMemoAsync(id, ct), stored.ExtractedDocumentText);
    }
}
