using System.Runtime.CompilerServices;
using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Infrastructure.Ai.Prompts;
using OpenAI.Chat;

namespace Gta.LegalCopilot.Infrastructure.Ai;

/// <summary>Streams a legally-polished Arabic rewrite of a deterministic draft section (SSE source).</summary>
public sealed class AzureOpenAiNarrativeGenerator(ChatClientFactory factory) : ILegalNarrativeGenerator
{
    public bool IsEnabled => factory.IsConfigured;

    public async IAsyncEnumerable<string> StreamRefinedSectionAsync(string sectionKey, string deterministicDraft, [EnumeratorCancellation] CancellationToken ct = default)
    {
        List<ChatMessage> messages =
        [
            new SystemChatMessage(AiPrompts.MemoRefinement),
            new UserChatMessage($"القسم: {sectionKey}\n\nالمسودة:\n{deterministicDraft}"),
        ];
        var options = new ChatCompletionOptions { Temperature = 0.2f, MaxOutputTokenCount = 4000 };
        await foreach (var update in factory.Client.CompleteChatStreamingAsync(messages, options, ct))
            foreach (var part in update.ContentUpdate)
                if (!string.IsNullOrEmpty(part.Text))
                    yield return part.Text;
    }
}

public sealed class DisabledNarrativeGenerator : ILegalNarrativeGenerator
{
    public bool IsEnabled => false;
    public async IAsyncEnumerable<string> StreamRefinedSectionAsync(string sectionKey, string deterministicDraft, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}
