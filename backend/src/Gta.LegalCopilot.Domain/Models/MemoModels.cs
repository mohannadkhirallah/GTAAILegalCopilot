namespace Gta.LegalCopilot.Domain.Models;

public record MemoMetadata(
    string State, string Authority, string Department, string ReferenceNumber,
    DateOnly FilingDate, string Addressee
);

/// <summary>Ordered memo section keys. Order defines rendering order in Word and UI.</summary>
public static class MemoSections
{
    public const string Preamble = "preamble";
    public const string Facts = "facts";
    public const string FormalDefense = "formalDefense";
    public const string SubstantiveDefense = "substantiveDefense";
    public const string FinancialImpact = "financialImpact";
    public const string Requests = "requests";

    public static readonly IReadOnlyList<string> Ordered =
        [Preamble, Facts, FormalDefense, SubstantiveDefense, FinancialImpact, Requests];

    public static readonly IReadOnlyDictionary<string, string> TitlesAr = new Dictionary<string, string>
    {
        [Preamble] = "الديباجة",
        [Facts] = "أولاً: الوقائع",
        [FormalDefense] = "ثانياً: الدفع الشكلي المتعلق بالنظام العام",
        [SubstantiveDefense] = "ثالثاً: الرد الموضوعي (على سبيل الاحتياط الكلي)",
        [FinancialImpact] = "رابعاً: الأثر المالي وإعادة الاحتساب",
        [Requests] = "الطلبات",
    };
}

public record AssembledMemo(
    string DocumentType, MemoMetadata Metadata, Dictionary<string, string> Sections,
    IReadOnlyList<string> FinalRequests, ProceduralVerdict Procedural,
    SubstantiveAuditSummary Substantive, FinancialRecalculationResult Financial,
    string NarrativeSource, DateTimeOffset GeneratedAtUtc
);
