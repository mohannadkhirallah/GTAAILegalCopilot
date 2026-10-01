namespace Gta.LegalCopilot.Domain.Models;

public record TaxpayerProfile(
    string NameAr, string? NameEn, string Tin, string CrNumber, string LegalForm, string CommercialActivity
);

public record DisputedItem(
    string ItemId, string DescriptionAr, decimal ClaimedAmountQar,
    string TaxpayerDefense, string TaxpayerLegalReference, bool AttachmentsPresent
);

public record DhareebaRecord(
    string AssessmentNoticeRef, DateOnly AssessmentNoticeDate, DateOnly StatutoryObjectionDeadline,
    bool AdministrativeObjectionFiled, DateOnly? AdministrativeObjectionDate,
    decimal OriginalAssessedTaxDiffQar, decimal OriginalDelayPenaltiesQar, decimal TotalOriginalClaimQar
);

public record DisputeDossier(
    string DossierId, string CommitteeRecordNumber, DateOnly CommitteeFilingDate,
    string DisputedFiscalYear, TaxpayerProfile Taxpayer, DhareebaRecord DhareebaRecord,
    IReadOnlyList<DisputedItem> DisputedItems
);
