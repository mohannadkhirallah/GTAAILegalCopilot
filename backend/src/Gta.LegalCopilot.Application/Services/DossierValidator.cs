using Gta.LegalCopilot.Application.Common;
using Gta.LegalCopilot.Domain.Models;

namespace Gta.LegalCopilot.Application.Services;

public static class DossierValidator
{
    public const int MaxItems = 200;

    public static void EnsureValid(DisputeDossier? d)
    {
        var errors = new List<string>();
        if (d is null) throw new DossierValidationException(["Dossier payload is empty."]);
        if (d.Taxpayer is null) errors.Add("taxpayer is required.");
        else if (string.IsNullOrWhiteSpace(d.Taxpayer.NameAr)) errors.Add("taxpayer.nameAr is required.");
        if (d.DhareebaRecord is null) errors.Add("dhareebaRecord is required.");
        else
        {
            var r = d.DhareebaRecord;
            if (r.AssessmentNoticeDate == default) errors.Add("dhareebaRecord.assessmentNoticeDate is required.");
            if (r.OriginalAssessedTaxDiffQar < 0 || r.OriginalDelayPenaltiesQar < 0) errors.Add("Original amounts must be non-negative.");
            if (r.AdministrativeObjectionFiled && r.AdministrativeObjectionDate is null)
                errors.Add("administrativeObjectionDate is required when administrativeObjectionFiled is true.");
            if (d.CommitteeFilingDate != default && d.CommitteeFilingDate < r.AssessmentNoticeDate)
                errors.Add("committeeFilingDate cannot precede assessmentNoticeDate.");
        }
        if (d.CommitteeFilingDate == default) errors.Add("committeeFilingDate is required.");
        if (d.DisputedItems is null) errors.Add("disputedItems is required.");
        else
        {
            if (d.DisputedItems.Count > MaxItems) errors.Add($"At most {MaxItems} disputed items are supported.");
            if (d.DisputedItems.Any(i => i.ClaimedAmountQar < 0)) errors.Add("Claimed amounts must be non-negative.");
            if (d.DisputedItems.Select(i => i.ItemId).Distinct().Count() != d.DisputedItems.Count) errors.Add("Item IDs must be unique.");
        }
        if (errors.Count > 0) throw new DossierValidationException(errors);
    }

    /// <summary>Normalizes AI- or user-supplied data: server-generated dossier id and sane defaults.</summary>
    public static DisputeDossier Normalize(DisputeDossier d, string dossierId)
    {
        var items = (d.DisputedItems ?? []).Select((i, idx) => i with
        {
            ItemId = string.IsNullOrWhiteSpace(i.ItemId) ? $"ITEM-{idx + 1:00}" : i.ItemId.Trim(),
            DescriptionAr = i.DescriptionAr ?? string.Empty,
            TaxpayerDefense = i.TaxpayerDefense ?? string.Empty,
            TaxpayerLegalReference = i.TaxpayerLegalReference ?? string.Empty,
        }).ToList();
        return d with
        {
            DossierId = dossierId,
            CommitteeRecordNumber = d.CommitteeRecordNumber ?? string.Empty,
            DisputedFiscalYear = d.DisputedFiscalYear ?? string.Empty,
            DisputedItems = items,
        };
    }
}
