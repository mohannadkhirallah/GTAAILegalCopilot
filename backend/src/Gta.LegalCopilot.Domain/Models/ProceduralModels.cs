namespace Gta.LegalCopilot.Domain.Models;

public record StatutoryBasis(string Source, string Article, string RuleSummary);
public record JurisprudenceDoctrine(string Court, string PrincipleAr);

public record ProceduralVerdict(
    bool IsAdmissibleFormally, string RulingRecommendation, string PrimaryPleaType,
    DateOnly AssessmentNoticeDate, DateOnly StatutoryObjectionDeadline, DateOnly? ActualObjectionDate,
    DateOnly GrievanceCommitteeFilingDate, int DaysElapsedSinceNotice, string? ProceduralViolationCode,
    IReadOnlyList<StatutoryBasis> GoverningLegalBasis, JurisprudenceDoctrine JurisprudenceDoctrine,
    string FormulatedDefenseClauseAr
);
