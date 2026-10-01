namespace Gta.LegalCopilot.Domain.Models;

public record LedgerEntry(
    string LedgerEntryName, decimal OriginalAssessmentQar, decimal SettlementProposalQar,
    decimal VarianceQar, string LegalBasis
);

public record FinancialRecalculationResult(
    string Currency, string ComputationMode, decimal CorporateTaxRate,
    decimal RevisedTaxDiffQar, decimal RevisedDelayPenaltiesQar, bool IsPenaltyCapped,
    decimal RevisedTotalDueQar, decimal TaxReliefQar, decimal PenaltyReliefQar,
    decimal FinalTreasuryReceivableQar, IReadOnlyList<LedgerEntry> LedgerComparisonMatrix
);
