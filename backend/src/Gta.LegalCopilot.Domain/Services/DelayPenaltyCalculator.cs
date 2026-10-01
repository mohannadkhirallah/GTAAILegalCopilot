using Gta.LegalCopilot.Domain.Statutes;

namespace Gta.LegalCopilot.Domain.Services;

public readonly record struct PenaltyComputation(decimal Penalty, decimal Cap, bool IsCapped);

/// <summary>Art. (24) late-payment penalty: 1.5% per month or part thereof, capped at 100% of principal.</summary>
public sealed class DelayPenaltyCalculator(StatutoryParameters parameters)
{
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Number of months or part thereof between the due date and the computation date.</summary>
    public static int MonthsOrPartThereof(DateOnly dueDate, DateOnly asOf)
    {
        if (asOf <= dueDate) return 0;
        var months = (asOf.Year - dueDate.Year) * 12 + asOf.Month - dueDate.Month;
        if (dueDate.AddMonths(months) < asOf) months++;
        return months;
    }

    public decimal CapFor(decimal principal) => Round(Math.Max(0, principal) * parameters.DelayPenaltyCapRatio);

    public PenaltyComputation Apply(decimal principal, decimal rawPenalty)
    {
        var cap = CapFor(principal);
        var penalty = Round(Math.Max(0, rawPenalty));
        return penalty > cap ? new PenaltyComputation(cap, cap, true) : new PenaltyComputation(penalty, cap, false);
    }

    public PenaltyComputation Compute(decimal principal, DateOnly dueDate, DateOnly asOf)
    {
        var months = MonthsOrPartThereof(dueDate, asOf);
        return Apply(principal, Math.Max(0, principal) * parameters.MonthlyDelayPenaltyRate * months);
    }
}
