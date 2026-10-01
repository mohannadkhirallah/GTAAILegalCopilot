namespace Gta.LegalCopilot.Application.Common;

public sealed class DossierValidationException(IReadOnlyList<string> errors)
    : Exception("Dossier validation failed: " + string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

public sealed class AiNotConfiguredException(string message) : Exception(message);
