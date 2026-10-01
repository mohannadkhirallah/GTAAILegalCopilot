using System.Runtime.CompilerServices;
using Gta.LegalCopilot.Application.Abstractions;
using OpenAI.Chat;

namespace Gta.LegalCopilot.Infrastructure.Ai;

/// <summary>Streams a legally-polished Arabic rewrite of a deterministic draft section (SSE source).</summary>
public sealed class AzureOpenAiNarrativeGenerator(ChatClientFactory factory) : ILegalNarrativeGenerator
{
    private const string SystemPrompt = """
        أنت مستشار قانوني أول في الهيئة العامة للضرائب بدولة قطر (إدارة ضريبة الدخل)، تصوغ مذكرات الرد أمام لجنة التظلم الضريبي.
        مهمتك الوحيدة: إعادة صياغة المسودة المقدمة بلغة قانونية عربية رصينة وفق أسلوب المذكرات القضائية القطرية.
        قواعد ملزمة لا يجوز مخالفتها:
        1. لا تُجرِ أي عملية حسابية ولا تحتسب أي تاريخ أو ميعاد.
        2. لا تضف ولا تحذف ولا تعدل أي رقم أو مبلغ أو تاريخ أو رقم مادة؛ انقلها حرفياً كما وردت بالأرقام الغربية.
        3. لا تغير النتيجة المقررة لأي بند (رفض كلي / قبول جزئي / قبول كلي) ولا تستحدث سنداً قانونياً أو حكماً قضائياً غير وارد بالمسودة.
        4. أخرج النص النهائي فقط دون عناوين إضافية أو تعليقات أو تنسيق Markdown.
        """;

    public bool IsEnabled => factory.IsConfigured;

    public async IAsyncEnumerable<string> StreamRefinedSectionAsync(string sectionKey, string deterministicDraft, [EnumeratorCancellation] CancellationToken ct = default)
    {
        List<ChatMessage> messages =
        [
            new SystemChatMessage(SystemPrompt),
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
