using System.Globalization;
using System.Text;
using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Domain.Models;
using Gta.LegalCopilot.Domain.Services;
using Gta.LegalCopilot.Domain.Statutes;

namespace Gta.LegalCopilot.Application.Services;

/// <summary>Deterministic, template-based composition of the Arabic rebuttal memo.</summary>
public sealed class MemoComposer
{
    public const string DocumentType = "مذكرة رد وتعقيب موضوعي وشكلي على التظلم الضريبي";
    public const string Addressee = "السيد / رئيس لجنة التظلم الضريبي المحترم،،،";

    public static string Money(decimal v) => v.ToString("N2", CultureInfo.InvariantCulture) + " ريال قطري";
    private static string D(DateOnly d) => ProceduralAdmissibilityService.Fmt(d);

    public static string DeterminationAr(DeterminationType t) => t switch
    {
        DeterminationType.AcceptedFully => "قبول كلي",
        DeterminationType.AcceptedPartially => "قبول جزئي",
        _ => "رفض كلي",
    };

    public MemoMetadata BuildMetadata(DisputeDossier d) => new(
        State: "دولة قطر",
        Authority: "الهيئة العامة للضرائب",
        Department: "إدارة ضريبة الدخل",
        ReferenceNumber: string.IsNullOrWhiteSpace(d.CommitteeRecordNumber) ? d.DossierId : d.CommitteeRecordNumber,
        FilingDate: d.CommitteeFilingDate,
        Addressee: Addressee);

    public Dictionary<string, string> ComposeSections(DisputeDossier d, CaseAnalysis a) => new()
    {
        [MemoSections.Preamble] = Preamble(d),
        [MemoSections.Facts] = Facts(d),
        [MemoSections.FormalDefense] = FormalDefense(a.Procedural),
        [MemoSections.SubstantiveDefense] = Substantive(d, a),
        [MemoSections.FinancialImpact] = Financial(a),
        [MemoSections.Requests] = string.Join("\n", Requests(d, a).Select((r, i) => $"{i + 1}. {r}")),
    };

    public IReadOnlyList<string> Requests(DisputeDossier d, CaseAnalysis a)
    {
        var f = a.Financial;
        var noticeRef = d.DhareebaRecord.AssessmentNoticeRef;
        var list = new List<string>();
        if (!a.Procedural.IsAdmissibleFormally)
        {
            list.Add($"أصلياً: {ProceduralAdmissibilityService.InadmissibilityRuling}، وتأييد قرار الربط رقم ({noticeRef}) بكامل ما تضمنه، مع إعمال الحد الأقصى لغرامات التأخير المقرر بالمادة (24) بما يجعل المستحق للخزانة العامة {Money(f.FinalTreasuryReceivableQar)}.");
            list.Add($"واحتياطياً كلياً: رفض التظلم موضوعاً فيما عدا ما أقرت به الهيئة من بنود، وتعديل المطالبة لتصبح {Money(f.RevisedTotalDueQar)} (فرق ضريبة {Money(f.RevisedTaxDiffQar)} وغرامات تأخير {Money(f.RevisedDelayPenaltiesQar)}).");
        }
        else
        {
            list.Add("قبول التظلم شكلاً.");
            list.Add($"وفي الموضوع: رفض التظلم وتأييد قرار الربط رقم ({noticeRef}) فيما عدا ما أقرت به الهيئة، وتعديل المطالبة لتصبح {Money(f.RevisedTotalDueQar)} (فرق ضريبة {Money(f.RevisedTaxDiffQar)} وغرامات تأخير {Money(f.RevisedDelayPenaltiesQar)}).");
        }
        list.Add("وفي جميع الأحوال: تأييد استحقاق غرامات التأخير وفقاً للمادة (24) من قانون ضريبة الدخل حتى تمام السداد، بما لا يجاوز أصل الضريبة المستحقة.");
        return list;
    }

    private static string Preamble(DisputeDossier d)
    {
        var t = d.Taxpayer;
        var sb = new StringBuilder();
        sb.AppendLine(DocumentType);
        sb.AppendLine("مقدمة من: الهيئة العامة للضرائب — إدارة ضريبة الدخل (متظلم ضدها)");
        sb.AppendLine($"ضد: {t.NameAr}{(string.IsNullOrWhiteSpace(t.NameEn) ? "" : $" ({t.NameEn})")} — {t.LegalForm} (متظلم)");
        sb.AppendLine($"الرقم الضريبي: {t.Tin} — السجل التجاري: {t.CrNumber}");
        sb.AppendLine($"في التظلم المقيد برقم ({d.CommitteeRecordNumber}) بتاريخ {D(d.CommitteeFilingDate)} عن السنة الضريبية {d.DisputedFiscalYear}");
        sb.AppendLine();
        sb.AppendLine(Addressee);
        sb.AppendLine("تحية طيبة وبعد،،،");
        sb.Append($"تتشرف الهيئة العامة للضرائب بأن تتقدم إلى عدالتكم بهذه المذكرة رداً وتعقيباً على التظلم المشار إليه، استناداً إلى أحكام {LegalCorpus.Law} و{LegalCorpus.Regulations} و{LegalCorpus.CabinetDecision}، وذلك على النحو الآتي:");
        return sb.ToString();
    }

    private static string Facts(DisputeDossier d)
    {
        var r = d.DhareebaRecord;
        var sb = new StringBuilder();
        sb.Append($"تخلص وقائع التظلم في أن الهيئة أصدرت قرار الربط رقم ({r.AssessmentNoticeRef}) عن السنة الضريبية {d.DisputedFiscalYear} في مواجهة {d.Taxpayer.NameAr}، الذي يزاول نشاط {d.Taxpayer.CommercialActivity}، وأُخطر به بتاريخ {D(r.AssessmentNoticeDate)}، متضمناً فرق ضريبة قدره {Money(r.OriginalAssessedTaxDiffQar)} وغرامات تأخير قدرها {Money(r.OriginalDelayPenaltiesQar)}. ");
        sb.Append(r.AdministrativeObjectionFiled && r.AdministrativeObjectionDate is { } od
            ? $"وقد تقدم المتظلم باعتراض إداري إلى الهيئة بتاريخ {D(od)}، "
            : "ولم يتقدم المتظلم بأي اعتراض إداري إلى الهيئة، ");
        sb.Append($"ثم قيد تظلمه أمام لجنة التظلم الضريبي بتاريخ {D(d.CommitteeFilingDate)}، منازعاً في عدد ({d.DisputedItems.Count}) من البنود بإجمالي مبالغ مطالب بخصمها قدرها {Money(d.DisputedItems.Sum(i => i.ClaimedAmountQar))}.");
        return sb.ToString();
    }

    private static string FormalDefense(ProceduralVerdict v)
    {
        var sb = new StringBuilder();
        sb.AppendLine(v.FormulatedDefenseClauseAr);
        sb.AppendLine();
        sb.AppendLine("السند القانوني:");
        foreach (var b in v.GoverningLegalBasis)
            sb.AppendLine($"- {b.Article} من {b.Source}: {b.RuleSummary}");
        sb.Append($"المبدأ القضائي ({v.JurisprudenceDoctrine.Court}): {v.JurisprudenceDoctrine.PrincipleAr}");
        return sb.ToString();
    }

    private static string Substantive(DisputeDossier d, CaseAnalysis a)
    {
        var sb = new StringBuilder();
        sb.AppendLine(a.Procedural.IsAdmissibleFormally
            ? "وحيث إن الهيئة لا تمانع في قبول التظلم شكلاً، فإنها ترد على موضوعه تفصيلاً على النحو الآتي:"
            : "وعلى سبيل الاحتياط الكلي، ودون أن يُعد ذلك تنازلاً من الهيئة عن دفعها الشكلي المتعلق بالنظام العام أو تسليماً بأي من طلبات المتظلم، ترد الهيئة على موضوع التظلم على النحو الآتي:");
        var items = d.DisputedItems.ToDictionary(i => i.ItemId);
        var n = 1;
        foreach (var e in a.Substantive.LineItemsEvaluation)
        {
            items.TryGetValue(e.ItemId, out var src);
            sb.AppendLine();
            sb.AppendLine($"البند ({n++}) — {e.LineName}: المبلغ محل النزاع {Money(e.ClaimedAmountQar)}.");
            if (src is not null && !string.IsNullOrWhiteSpace(src.TaxpayerDefense))
                sb.AppendLine($"دفاع المتظلم: {src.TaxpayerDefense}{(string.IsNullOrWhiteSpace(src.TaxpayerLegalReference) ? "" : $" (مستنداً إلى: {src.TaxpayerLegalReference})")}.");
            sb.AppendLine($"رد الهيئة: {e.LegalReasoningAr}");
            sb.AppendLine($"السند: {e.StatutoryReference}.");
            sb.Append($"النتيجة: {DeterminationAr(e.GtaDetermination)} — المبلغ المقر به {Money(e.AdmittedDeductionQar)}.");
        }
        sb.AppendLine();
        sb.AppendLine();
        sb.Append($"وبذلك يبلغ إجمالي المبالغ محل النزاع {Money(a.Substantive.TotalDisputedClaimedQar)}، أقرت الهيئة منها بمبلغ {Money(a.Substantive.TotalConcessionsAdmittedQar)}، وتتمسك برفض الباقي ومقداره {Money(a.Substantive.TotalDisallowedConfirmedQar)}.");
        return sb.ToString();
    }

    private static string Financial(CaseAnalysis a)
    {
        var f = a.Financial;
        var sb = new StringBuilder();
        sb.AppendLine($"أعادت الهيئة احتساب الأثر المالي آلياً وفق السعر الضريبي المقرر ({(f.CorporateTaxRate * 100).ToString("0.##", CultureInfo.InvariantCulture)}%) وأحكام المادة (24)، وذلك على النحو الآتي:");
        foreach (var l in f.LedgerComparisonMatrix)
            sb.AppendLine($"- {l.LedgerEntryName}: الربط الأصلي {Money(l.OriginalAssessmentQar)} ← المقترح {Money(l.SettlementProposalQar)} (الفرق {Money(l.VarianceQar)}) — {l.LegalBasis}.");
        sb.AppendLine($"- الأثر الضريبي للبنود المقر بها: {Money(f.TaxReliefQar)}، وتخفيض الغرامات: {Money(f.PenaltyReliefQar)}.");
        if (f.IsPenaltyCapped)
            sb.AppendLine("- تم إعمال الحد الأقصى لغرامة التأخير بما لا يجاوز 100% من أصل الضريبة عملاً بالمادة (24).");
        sb.Append(a.Procedural.IsAdmissibleFormally
            ? $"ومن ثم يكون المستحق للخزانة العامة مبلغ {Money(f.FinalTreasuryReceivableQar)}."
            : $"ومن ثم يكون المستحق للخزانة العامة أصلياً مبلغ {Money(f.FinalTreasuryReceivableQar)}، واحتياطياً مبلغ {Money(f.RevisedTotalDueQar)}.");
        return sb.ToString();
    }
}
