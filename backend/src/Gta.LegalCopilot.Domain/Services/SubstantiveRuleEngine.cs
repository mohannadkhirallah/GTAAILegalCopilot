using Gta.LegalCopilot.Domain.Models;

namespace Gta.LegalCopilot.Domain.Services;

public enum ItemCategory { ExpectedCreditLossProvision, FairValueLoss, ManagementFees, WithholdingTax, TransferPricing, Other }

/// <summary>Configurable GTA policy ratios applied when an evidenced claim is admitted in part.</summary>
public sealed record SubstantivePolicy
{
    public decimal EvidencedManagementFeeAdmissibleShare { get; init; } = 0.50m;
    public decimal EvidencedTransferPricingAdmissibleShare { get; init; } = 0.50m;
    public static SubstantivePolicy Default { get; } = new();
}

public static class EvidenceStatuses
{
    public const string Sufficient = "SUPPORTED_BY_DOCUMENTS";
    public const string Missing = "NO_SUPPORTING_DOCUMENTS";
    public const string Irrelevant = "DOCUMENTS_LEGALLY_IRRELEVANT";
    public const string ManualReview = "REQUIRES_MANUAL_REVIEW";
}

public static class DefensePostures
{
    public const string AlternativeReserve = "ALTERNATIVE_RESERVE";
    public const string SubstantiveMerits = "SUBSTANTIVE_MERITS";
}

public sealed record ItemRuleOutcome(ItemCategory Category, ItemEvaluation Evaluation, bool AffectsTaxDirectly);

/// <summary>
/// Deterministic classification and determination of disputed items. The LLM may only rephrase
/// the Arabic reasoning; amounts and determinations are fixed here.
/// </summary>
public sealed class SubstantiveRuleEngine(SubstantivePolicy policy)
{
    private static readonly (ItemCategory Category, string[] Keywords)[] Classifier =
    [
        (ItemCategory.TransferPricing, ["تسعير التحويل", "أسعار التحويل", "السعر المحايد", "transfer pricing", "arm's length", "33 مكرر"]),
        (ItemCategory.WithholdingTax, ["استقطاع", "الاستقطاع", "withholding", "wht", "غير المقيم"]),
        (ItemCategory.ExpectedCreditLossProvision, ["ifrs 9", "ifrs9", "خسائر ائتمانية", "الخسائر الائتمانية", "مخصص", "expected credit"]),
        (ItemCategory.FairValueLoss, ["القيمة العادلة", "fair value", "إعادة التقييم", "غير محققة"]),
        (ItemCategory.ManagementFees, ["أتعاب إدارة", "أتعاب الإدارة", "رسوم إدارة", "المركز الرئيسي", "management fee", "head office"]),
    ];

    public static ItemCategory Classify(DisputedItem item)
    {
        var text = $"{item.DescriptionAr} {item.TaxpayerLegalReference} {item.TaxpayerDefense}".ToLowerInvariant();
        foreach (var (category, keywords) in Classifier)
            if (keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return category;
        return ItemCategory.Other;
    }

    public ItemRuleOutcome EvaluateItem(DisputedItem item)
    {
        var category = Classify(item);
        var amount = Math.Max(0, item.ClaimedAmountQar);
        var evidenced = item.AttachmentsPresent;

        (DeterminationType det, decimal admitted, string statute, string evidence, string reasoning) = category switch
        {
            ItemCategory.ExpectedCreditLossProvision => (DeterminationType.RejectedFully, 0m,
                "المادة (8) من قانون ضريبة الدخل واللائحة التنفيذية",
                evidenced ? EvidenceStatuses.Irrelevant : EvidenceStatuses.Missing,
                "المخصصات المحتسبة وفقاً لنموذج الخسائر الائتمانية المتوقعة (IFRS 9) هي تقديرات محاسبية لخسائر مستقبلية محتملة لم تتحقق بعد، ولا تعد من التكاليف الفعلية واجبة الخصم، إذ لا يُقبل خصم الديون إلا إذا كانت معدومة فعلاً وتم شطبها بعد استنفاد وسائل التحصيل وفق الشروط المقررة، ومن ثم يتعين رفض هذا البند."),
            ItemCategory.FairValueLoss => (DeterminationType.RejectedFully, 0m,
                "المادة (9) من قانون ضريبة الدخل",
                evidenced ? EvidenceStatuses.Irrelevant : EvidenceStatuses.Missing,
                "خسائر إعادة التقييم بالقيمة العادلة خسائر دفترية غير محققة لم تنشأ عن تصرف ناقل للملكية، والعبرة في الوعاء الضريبي بالأرباح والخسائر المحققة فعلاً، فلا يجوز خصمها قبل تحققها بالبيع أو التصرف."),
            ItemCategory.ManagementFees when evidenced => (DeterminationType.AcceptedPartially,
                DelayPenaltyCalculator.Round(amount * policy.EvidencedManagementFeeAdmissibleShare),
                "المادة (33) من قانون ضريبة الدخل واللائحة التنفيذية", EvidenceStatuses.Sufficient,
                "قدم المتظلم مستندات مؤيدة لجانب من أتعاب الإدارة المحملة من الأطراف المرتبطة، وإعمالاً للضوابط المقررة لخصم تكاليف المركز الرئيسي والأطراف المرتبطة بما يتفق ومبدأ السعر المحايد، تقر الهيئة بالجزء الثابت منها وترفض ما جاوزه لعدم ثبوت الخدمة الفعلية وارتباطها بتحقيق الدخل."),
            ItemCategory.ManagementFees => (DeterminationType.RejectedFully, 0m,
                "المادة (33) من قانون ضريبة الدخل واللائحة التنفيذية", EvidenceStatuses.Missing,
                "لم يقدم المتظلم ما يثبت أداء خدمات إدارية فعلية مقابل الأتعاب المحملة، ولا أسس التوزيع ومعاييره، ولا ما يفيد اتفاقها مع مبدأ السعر المحايد، ومن ثم تفتقر إلى شروط الخصم المقررة قانوناً."),
            ItemCategory.WithholdingTax when evidenced => (DeterminationType.AcceptedFully, amount,
                "المادة (20) من قانون ضريبة الدخل", EvidenceStatuses.Sufficient,
                "ثبت من المستندات المقدمة أن الخدمات محل هذا البند قد أُديت بالكامل خارج دولة قطر، ومن ثم لا تعد دخلاً من مصدر في الدولة خاضعاً للضريبة بطريق الاستقطاع، وتقر الهيئة بهذا البند."),
            ItemCategory.WithholdingTax => (DeterminationType.RejectedFully, 0m,
                "المادة (20) من قانون ضريبة الدخل", EvidenceStatuses.Missing,
                "المبالغ المدفوعة لغير المقيمين مقابل خدمات تخضع للضريبة بطريق الاستقطاع ويلتزم الدافع بخصمها وتوريدها خلال الميعاد المقرر، ولم يقدم المتظلم دليلاً على أداء الخدمات بالكامل خارج الدولة أو على توافر حالة إعفاء، فيظل ملتزماً بالضريبة المستحقة بصفته المسؤول عن الاستقطاع."),
            ItemCategory.TransferPricing when evidenced => (DeterminationType.AcceptedPartially,
                DelayPenaltyCalculator.Round(amount * policy.EvidencedTransferPricingAdmissibleShare),
                "المادة (33) مكرراً من قانون ضريبة الدخل واللائحة التنفيذية", EvidenceStatuses.Sufficient,
                "قدم المتظلم دراسة لأسعار التحويل تؤيد جزئياً اتفاق المعاملات مع الأطراف المرتبطة مع مبدأ السعر المحايد، وتقر الهيئة بالجزء المؤيد بالمقارنات المقبولة، مع تأييد التعديل فيما جاوزه."),
            ItemCategory.TransferPricing => (DeterminationType.RejectedFully, 0m,
                "المادة (33) مكرراً من قانون ضريبة الدخل واللائحة التنفيذية", EvidenceStatuses.Missing,
                "لم يقدم المتظلم ملف أسعار التحويل أو دراسة مقارنة تثبت اتفاق معاملاته مع الأطراف المرتبطة مع مبدأ السعر المحايد، ومن ثم يحق للهيئة تعديل الدخل الخاضع للضريبة بما يعكس السعر المحايد."),
            _ => (DeterminationType.RejectedFully, 0m,
                "قانون ضريبة الدخل واللائحة التنفيذية",
                EvidenceStatuses.ManualReview,
                "لم يثبت المتظلم توافر شروط الخصم المقررة قانوناً لهذا البند، وتتمسك الهيئة بقرار الربط بشأنه، مع إحالة البند للمراجعة القانونية التفصيلية."),
        };

        var evaluation = new ItemEvaluation(item.ItemId, item.DescriptionAr, amount, det, admitted, statute, reasoning, evidence);
        return new ItemRuleOutcome(category, evaluation, category == ItemCategory.WithholdingTax);
    }

    public (SubstantiveAuditSummary Summary, IReadOnlyList<ItemRuleOutcome> Outcomes) Evaluate(DisputeDossier dossier, ProceduralVerdict verdict)
    {
        var outcomes = dossier.DisputedItems.Select(EvaluateItem).ToList();
        var evaluations = outcomes.Select(o => o.Evaluation).ToList();
        var claimed = evaluations.Sum(e => e.ClaimedAmountQar);
        var admitted = evaluations.Sum(e => e.AdmittedDeductionQar);
        var summary = new SubstantiveAuditSummary(
            claimed, admitted, claimed - admitted,
            verdict.IsAdmissibleFormally ? DefensePostures.SubstantiveMerits : DefensePostures.AlternativeReserve,
            evaluations);
        return (summary, outcomes);
    }
}
