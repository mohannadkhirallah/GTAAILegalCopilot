using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Gta.LegalCopilot.Application.Services;

/// <summary>
/// Enforces deterministic-LLM isolation: any number appearing in LLM prose must already exist
/// in the deterministic draft it was asked to refine. Otherwise the draft is used verbatim.
/// </summary>
public static partial class NumericIntegrityGuard
{
    [GeneratedRegex(@"\d[\d,]*(?:\.\d+)?")]
    private static partial Regex NumberRegex();

    public static string NormalizeDigits(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            sb.Append(ch switch
            {
                >= '\u0660' and <= '\u0669' => (char)('0' + (ch - '\u0660')),
                >= '\u06F0' and <= '\u06F9' => (char)('0' + (ch - '\u06F0')),
                '\u066B' => '.',
                '\u066C' => ',',
                _ => ch,
            });
        }
        return sb.ToString();
    }

    public static HashSet<decimal> ExtractNumbers(string text)
    {
        var set = new HashSet<decimal>();
        foreach (Match m in NumberRegex().Matches(NormalizeDigits(text)))
        {
            var raw = m.Value.TrimEnd(',').Replace(",", string.Empty);
            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
                set.Add(v);
        }
        return set;
    }

    public static bool IsFaithful(string deterministicDraft, string llmText, out IReadOnlyList<decimal> foreignNumbers)
    {
        var allowed = ExtractNumbers(deterministicDraft);
        var found = ExtractNumbers(llmText);
        foreignNumbers = found.Where(n => !allowed.Contains(n)).ToList();
        return foreignNumbers.Count == 0 && !string.IsNullOrWhiteSpace(llmText);
    }
}
