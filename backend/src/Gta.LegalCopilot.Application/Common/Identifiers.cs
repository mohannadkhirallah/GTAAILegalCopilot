using System.Text.RegularExpressions;

namespace Gta.LegalCopilot.Application.Common;

public static partial class Identifiers
{
    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex SafeIdRegex();

    public static bool IsSafe(string? id) => id is not null && SafeIdRegex().IsMatch(id);

    public static string NewDossierId(string prefix = "DOS") =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}
