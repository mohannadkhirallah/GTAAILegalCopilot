using System.Globalization;
using System.Text.Json;
using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Application.Common;
using Gta.LegalCopilot.Domain.Models;
using Gta.LegalCopilot.Domain.Services;
using OpenAI.Chat;

namespace Gta.LegalCopilot.Infrastructure.Ai;

/// <summary>
/// Structured-output (strict JSON schema) extraction of a dossier from petition text.
/// The LLM only transcribes facts; deadlines and totals are computed afterwards in C#.
/// </summary>
public sealed class AzureOpenAiDossierExtractor(ChatClientFactory factory, StatutoryDeadlineCalculator deadlines) : IDossierExtractor
{
    public bool IsEnabled => factory.IsConfigured;

    private const string SystemPrompt = """
        أنت محلل قانوني في الهيئة العامة للضرائب بدولة قطر. استخرج بيانات ملف التظلم الضريبي من النص المقدم بدقة حرفية.
        - انقل الأرقام والمبالغ والتواريخ كما وردت في المستند فقط، ولا تحتسب أي ميعاد أو مجموع أو غرامة.
        - التواريخ بصيغة YYYY-MM-DD. المبالغ أرقام بالريال القطري دون فواصل.
        - إذا لم يرد ما يفيد تقديم اعتراض إداري إلى الهيئة فاجعل administrativeObjectionFiled=false و administrativeObjectionDate=null.
        - إذا تعذر العثور على قيمة نصية فأعد سلسلة فارغة، وللأرقام غير المذكورة أعد 0.
        """;

    private static readonly BinaryData Schema = BinaryData.FromString("""
    {
      "type": "object", "additionalProperties": false,
      "required": ["committeeRecordNumber","committeeFilingDate","disputedFiscalYear","taxpayer","assessment","disputedItems"],
      "properties": {
        "committeeRecordNumber": {"type":"string"},
        "committeeFilingDate": {"type":"string"},
        "disputedFiscalYear": {"type":"string"},
        "taxpayer": {"type":"object","additionalProperties":false,
          "required":["nameAr","nameEn","tin","crNumber","legalForm","commercialActivity"],
          "properties":{"nameAr":{"type":"string"},"nameEn":{"type":["string","null"]},"tin":{"type":"string"},
            "crNumber":{"type":"string"},"legalForm":{"type":"string"},"commercialActivity":{"type":"string"}}},
        "assessment": {"type":"object","additionalProperties":false,
          "required":["assessmentNoticeRef","assessmentNoticeDate","administrativeObjectionFiled","administrativeObjectionDate","originalAssessedTaxDiffQar","originalDelayPenaltiesQar"],
          "properties":{"assessmentNoticeRef":{"type":"string"},"assessmentNoticeDate":{"type":"string"},
            "administrativeObjectionFiled":{"type":"boolean"},"administrativeObjectionDate":{"type":["string","null"]},
            "originalAssessedTaxDiffQar":{"type":"number"},"originalDelayPenaltiesQar":{"type":"number"}}},
        "disputedItems": {"type":"array","items":{"type":"object","additionalProperties":false,
          "required":["descriptionAr","claimedAmountQar","taxpayerDefense","taxpayerLegalReference","attachmentsPresent"],
          "properties":{"descriptionAr":{"type":"string"},"claimedAmountQar":{"type":"number"},"taxpayerDefense":{"type":"string"},
            "taxpayerLegalReference":{"type":"string"},"attachmentsPresent":{"type":"boolean"}}}}
      }
    }
    """);

    private sealed record Extracted(string CommitteeRecordNumber, string CommitteeFilingDate, string DisputedFiscalYear,
        TaxpayerProfile Taxpayer, ExtractedAssessment Assessment, List<ExtractedItem> DisputedItems);
    private sealed record ExtractedAssessment(string AssessmentNoticeRef, string AssessmentNoticeDate, bool AdministrativeObjectionFiled,
        string? AdministrativeObjectionDate, decimal OriginalAssessedTaxDiffQar, decimal OriginalDelayPenaltiesQar);
    private sealed record ExtractedItem(string DescriptionAr, decimal ClaimedAmountQar, string TaxpayerDefense, string TaxpayerLegalReference, bool AttachmentsPresent);

    public async Task<DisputeDossier> ExtractAsync(string documentText, CancellationToken ct = default)
    {
        if (!IsEnabled) throw new AiNotConfiguredException("Azure OpenAI is not configured; upload a JSON dossier or use a demo case.");
        var options = new ChatCompletionOptions
        {
            Temperature = 0,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("gta_dispute_dossier", Schema, jsonSchemaIsStrict: true),
        };
        List<ChatMessage> messages = [new SystemChatMessage(SystemPrompt), new UserChatMessage(documentText)];
        ChatCompletion completion = await factory.Client.CompleteChatAsync(messages, options, ct);
        var json = completion.Content.FirstOrDefault()?.Text ?? throw new InvalidOperationException("Empty extraction response.");
        var x = JsonSerializer.Deserialize<Extracted>(json, JsonDefaults.Options) ?? throw new InvalidOperationException("Invalid extraction JSON.");
        return Map(x);
    }

    private DisputeDossier Map(Extracted x)
    {
        var a = x.Assessment;
        var notice = ParseDate(a.AssessmentNoticeDate, "assessmentNoticeDate");
        DateOnly? objection = a.AdministrativeObjectionFiled && !string.IsNullOrWhiteSpace(a.AdministrativeObjectionDate)
            ? ParseDate(a.AdministrativeObjectionDate, "administrativeObjectionDate") : null;
        var record = new DhareebaRecord(
            a.AssessmentNoticeRef, notice, deadlines.ObjectionDeadline(notice),
            objection is not null, objection,
            a.OriginalAssessedTaxDiffQar, a.OriginalDelayPenaltiesQar,
            a.OriginalAssessedTaxDiffQar + a.OriginalDelayPenaltiesQar);
        var items = x.DisputedItems.Select((i, idx) => new DisputedItem(
            $"ITEM-{idx + 1:00}", i.DescriptionAr, i.ClaimedAmountQar, i.TaxpayerDefense, i.TaxpayerLegalReference, i.AttachmentsPresent)).ToList();
        return new DisputeDossier(string.Empty, x.CommitteeRecordNumber, ParseDate(x.CommitteeFilingDate, "committeeFilingDate"),
            x.DisputedFiscalYear, x.Taxpayer, record, items);
    }

    private static DateOnly ParseDate(string? value, string field) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : throw new DossierValidationException([$"Could not extract a valid {field} from the document."]);
}

public sealed class DisabledDossierExtractor : IDossierExtractor
{
    public bool IsEnabled => false;
    public Task<DisputeDossier> ExtractAsync(string documentText, CancellationToken ct = default) =>
        throw new AiNotConfiguredException("Azure OpenAI is not configured; upload a JSON dossier or use a demo case.");
}
