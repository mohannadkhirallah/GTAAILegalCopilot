using Gta.LegalCopilot.Domain.Models;
using Gta.LegalCopilot.Domain.Statutes;

namespace Gta.LegalCopilot.Domain.Services;

public static class ComputationModes
{
    public const string AlternativeReserve = "ALTERNATIVE_RESERVE_RECALCULATION";
    public const string Substantive = "SUBSTANTIVE_RECALCULATION";
}

/// <summary>Deterministic recalculation of tax and Art. (24) penalties after GTA concessions.</summary>
public sealed class FinancialRecalculationService(StatutoryParameters parameters, DelayPenaltyCalculator penalties)
{
    public FinancialRecalculationResult Recalculate(DisputeDossier dossier, ProceduralVerdict verdict, IReadOnlyList<ItemRuleOutcome> outcomes)
    {
        var rate = parameters.CorporateTaxRate;
        var record = dossier.DhareebaRecord;
        var originalTax = DelayPenaltyCalculator.Round(Math.Max(0, record.OriginalAssessedTaxDiffQar));
        var originalPenaltyRaw = DelayPenaltyCalculator.Round(Math.Max(0, record.OriginalDelayPenaltiesQar));

        // Original assessment with the Art. (24) cap enforced.
        var originalPenalty = penalties.Apply(originalTax, originalPenaltyRaw);

        // Tax effect of concessions: deductions reduce the base at the corporate rate; WHT items are tax amounts.
        var taxRelief = outcomes.Sum(o => o.AffectsTaxDirectly
            ? o.Evaluation.AdmittedDeductionQar
            : o.Evaluation.AdmittedDeductionQar * rate);
        taxRelief = Math.Min(DelayPenaltyCalculator.Round(taxRelief), originalTax);
        var revisedTax = originalTax - taxRelief;

        // Penalty follows the principal proportionally (same months elapsed), then re-capped at 100% of revised principal.
        var penaltyRatio = originalTax == 0 ? 0 : originalPenaltyRaw / originalTax;
        var revisedPenalty = penalties.Apply(revisedTax, revisedTax * penaltyRatio);

        var revisedTotal = revisedTax + revisedPenalty.Penalty;
        var originalTotal = originalTax + originalPenaltyRaw;
        var penaltyRelief = originalPenaltyRaw - revisedPenalty.Penalty;
        var finalReceivable = verdict.IsAdmissibleFormally ? revisedTotal : originalTax + originalPenalty.Penalty;

        var ledger = new List<LedgerEntry>
        {
            new("فرق الضريبة المربوطة", originalTax, revisedTax, revisedTax - originalTax,
                "قرار الربط مخصوماً منه الأثر الضريبي للبنود المقر بها"),
            new("غرامات التأخير", originalPenaltyRaw, revisedPenalty.Penalty, revisedPenalty.Penalty - originalPenaltyRaw,
                "المادة (24) — 1.5% شهرياً بحد أقصى 100% من أصل الضريبة"),
            new("إجمالي المطالبة", originalTotal, revisedTotal, revisedTotal - originalTotal,
                verdict.IsAdmissibleFormally ? "التسوية الموضوعية المقترحة" : "على سبيل الاحتياط الكلي"),
        };

        return new FinancialRecalculationResult(
            Currency: "QAR",
            ComputationMode: verdict.IsAdmissibleFormally ? ComputationModes.Substantive : ComputationModes.AlternativeReserve,
            CorporateTaxRate: rate,
            RevisedTaxDiffQar: revisedTax,
            RevisedDelayPenaltiesQar: revisedPenalty.Penalty,
            IsPenaltyCapped: originalPenalty.IsCapped || revisedPenalty.IsCapped,
            RevisedTotalDueQar: revisedTotal,
            TaxReliefQar: taxRelief,
            PenaltyReliefQar: penaltyRelief,
            FinalTreasuryReceivableQar: finalReceivable,
            LedgerComparisonMatrix: ledger);
    }
}
