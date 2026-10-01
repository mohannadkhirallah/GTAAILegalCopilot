using Gta.LegalCopilot.Application.Services;
using Gta.LegalCopilot.Domain.Models;
using Gta.LegalCopilot.Domain.Services;
using Gta.LegalCopilot.Domain.Statutes;
using Gta.LegalCopilot.Infrastructure.Demo;

namespace Gta.LegalCopilot.Tests;

public class DomainTests
{
    private static readonly StatutoryParameters P = StatutoryParameters.Default;
    private static readonly StatutoryDeadlineCalculator Calc = new(P);
    private static readonly ProceduralAdmissibilityService Procedural = new(Calc);
    private static readonly DelayPenaltyCalculator Penalties = new(P);
    private static readonly SubstantiveRuleEngine Rules = new(SubstantivePolicy.Default);
    private static readonly FinancialRecalculationService Financial = new(P, Penalties);
    private static readonly DemoCaseCatalog Demos = new(Calc);

    private static DisputeDossier Dossier(DateOnly notice, DateOnly? objection, DateOnly filing, decimal tax = 1000m, decimal penalty = 100m, params DisputedItem[] items) =>
        new("T-1", "REC", filing, "2023",
            new TaxpayerProfile("مكلف", null, "1", "1", "ذ.م.م", "تجارة"),
            new DhareebaRecord("N-1", notice, Calc.ObjectionDeadline(notice), objection is not null, objection, tax, penalty, tax + penalty),
            items);

    [Fact]
    public void ObjectionDeadline_Is30CalendarDays() =>
        Assert.Equal(new DateOnly(2024, 3, 1), Calc.ObjectionDeadline(new DateOnly(2024, 1, 31)));

    [Fact]
    public void GrievanceDeadline_IsDecisionWindowPlus30() =>
        Assert.Equal(new DateOnly(2024, 1, 1).AddDays(90), Calc.GrievanceDeadline(new DateOnly(2024, 1, 1)));

    [Fact]
    public void BypassedObjection_IsInadmissible_WithPublicOrderPlea()
    {
        var v = Procedural.Evaluate(Dossier(new DateOnly(2024, 3, 10), null, new DateOnly(2024, 5, 20)));
        Assert.False(v.IsAdmissibleFormally);
        Assert.Equal(ProceduralViolationCodes.ObjectionBypassed, v.ProceduralViolationCode);
        Assert.Equal("الحكم بعدم قبول التظلم شكلاً لفوات المواعيد وتخطي مرحلة الاعتراض الإداري الوجوبية", v.RulingRecommendation);
        Assert.Equal(71, v.DaysElapsedSinceNotice);
        Assert.Equal(new DateOnly(2024, 4, 9), v.StatutoryObjectionDeadline);
    }

    [Theory]
    [InlineData(30, true)]   // last day is still within the window
    [InlineData(31, false)]  // one day late
    public void ObjectionOnBoundary(int daysAfterNotice, bool admissible)
    {
        var notice = new DateOnly(2024, 1, 1);
        var objection = notice.AddDays(daysAfterNotice);
        var v = Procedural.Evaluate(Dossier(notice, objection, objection.AddDays(10)));
        Assert.Equal(admissible, v.IsAdmissibleFormally);
        if (!admissible) Assert.Equal(ProceduralViolationCodes.ObjectionTimeBarred, v.ProceduralViolationCode);
    }

    [Fact]
    public void LateGrievance_IsTimeBarred()
    {
        var notice = new DateOnly(2024, 1, 1);
        var objection = notice.AddDays(5);
        var v = Procedural.Evaluate(Dossier(notice, objection, objection.AddDays(91)));
        Assert.Equal(ProceduralViolationCodes.GrievanceTimeBarred, v.ProceduralViolationCode);
    }

    [Fact]
    public void GrievanceBeforeObjection_IsPremature()
    {
        var notice = new DateOnly(2024, 1, 1);
        var v = Procedural.Evaluate(Dossier(notice, notice.AddDays(20), notice.AddDays(10)));
        Assert.Equal(ProceduralViolationCodes.GrievancePremature, v.ProceduralViolationCode);
    }

    [Theory]
    [InlineData("2024-01-15", "2024-01-15", 0)]
    [InlineData("2024-01-15", "2024-01-16", 1)]
    [InlineData("2024-01-15", "2024-02-15", 1)]
    [InlineData("2024-01-15", "2024-02-16", 2)]
    [InlineData("2024-01-31", "2024-02-29", 1)]
    public void MonthsOrPartThereof(string due, string asOf, int expected) =>
        Assert.Equal(expected, DelayPenaltyCalculator.MonthsOrPartThereof(DateOnly.Parse(due), DateOnly.Parse(asOf)));

    [Fact]
    public void Penalty_Is1_5PercentPerMonth()
    {
        var r = Penalties.Compute(100_000m, new DateOnly(2024, 1, 1), new DateOnly(2024, 4, 1));
        Assert.Equal(4_500m, r.Penalty);
        Assert.False(r.IsCapped);
    }

    [Fact]
    public void Penalty_IsCappedAt100PercentOfPrincipal()
    {
        var r = Penalties.Compute(100_000m, new DateOnly(2015, 1, 1), new DateOnly(2024, 1, 1));
        Assert.Equal(100_000m, r.Penalty);
        Assert.True(r.IsCapped);
    }

    [Theory]
    [InlineData("مخصص خسائر ائتمانية متوقعة", ItemCategory.ExpectedCreditLossProvision)]
    [InlineData("خسائر القيمة العادلة", ItemCategory.FairValueLoss)]
    [InlineData("أتعاب إدارة للشركة الأم", ItemCategory.ManagementFees)]
    [InlineData("ضريبة استقطاع", ItemCategory.WithholdingTax)]
    [InlineData("تعديل أسعار التحويل", ItemCategory.TransferPricing)]
    [InlineData("مصاريف ضيافة", ItemCategory.Other)]
    public void Classifier(string description, ItemCategory expected) =>
        Assert.Equal(expected, SubstantiveRuleEngine.Classify(new DisputedItem("I", description, 1, "", "", false)));

    [Fact]
    public void Financial_RevisesTaxAndPenalty_Proportionally()
    {
        var d = Dossier(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 10), new DateOnly(2024, 2, 1), 1_000_000m, 180_000m,
            new DisputedItem("A", "أتعاب إدارة", 2_000_000m, "", "", true));
        var v = Procedural.Evaluate(d);
        var (summary, outcomes) = Rules.Evaluate(d, v);
        var f = Financial.Recalculate(d, v, outcomes);
        Assert.Equal(1_000_000m, summary.TotalConcessionsAdmittedQar);
        Assert.Equal(100_000m, f.TaxReliefQar);
        Assert.Equal(900_000m, f.RevisedTaxDiffQar);
        Assert.Equal(162_000m, f.RevisedDelayPenaltiesQar);
        Assert.Equal(1_062_000m, f.FinalTreasuryReceivableQar);
    }

    [Fact]
    public void Financial_ReliefNeverExceedsOriginalTax()
    {
        var d = Dossier(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 10), new DateOnly(2024, 2, 1), 10m, 1m,
            new DisputedItem("A", "ضريبة استقطاع", 1_000m, "", "", true));
        var v = Procedural.Evaluate(d);
        var (_, outcomes) = Rules.Evaluate(d, v);
        var f = Financial.Recalculate(d, v, outcomes);
        Assert.Equal(0m, f.RevisedTaxDiffQar);
        Assert.Equal(0m, f.RevisedDelayPenaltiesQar);
    }

    [Fact]
    public void SiemensDemo_PrimaryPleaAndCappedPenalty()
    {
        var d = Demos.Get("siemens")!;
        var v = Procedural.Evaluate(d);
        var (summary, outcomes) = Rules.Evaluate(d, v);
        var f = Financial.Recalculate(d, v, outcomes);
        Assert.False(v.IsAdmissibleFormally);
        Assert.Equal(DefensePostures.AlternativeReserve, summary.DefensePosture);
        Assert.Equal(ComputationModes.AlternativeReserve, f.ComputationMode);
        Assert.True(f.IsPenaltyCapped);
        Assert.Equal(2_450_000m, f.RevisedDelayPenaltiesQar);
        Assert.Equal(4_900_000m, f.FinalTreasuryReceivableQar);
    }

    [Theory]
    [InlineData("dohaTech", 1_357_000)]
    [InlineData("lusail", 856_800)]
    public void SubstantiveDemos_AreAdmissible(string key, decimal expectedTotal)
    {
        var d = Demos.Get(key)!;
        var v = Procedural.Evaluate(d);
        var (_, outcomes) = Rules.Evaluate(d, v);
        var f = Financial.Recalculate(d, v, outcomes);
        Assert.True(v.IsAdmissibleFormally);
        Assert.Equal(expectedTotal, f.RevisedTotalDueQar);
    }

    [Fact]
    public void NumericGuard_RejectsInventedNumbers()
    {
        const string draft = "فرق ضريبة قدره 1,400,000.00 ريال بتاريخ 2024/02/01 وفق المادة (18).";
        Assert.True(NumericIntegrityGuard.IsFaithful(draft, "بمبلغ ١٬٤٠٠٬٠٠٠ ريال في 2024/02/01 طبقاً للمادة 18", out _));
        Assert.False(NumericIntegrityGuard.IsFaithful(draft, "بمبلغ 1,500,000 ريال", out var foreign));
        Assert.Contains(1_500_000m, foreign);
    }
}
