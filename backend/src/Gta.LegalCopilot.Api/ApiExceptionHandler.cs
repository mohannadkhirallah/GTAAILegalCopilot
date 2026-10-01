using Gta.LegalCopilot.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Gta.LegalCopilot.Api;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, title, errors) = ex switch
        {
            DossierValidationException v => (StatusCodes.Status400BadRequest, "Invalid dossier", v.Errors),
            AiNotConfiguredException a => (StatusCodes.Status503ServiceUnavailable, a.Message, (IReadOnlyList<string>)[]),
            BadHttpRequestException b => (b.StatusCode, "Bad request", []),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error", []),
        };
        if (status >= 500 && ex is not AiNotConfiguredException) logger.LogError(ex, "Unhandled exception");
        ctx.Response.StatusCode = status;
        var pd = new ProblemDetails { Status = status, Title = title };
        if (errors.Count > 0) pd.Extensions["errors"] = errors;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = ctx, ProblemDetails = pd, Exception = ex });
    }
}
