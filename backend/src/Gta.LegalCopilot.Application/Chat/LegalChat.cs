using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Application.Common;
using Gta.LegalCopilot.Domain.Models;

namespace Gta.LegalCopilot.Application.Chat;

/// <summary>Validates public chat input independently of the agent and its provider.</summary>
public sealed class LegalChatService(ILegalChatAgent agent)
{
    public const int MaxMessageLength = 4000;
    public const int MaxHistoryMessages = 20;
    public const int MaxHistoryMessageLength = 12000;
    public const int MaxHistoryCharacters = 60000;

    public Task<LegalChatAnswer> AnswerAsync(LegalChatRequest request, CancellationToken ct = default,
        Func<string, Task>? onDelta = null, LegalChatContext? context = null)
    {
        ValidateRequest(request);
        return agent.AnswerAsync(request with { Message = request.Message.Trim() }, ct, onDelta, context);
    }

    public void ValidateRequest(LegalChatRequest request)
    {
        Validate(request);
        if (!agent.IsEnabled)
            throw new AiNotConfiguredException("المساعد القانوني غير مهيأ. تحقق من إعدادات Azure OpenAI وAzure AI Search ونشر نموذج التضمين.");
    }

    public static void Validate(LegalChatRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > MaxMessageLength)
            errors.Add($"Message must contain between 1 and {MaxMessageLength} characters.");
        var history = request.History ?? [];
        if (history.Count > MaxHistoryMessages)
            errors.Add($"At most {MaxHistoryMessages} history messages are supported.");
        foreach (var message in history)
        {
            if (message is null || message.Role is not ("user" or "assistant"))
                errors.Add("History roles must be user or assistant.");
            if (message is null || string.IsNullOrWhiteSpace(message.Text) || message.Text.Length > MaxHistoryMessageLength)
                errors.Add("History messages must contain text within the size limit.");
        }
        if (history.Sum(m => (long)(m?.Text?.Length ?? 0)) > MaxHistoryCharacters)
            errors.Add("Conversation history exceeds the size limit.");
        if (errors.Count > 0) throw new ChatValidationException(errors);
    }
}

public sealed record LegalChatMessage(string Role, string Text);
public sealed record LegalChatRequest(string Message, IReadOnlyList<LegalChatMessage>? History = null);
public sealed record LegalChatAnswer(string Answer, IReadOnlyList<LegalSource> Sources, bool Grounded);
// Loaded by the server from the selected dossier; never accepted as client-supplied facts.
public sealed record LegalChatContext(DisputeDossier Dossier, CaseAnalysis Analysis,
    AssembledMemo? Memo = null, string? DocumentText = null);

/// <summary>A retrieved law chunk. Citation IDs are assigned per answer by the server.</summary>
public sealed record LegalSource(
    string Id, string? LawId, string? RegulationType, string? Number, string? Year,
    string? OfficialTitle, string? Date, string? Status, string? Type,
    string? AmendedLawNumber, string? AmendedLawName, string? AmendedLawYear,
    int? ArticlesCount, string? SourceUrl, string? OfficialGazetteIssueNumber,
    string? OfficialGazettePublicationDate, string? OfficialGazettePage,
    string? LawDescription, int? LawHeaderTokenSize, string? ArticleNumber,
    int? ChunkNumber, string Content, int? TokenSize, double? Score,
    string CitationId = "");

public interface ILegalKnowledgeSearch
{
    bool IsEnabled { get; }
    Task<IReadOnlyList<LegalSource>> SearchAsync(string query, string? lawNumber = null,
        string? lawYear = null, CancellationToken ct = default);
}

public interface ILegalChatAgent
{
    bool IsEnabled { get; }
    Task<LegalChatAnswer> AnswerAsync(LegalChatRequest request, CancellationToken ct = default,
        Func<string, Task>? onDelta = null, LegalChatContext? context = null);
}

public sealed class ChatValidationException(IReadOnlyList<string> errors) : Exception("Invalid chat request")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

public sealed class LegalChatUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
