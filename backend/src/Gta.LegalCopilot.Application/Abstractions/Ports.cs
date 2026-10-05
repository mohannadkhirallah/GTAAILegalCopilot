using Gta.LegalCopilot.Domain.Models;
using Gta.LegalCopilot.Domain.Services;

namespace Gta.LegalCopilot.Application.Abstractions;

public record DossierSummary(string DossierId, string CommitteeRecordNumber, string TaxpayerNameAr, string DisputedFiscalYear, DateOnly CommitteeFilingDate, string Source);

public record StoredDossier(DisputeDossier Dossier, string Source, DateTimeOffset CreatedAtUtc,
    IReadOnlyList<string> SourceFiles, string? ExtractedDocumentText = null);

public interface IDossierRepository
{
    Task SaveAsync(StoredDossier dossier, CancellationToken ct = default);
    Task<StoredDossier?> GetAsync(string dossierId, CancellationToken ct = default);
    Task<IReadOnlyList<DossierSummary>> ListAsync(CancellationToken ct = default);
    Task SaveMemoAsync(AssembledMemo memo, string dossierId, CancellationToken ct = default);
    Task<AssembledMemo?> GetMemoAsync(string dossierId, CancellationToken ct = default);
}

public interface IFileStore
{
    Task<string> SaveUploadAsync(string dossierId, string extension, Stream content, CancellationToken ct = default);
    Task<string> SaveExportAsync(string dossierId, string fileName, byte[] content, CancellationToken ct = default);
}

public interface IDocumentTextExtractor
{
    bool Supports(string extension);
    Task<string> ExtractTextAsync(Stream content, string extension, CancellationToken ct = default);
}

/// <summary>AI-assisted extraction of a structured dossier from free text (extraction only — no calculation).</summary>
public interface IDossierExtractor
{
    bool IsEnabled { get; }
    Task<DisputeDossier> ExtractAsync(string documentText, CancellationToken ct = default);
}

/// <summary>LLM narrative polishing of deterministic drafts. Never a source of numbers, dates or determinations.</summary>
public interface ILegalNarrativeGenerator
{
    bool IsEnabled { get; }
    IAsyncEnumerable<string> StreamRefinedSectionAsync(string sectionKey, string deterministicDraft, CancellationToken ct = default);
}

public interface IMemoDocumentRenderer
{
    byte[] Render(AssembledMemo memo);
}

public record DemoCaseInfo(string Key, string TitleAr, string Scenario, string DescriptionAr);

public interface IDemoCaseCatalog
{
    IReadOnlyList<DemoCaseInfo> List();
    DisputeDossier? Get(string key);
}

public record CaseAnalysis(
    string DossierId, ProceduralVerdict Procedural, SubstantiveAuditSummary Substantive,
    FinancialRecalculationResult Financial, IReadOnlyList<ItemCategoryInfo> ItemCategories);

public record ItemCategoryInfo(string ItemId, ItemCategory Category);
