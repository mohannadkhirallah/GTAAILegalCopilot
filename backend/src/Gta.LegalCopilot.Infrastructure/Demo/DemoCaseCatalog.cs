using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Domain.Models;
using Gta.LegalCopilot.Domain.Services;

namespace Gta.LegalCopilot.Infrastructure.Demo;

/// <summary>
/// Three pre-bundled instant demo dossiers. All taxpayer data is fictitious and for demonstration only.
/// Statutory objection deadlines are computed by the deterministic calculator, not hard-coded.
/// </summary>
public sealed class DemoCaseCatalog(StatutoryDeadlineCalculator deadlines) : IDemoCaseCatalog
{
    public const string Siemens = "siemens";
    public const string DohaTech = "dohaTech";
    public const string Lusail = "lusail";

    private static readonly IReadOnlyList<DemoCaseInfo> Infos =
    [
        new(Siemens, "حالة «سيمنز» — عيب إجرائي", "PROCEDURAL_DEFECT",
            "تخطي المتظلم مرحلة الاعتراض الإداري الوجوبية (المادة 18) والتظلم مباشرة أمام اللجنة، مع تجاوز الغرامات حد 100%."),
        new(DohaTech, "حالة «الدوحة تك» — رد موضوعي", "SUBSTANTIVE_REBUTTAL",
            "تظلم مقبول شكلاً في مخصصات IFRS 9 وخسائر القيمة العادلة وأتعاب الإدارة."),
        new(Lusail, "حالة «لوسيل» — الاستقطاع وأسعار التحويل", "WHT_TRANSFER_PRICING",
            "منازعة في ضريبة الاستقطاع على مدفوعات لغير المقيمين وتعديلات أسعار التحويل."),
    ];

    public IReadOnlyList<DemoCaseInfo> List() => Infos;

    public DisputeDossier? Get(string key) => key.ToLowerInvariant() switch
    {
        "siemens" => BuildSiemens(),
        "dohatech" => BuildDohaTech(),
        "lusail" => BuildLusail(),
        _ => null,
    };

    private DhareebaRecord Record(string noticeRef, DateOnly notice, DateOnly? objection, decimal tax, decimal penalties) =>
        new(noticeRef, notice, deadlines.ObjectionDeadline(notice), objection is not null, objection, tax, penalties, tax + penalties);

    private DisputeDossier BuildSiemens() => new(
        "DEMO-SIEMENS", "TGC/2024/0187", new DateOnly(2024, 5, 20), "2018",
        new TaxpayerProfile("الشركة الهندسية للأنظمة الصناعية — فرع قطر (بيانات افتراضية)", "Industrial Systems Engineering – Qatar Branch (Demo)",
            "5000123456", "54321", "فرع شركة أجنبية", "توريد وتركيب الأنظمة الكهربائية والصناعية"),
        Record("GTA/IT/ASM/2024/00931", new DateOnly(2024, 3, 10), null, 2_450_000m, 2_572_500m),
        [
            new("ITEM-01", "أتعاب إدارة وخدمات فنية محملة من المركز الرئيسي", 12_000_000m,
                "الأتعاب تمثل خدمات فعلية مقدمة من المركز الرئيسي وتعد من التكاليف اللازمة لتحقيق الدخل.", "المادة (33) من القانون", false),
            new("ITEM-02", "مخصص خسائر ائتمانية متوقعة وفق المعيار IFRS 9", 8_500_000m,
                "المخصص مكون وفقاً للمعايير الدولية للتقارير المالية ويعكس مخاطر ائتمانية حقيقية.", "المعيار الدولي IFRS 9", true),
            new("ITEM-03", "خسائر إعادة تقييم استثمارات بالقيمة العادلة", 4_000_000m,
                "الخسائر ناتجة عن انخفاض القيمة السوقية للاستثمارات في تاريخ الميزانية.", "المادة (9) من القانون", false),
        ]);

    private DisputeDossier BuildDohaTech() => new(
        "DEMO-DOHATECH", "TGC/2024/0112", new DateOnly(2024, 5, 5), "2022",
        new TaxpayerProfile("شركة الدوحة للحلول التقنية ذ.م.م (بيانات افتراضية)", "Doha Tech Solutions LLC (Demo)",
            "5000654321", "112233", "شركة ذات مسؤولية محدودة", "تقنية المعلومات والحلول الرقمية"),
        Record("GTA/IT/ASM/2024/00412", new DateOnly(2024, 2, 1), new DateOnly(2024, 2, 20), 1_400_000m, 252_000m),
        [
            new("ITEM-01", "مخصص خسائر ائتمانية متوقعة على الذمم المدينة (IFRS 9)", 6_000_000m,
                "المخصص إلزامي محاسبياً ويجب قبوله ضريبياً لتطابق القوائم المالية المدققة.", "المادة (8) من القانون والمعيار IFRS 9", true),
            new("ITEM-02", "خسائر غير محققة من تقييم محفظة أوراق مالية بالقيمة العادلة", 3_000_000m,
                "الخسائر معترف بها في قائمة الدخل وفقاً للمعايير المحاسبية.", "المادة (9) من القانون", true),
            new("ITEM-03", "أتعاب إدارة مدفوعة للشركة الأم", 5_000_000m,
                "الأتعاب مقابل خدمات إدارية ومالية فعلية مؤيدة بعقد واتفاقية توزيع تكاليف.", "المادة (33) من القانون", true),
        ]);

    private DisputeDossier BuildLusail() => new(
        "DEMO-LUSAIL", "TGC/2024/0263", new DateOnly(2024, 9, 15), "2021",
        new TaxpayerProfile("شركة لوسيل للتطوير والمقاولات ش.م.خ (بيانات افتراضية)", "Lusail Development & Contracting QPSC (Demo)",
            "5000987654", "998877", "شركة مساهمة خاصة", "التطوير العقاري والمقاولات العامة"),
        Record("GTA/IT/ASM/2024/01577", new DateOnly(2024, 6, 1), new DateOnly(2024, 6, 25), 1_500_000m, 540_000m),
        [
            new("ITEM-01", "ضريبة استقطاع على أتعاب خدمات فنية لمستشار غير مقيم (مبلغ الضريبة)", 420_000m,
                "الخدمات أُديت بالكامل خارج دولة قطر فلا تخضع للاستقطاع.", "المادة (20) من القانون", true),
            new("ITEM-02", "ضريبة استقطاع على إتاوات برمجيات لمورد غير مقيم (مبلغ الضريبة)", 180_000m,
                "المدفوعات مقابل شراء برمجيات جاهزة وليست إتاوات.", "المادة (20) من القانون", false),
            new("ITEM-03", "تعديل أسعار التحويل على مشتريات من أطراف مرتبطة", 9_000_000m,
                "الأسعار متفقة مع السعر المحايد وفق دراسة أسعار التحويل المرفقة.", "المادة (33) مكرراً من القانون", true),
        ]);
}
