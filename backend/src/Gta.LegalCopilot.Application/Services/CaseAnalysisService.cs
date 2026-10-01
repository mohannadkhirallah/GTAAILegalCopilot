using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Domain.Models;
using Gta.LegalCopilot.Domain.Services;

namespace Gta.LegalCopilot.Application.Services;

/// <summary>Runs the three deterministic engines (procedural → substantive → financial).</summary>
public sealed class CaseAnalysisService(
    ProceduralAdmissibilityService procedural,
    SubstantiveRuleEngine substantive,
    FinancialRecalculationService financial)
{
    public CaseAnalysis Analyze(DisputeDossier dossier)
    {
        DossierValidator.EnsureValid(dossier);
        var verdict = procedural.Evaluate(dossier);
        var (summary, outcomes) = substantive.Evaluate(dossier, verdict);
        var money = financial.Recalculate(dossier, verdict, outcomes);
        return new CaseAnalysis(dossier.DossierId, verdict, summary, money,
            outcomes.Select(o => new ItemCategoryInfo(o.Evaluation.ItemId, o.Category)).ToList());
    }
}
