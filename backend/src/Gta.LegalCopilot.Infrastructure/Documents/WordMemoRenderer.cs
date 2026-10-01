using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Application.Services;
using Gta.LegalCopilot.Domain.Models;

namespace Gta.LegalCopilot.Infrastructure.Documents;

/// <summary>Renders the assembled memo as an Arabic RTL Word document (DocumentFormat.OpenXml).</summary>
public sealed class WordMemoRenderer : IMemoDocumentRenderer
{
    private const string Font = "Simplified Arabic";

    public byte[] Render(AssembledMemo memo)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            var body = new Body();
            main.Document = new Document(body);
            AddDefaultStyles(main);

            var m = memo.Metadata;
            body.Append(Para(m.State, bold: true, size: 28));
            body.Append(Para(m.Authority, bold: true, size: 28));
            body.Append(Para(m.Department, bold: true, size: 28));
            body.Append(Para($"المرجع: {m.ReferenceNumber}   —   تاريخ قيد التظلم: {m.FilingDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)}", size: 24));
            body.Append(Para(string.Empty));
            body.Append(Para(memo.DocumentType, bold: true, size: 34, center: true, underline: true));
            body.Append(Para(string.Empty));

            foreach (var key in MemoSections.Ordered)
            {
                if (!memo.Sections.TryGetValue(key, out var text)) continue;
                if (key != MemoSections.Preamble)
                    body.Append(Para(MemoSections.TitlesAr[key], bold: true, size: 30, underline: true));
                foreach (var line in text.Split('\n'))
                {
                    var t = line.TrimEnd('\r');
                    var isAddressee = t.Trim() == m.Addressee;
                    var isTitle = key == MemoSections.Preamble && t.Trim() == memo.DocumentType;
                    if (isTitle) continue;
                    body.Append(Para(t, bold: isAddressee, center: isAddressee, justify: !isAddressee));
                }
                if (key == MemoSections.SubstantiveDefense) body.Append(ItemsTable(memo.Substantive));
                if (key == MemoSections.FinancialImpact) body.Append(LedgerTable(memo.Financial));
                body.Append(Para(string.Empty));
            }

            body.Append(Para("وتفضلوا بقبول فائق الاحترام والتقدير،،،", bold: true, center: true));
            body.Append(Para(string.Empty));
            body.Append(Para("عن الهيئة العامة للضرائب", bold: true));
            body.Append(Para("الإدارة القانونية — إدارة ضريبة الدخل", bold: true));
            body.Append(SectionProps());
            main.Document.Save();
        }
        return ms.ToArray();
    }

    private static void AddDefaultStyles(MainDocumentPart main)
    {
        var stylesPart = main.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = new Styles(new DocDefaults(
            new RunPropertiesDefault(new RunPropertiesBaseStyle(
                new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font, EastAsia = Font },
                new FontSize { Val = "26" }, new FontSizeComplexScript { Val = "26" },
                new Languages { Bidi = "ar-QA" })),
            new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                new BiDi(), new SpacingBetweenLines { After = "120", Line = "300", LineRule = LineSpacingRuleValues.Auto }))));
    }

    private static Paragraph Para(string text, bool bold = false, int size = 26, bool center = false, bool underline = false, bool justify = false)
    {
        var pPr = new ParagraphProperties(new BiDi());
        if (center) pPr.Append(new Justification { Val = JustificationValues.Center });
        else if (justify) pPr.Append(new Justification { Val = JustificationValues.Both });
        // Child order follows the CT_RPr schema sequence (rFonts, b, bCs, sz, szCs, u, rtl).
        var rPr = new RunProperties(new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font });
        if (bold) { rPr.Append(new Bold()); rPr.Append(new BoldComplexScript()); }
        rPr.Append(new FontSize { Val = size.ToString(CultureInfo.InvariantCulture) });
        rPr.Append(new FontSizeComplexScript { Val = size.ToString(CultureInfo.InvariantCulture) });
        if (underline) rPr.Append(new Underline { Val = UnderlineValues.Single });
        rPr.Append(new RightToLeftText());
        return new Paragraph(pPr, new Run(rPr, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Table ItemsTable(SubstantiveAuditSummary s)
    {
        var rows = s.LineItemsEvaluation.Select((e, i) => new[]
        {
            (i + 1).ToString(CultureInfo.InvariantCulture), e.LineName, Money(e.ClaimedAmountQar),
            MemoComposer.DeterminationAr(e.GtaDetermination), Money(e.AdmittedDeductionQar), e.StatutoryReference,
        }).ToList();
        rows.Add(["", "الإجمالي", Money(s.TotalDisputedClaimedQar), "", Money(s.TotalConcessionsAdmittedQar), ""]);
        return Table(["م", "البند", "المبلغ محل النزاع", "قرار الهيئة", "المقر به", "السند"], rows);
    }

    private static Table LedgerTable(FinancialRecalculationResult f) =>
        Table(["البيان", "الربط الأصلي", "المقترح", "الفرق", "السند"],
            f.LedgerComparisonMatrix.Select(l => new[]
            {
                l.LedgerEntryName, Money(l.OriginalAssessmentQar), Money(l.SettlementProposalQar), Money(l.VarianceQar), l.LegalBasis,
            }).ToList());

    private static string Money(decimal v) => v.ToString("N2", CultureInfo.InvariantCulture);

    private static Table Table(string[] headers, IEnumerable<string[]> rows)
    {
        var border = () => new BorderType[]
        {
            new TopBorder { Val = BorderValues.Single, Size = 6 }, new LeftBorder { Val = BorderValues.Single, Size = 6 },
            new BottomBorder { Val = BorderValues.Single, Size = 6 }, new RightBorder { Val = BorderValues.Single, Size = 6 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 }, new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 },
        };
        var table = new Table(new TableProperties(
            new BiDiVisual(),
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(border())));
        table.Append(new TableGrid(headers.Select(_ => new GridColumn { Width = (9638 / headers.Length).ToString(CultureInfo.InvariantCulture) })));
        table.Append(Row(headers, header: true));
        foreach (var r in rows) table.Append(Row(r, header: false));
        return table;
    }

    private static TableRow Row(string[] cells, bool header)
    {
        var row = new TableRow();
        foreach (var c in cells)
        {
            var cell = new TableCell(Para(c, bold: header, size: 22, center: true));
            if (header) cell.PrependChild(new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = "D9E2F3", Color = "auto" }));
            row.Append(cell);
        }
        return row;
    }

    private static SectionProperties SectionProps() => new(
        new PageSize { Width = 11906, Height = 16838 },
        new PageMargin { Top = 1134, Bottom = 1134, Left = 1134, Right = 1134, Header = 709, Footer = 709 },
        new BiDi());
}
