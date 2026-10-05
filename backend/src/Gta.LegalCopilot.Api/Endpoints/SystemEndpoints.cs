using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Application.Chat;
using Gta.LegalCopilot.Domain.Models;

namespace Gta.LegalCopilot.Api.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");
        api.MapGet("/health", (IDossierExtractor extractor, ILegalNarrativeGenerator narrative,
            ILegalKnowledgeSearch search, ILegalChatAgent chat) =>
            Results.Ok(new { status = "ok", aiExtractionEnabled = extractor.IsEnabled, aiNarrativeEnabled = narrative.IsEnabled,
                legalSearchEnabled = search.IsEnabled, legalChatEnabled = chat.IsEnabled }));

        api.MapGet("/demo-cases", (IDemoCaseCatalog catalog) => Results.Ok(catalog.List()));

        api.MapPost("/demo-cases/{key}/load", async (string key, IDemoCaseCatalog catalog, IDossierRepository repo, CancellationToken ct) =>
        {
            var dossier = catalog.Get(key);
            if (dossier is null) return Results.NotFound();
            await repo.SaveAsync(new StoredDossier(dossier, $"demo:{key}", DateTimeOffset.UtcNow, []), ct);
            return Results.Ok(dossier);
        });

        return app;
    }
}
