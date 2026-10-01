namespace Gta.LegalCopilot.Domain.Models;

public enum DeterminationType { RejectedFully, AcceptedPartially, AcceptedFully }

public record ItemEvaluation(
    string ItemId, string LineName, decimal ClaimedAmountQar, DeterminationType GtaDetermination,
    decimal AdmittedDeductionQar, string StatutoryReference, string LegalReasoningAr, string EvidenceStatus
);

public record SubstantiveAuditSummary(
    decimal TotalDisputedClaimedQar, decimal TotalConcessionsAdmittedQar,
    decimal TotalDisallowedConfirmedQar, string DefensePosture,
    IReadOnlyList<ItemEvaluation> LineItemsEvaluation
);
