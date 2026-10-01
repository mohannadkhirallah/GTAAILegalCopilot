namespace Gta.LegalCopilot.Domain.Statutes;

/// <summary>
/// Deterministic statutory constants of Qatar Income Tax Law No. (24) of 2018 and its Executive Regulations.
/// All values are calendar-day / percentage parameters consumed exclusively by C# domain services (never by the LLM).
/// </summary>
public sealed record StatutoryParameters
{
    /// <summary>Art. (18): objection to the GTA within 30 days from notification of the assessment.</summary>
    public int ObjectionWindowDays { get; init; } = 30;

    /// <summary>Art. (18)/(19): the GTA decides the objection within 60 days; silence after that period is an implicit rejection.</summary>
    public int AuthorityDecisionWindowDays { get; init; } = 60;

    /// <summary>Art. (19): grievance to the Tax Grievance Committee within 30 days of the decision or of the lapse of the decision window.</summary>
    public int GrievanceWindowDays { get; init; } = 30;

    /// <summary>Art. (24): late-payment penalty of 1.5% of unpaid tax per month or part thereof.</summary>
    public decimal MonthlyDelayPenaltyRate { get; init; } = 0.015m;

    /// <summary>Art. (24): the delay penalty may not exceed 100% of the unpaid principal tax.</summary>
    public decimal DelayPenaltyCapRatio { get; init; } = 1.00m;

    /// <summary>Standard corporate income tax rate.</summary>
    public decimal CorporateTaxRate { get; init; } = 0.10m;

    public static StatutoryParameters Default { get; } = new();
}
