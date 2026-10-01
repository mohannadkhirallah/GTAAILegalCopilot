using System.Text;
using DocumentFormat.OpenXml.Packaging;
using Gta.LegalCopilot.Application.Abstractions;
using UglyToad.PdfPig;

namespace Gta.LegalCopilot.Infrastructure.Documents;

/// <summary>Extracts raw text from PDF (PdfPig) and DOCX (OpenXml) petitions.</summary>
public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    public const int MaxCharacters = 120_000;

    public bool Supports(string extension) => extension.ToLowerInvariant() is ".pdf" or ".docx";

    public async Task<string> ExtractTextAsync(Stream content, string extension, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        var text = extension.ToLowerInvariant() switch
        {
            ".pdf" => ExtractPdf(buffer),
            ".docx" => ExtractDocx(buffer),
            _ => throw new NotSupportedException($"Unsupported extension {extension}"),
        };
        return text.Length > MaxCharacters ? text[..MaxCharacters] : text;
    }

    private static string ExtractPdf(Stream s)
    {
        using var pdf = PdfDocument.Open(s);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            sb.AppendLine($"--- صفحة {page.Number} ---");
            sb.AppendLine(string.Join(' ', page.GetWords().Select(w => w.Text)));
            if (sb.Length > MaxCharacters) break;
        }
        return sb.ToString();
    }

    private static string ExtractDocx(Stream s)
    {
        using var doc = WordprocessingDocument.Open(s, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;
        var sb = new StringBuilder();
        foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
            sb.AppendLine(p.InnerText);
        return sb.ToString();
    }

    /// <summary>Magic-byte sanity check so renamed files are rejected.</summary>
    public static bool HasValidSignature(ReadOnlySpan<byte> header, string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => header.Length >= 4 && header[..4].SequenceEqual("%PDF"u8),
        ".docx" => header.Length >= 4 && header[..4].SequenceEqual("PK\u0003\u0004"u8),
        ".json" => true,
        _ => false,
    };
}
