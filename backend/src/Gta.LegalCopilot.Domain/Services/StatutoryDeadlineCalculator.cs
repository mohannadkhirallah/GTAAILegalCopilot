using Gta.LegalCopilot.Domain.Statutes;

namespace Gta.LegalCopilot.Domain.Services;

/// <summary>Pure calendar-day arithmetic for Articles (17), (18) and (19).</summary>
public sealed class StatutoryDeadlineCalculator(StatutoryParameters parameters)
{
    public StatutoryParameters Parameters { get; } = parameters;

    /// <summary>Art. (18): last day to object before the GTA.</summary>
    public DateOnly ObjectionDeadline(DateOnly assessmentNoticeDate) =>
        assessmentNoticeDate.AddDays(Parameters.ObjectionWindowDays);

    /// <summary>Art. (18)/(19): day on which GTA silence becomes an implicit rejection.</summary>
    public DateOnly AuthorityDecisionDeadline(DateOnly objectionDate) =>
        objectionDate.AddDays(Parameters.AuthorityDecisionWindowDays);

    /// <summary>
    /// Art. (19): latest admissible committee filing date. Computed from the lapse of the GTA decision window
    /// (the date most favourable to the taxpayer when no explicit decision notice is on file).
    /// </summary>
    public DateOnly GrievanceDeadline(DateOnly objectionDate) =>
        AuthorityDecisionDeadline(objectionDate).AddDays(Parameters.GrievanceWindowDays);

    public static int DaysBetween(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber;
}
