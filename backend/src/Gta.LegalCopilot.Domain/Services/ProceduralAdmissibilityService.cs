using System.Globalization;
using Gta.LegalCopilot.Domain.Models;
using Gta.LegalCopilot.Domain.Statutes;

namespace Gta.LegalCopilot.Domain.Services;

public static class ProceduralViolationCodes
{
    public const string ObjectionBypassed = "ART18_ADMIN_OBJECTION_BYPASSED";
    public const string ObjectionTimeBarred = "ART18_OBJECTION_TIME_BARRED";
    public const string GrievancePremature = "ART19_GRIEVANCE_BEFORE_OBJECTION";
    public const string GrievanceTimeBarred = "ART19_GRIEVANCE_TIME_BARRED";
}

public static class PleaTypes
{
    public const string FormalInadmissibility = "PUBLIC_ORDER_FORMAL_INADMISSIBILITY";
    public const string SubstantiveOnly = "SUBSTANTIVE_REBUTTAL";
}

/// <summary>Deterministic public-order (النظام العام) admissibility screen. No LLM involvement.</summary>
public sealed class ProceduralAdmissibilityService(StatutoryDeadlineCalculator calculator)
{
    public const string InadmissibilityRuling =
        "الحكم بعدم قبول التظلم شكلاً لفوات المواعيد وتخطي مرحلة الاعتراض الإداري الوجوبية";

    public const string AdmissibleRuling =
        "قبول التظلم شكلاً، ورفضه موضوعاً وتأييد قرار الربط فيما لم تقر به الهيئة";

    private static readonly CultureInfo Ar = CultureInfo.InvariantCulture;

    public ProceduralVerdict Evaluate(DisputeDossier dossier)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        var record = dossier.DhareebaRecord;
        var noticeDate = record.AssessmentNoticeDate;
        var objectionDeadline = calculator.ObjectionDeadline(noticeDate);
        var filingDate = dossier.CommitteeFilingDate;
        var daysElapsed = StatutoryDeadlineCalculator.DaysBetween(noticeDate, filingDate);
        var objectionDate = record.AdministrativeObjectionFiled ? record.AdministrativeObjectionDate : null;

        string? violation = null;
        if (objectionDate is null)
            violation = ProceduralViolationCodes.ObjectionBypassed;
        else if (objectionDate.Value > objectionDeadline)
            violation = ProceduralViolationCodes.ObjectionTimeBarred;
        else if (filingDate < objectionDate.Value)
            violation = ProceduralViolationCodes.GrievancePremature;
        else if (filingDate > calculator.GrievanceDeadline(objectionDate.Value))
            violation = ProceduralViolationCodes.GrievanceTimeBarred;

        var admissible = violation is null;
        var basis = new List<StatutoryBasis>
        {
            LegalCorpus.Article17, LegalCorpus.Article18, LegalCorpus.Article19, LegalCorpus.CommitteeDecision,
        };

        return new ProceduralVerdict(
            IsAdmissibleFormally: admissible,
            RulingRecommendation: admissible ? AdmissibleRuling : InadmissibilityRuling,
            PrimaryPleaType: admissible ? PleaTypes.SubstantiveOnly : PleaTypes.FormalInadmissibility,
            AssessmentNoticeDate: noticeDate,
            StatutoryObjectionDeadline: objectionDeadline,
            ActualObjectionDate: objectionDate,
            GrievanceCommitteeFilingDate: filingDate,
            DaysElapsedSinceNotice: daysElapsed,
            ProceduralViolationCode: violation,
            GoverningLegalBasis: basis,
            JurisprudenceDoctrine: LegalCorpus.PublicOrderDoctrine,
            FormulatedDefenseClauseAr: FormulateClause(dossier, violation, objectionDeadline, objectionDate, daysElapsed));
    }

    private string FormulateClause(DisputeDossier d, string? violation, DateOnly objectionDeadline, DateOnly? objectionDate, int daysElapsed)
    {
        var notice = Fmt(d.DhareebaRecord.AssessmentNoticeDate);
        var filing = Fmt(d.CommitteeFilingDate);
        var deadline = Fmt(objectionDeadline);
        const string closing = " وحيث إن المواعيد الإجرائية من النظام العام تقضي بها اللجنة من تلقاء نفسها، فإن الهيئة تتمسك بالدفع بعدم قبول التظلم شكلاً.";

        return violation switch
        {
            ProceduralViolationCodes.ObjectionBypassed =>
                $"حيث إن المتظلم أُخطر بقرار الربط رقم ({d.DhareebaRecord.AssessmentNoticeRef}) بتاريخ {notice}، وكان يتعين عليه عملاً بالمادة (18) من {LegalCorpus.Law} أن يتقدم باعتراضه إلى الهيئة في موعد أقصاه {deadline}، إلا أنه لم يتقدم بأي اعتراض إداري، وبادر مباشرة بقيد تظلمه أمام اللجنة بتاريخ {filing} بعد مضي ({daysElapsed}) يوماً على الإخطار، متخطياً بذلك مرحلة الاعتراض الإداري الوجوبية التي جعلها المشرع شرطاً لازماً لقبول التظلم وفقاً للمادة (19)، فضلاً عن أن قرار الربط قد أضحى نهائياً بفوات ميعاد الاعتراض." + closing,
            ProceduralViolationCodes.ObjectionTimeBarred =>
                $"حيث إن المتظلم أُخطر بقرار الربط بتاريخ {notice}، وكان آخر ميعاد للاعتراض عملاً بالمادة (18) هو {deadline}، غير أنه لم يتقدم باعتراضه إلا بتاريخ {Fmt(objectionDate!.Value)} بعد فوات الميعاد القانوني، فيكون قرار الربط قد تحصن وأضحى نهائياً، ويكون التظلم المقيد أمام اللجنة بتاريخ {filing} قد ورد على قرار نهائي غير قابل للتظلم." + closing,
            ProceduralViolationCodes.GrievancePremature =>
                $"حيث إن المتظلم قيد تظلمه أمام اللجنة بتاريخ {filing} سابقاً على تقديم اعتراضه الإداري بتاريخ {Fmt(objectionDate!.Value)}، بما يعني أن التظلم رُفع قبل استنفاد مرحلة الاعتراض الوجوبية المنصوص عليها في المادتين (18) و(19)." + closing,
            ProceduralViolationCodes.GrievanceTimeBarred =>
                $"حيث إن المتظلم تقدم باعتراضه بتاريخ {Fmt(objectionDate!.Value)}، وكان يتعين عليه عملاً بالمادة (19) قيد تظلمه أمام اللجنة في موعد أقصاه {Fmt(calculator.GrievanceDeadline(objectionDate!.Value))}، إلا أنه لم يقيده إلا بتاريخ {filing}، بعد فوات الميعاد القانوني." + closing,
            _ =>
                $"حيث إن المتظلم أُخطر بقرار الربط بتاريخ {notice} وتقدم باعتراضه الإداري بتاريخ {Fmt(objectionDate!.Value)} خلال الميعاد المقرر بالمادة (18) والمنتهي في {deadline}، ثم قيد تظلمه أمام اللجنة بتاريخ {filing} خلال الميعاد المقرر بالمادة (19)، فإن الهيئة لا تمانع في قبول التظلم شكلاً، مع تمسكها برفضه موضوعاً للأسباب المبينة تفصيلاً.",
        };
    }

    public static string Fmt(DateOnly date) => date.ToString("yyyy/MM/dd", Ar);
}
