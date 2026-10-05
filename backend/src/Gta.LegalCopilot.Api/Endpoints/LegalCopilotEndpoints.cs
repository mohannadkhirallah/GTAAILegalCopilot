namespace Gta.LegalCopilot.Api.Endpoints;

/// <summary>Composes the demo, dossier, memo, and legal-chat API modules.</summary>
public static class LegalCopilotEndpoints
{
    public static IEndpointRouteBuilder MapLegalCopilotEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapSystemEndpoints();
        app.MapDossierEndpoints();
        app.MapMemoEndpoints();
        app.MapLegalChatEndpoints();
        return app;
    }
}
