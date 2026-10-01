using Gta.LegalCopilot.Domain.Models;

namespace Gta.LegalCopilot.Domain.Statutes;

/// <summary>Static, curated references used to ground the memo. The LLM never invents legal sources.</summary>
public static class LegalCorpus
{
    public const string Law = "قانون ضريبة الدخل رقم (24) لسنة 2018";
    public const string Regulations = "اللائحة التنفيذية لقانون ضريبة الدخل الصادرة بقرار وزير المالية رقم (39) لسنة 2019";
    public const string CabinetDecision = "قرار مجلس الوزراء رقم (38) لسنة 2020";

    public static readonly StatutoryBasis Article17 = new(Law, "المادة (17)",
        "تختص الهيئة بربط الضريبة وإخطار المكلف بقرار الربط بأي وسيلة تفيد العلم.");
    public static readonly StatutoryBasis Article18 = new(Law, "المادة (18)",
        "للمكلف الاعتراض على قرار الربط أمام الهيئة خلال ثلاثين يوماً من تاريخ إخطاره، وإلا أصبح الربط نهائياً.");
    public static readonly StatutoryBasis Article19 = new(Law, "المادة (19)",
        "للمكلف التظلم أمام لجنة التظلم الضريبي خلال ثلاثين يوماً من تاريخ إخطاره بقرار الهيئة في الاعتراض أو من انقضاء ستين يوماً دون البت فيه.");
    public static readonly StatutoryBasis Article24 = new(Law, "المادة (24)",
        "غرامة تأخير بواقع 1.5% من الضريبة غير المسددة عن كل شهر أو جزء منه، بما لا يجاوز أصل الضريبة.");
    public static readonly StatutoryBasis CommitteeDecision = new(CabinetDecision, "قرار التشكيل",
        "تنظيم لجنة التظلم الضريبي وإجراءات نظر التظلمات أمامها.");

    public static readonly JurisprudenceDoctrine PublicOrderDoctrine = new(
        "محكمة التمييز القطرية — الدائرة الإدارية",
        "المواعيد والإجراءات المقررة للطعن والتظلم من النظام العام، تقضي بها جهة الفصل من تلقاء نفسها، ولا يجوز تخطي مرحلة التظلم أو الاعتراض الإداري الوجوبي التي رسمها المشرع قبل اللجوء إلى جهة الفصل.");
}
