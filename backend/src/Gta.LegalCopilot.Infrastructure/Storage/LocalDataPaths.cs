using Gta.LegalCopilot.Application.Common;
using Microsoft.Extensions.Options;

namespace Gta.LegalCopilot.Infrastructure.Storage;

/// <summary>Resolves safe paths under the local ./data root (dossiers, uploads, exports, memos).</summary>
public sealed class LocalDataPaths
{
    public string Root { get; }

    public LocalDataPaths(IOptions<StorageOptions> options)
    {
        Root = Path.GetFullPath(options.Value.RootPath);
        foreach (var dir in new[] { Dossiers, Uploads, Exports, Memos })
            Directory.CreateDirectory(dir);
    }

    public string Dossiers => Path.Combine(Root, "dossiers");
    public string Uploads => Path.Combine(Root, "uploads");
    public string Exports => Path.Combine(Root, "exports");
    public string Memos => Path.Combine(Root, "memos");

    public string Under(string baseDir, string id, string? fileName = null)
    {
        if (!Identifiers.IsSafe(id)) throw new ArgumentException("Unsafe identifier.", nameof(id));
        var path = Path.GetFullPath(fileName is null ? Path.Combine(baseDir, id) : Path.Combine(baseDir, id, fileName));
        if (!path.StartsWith(Path.GetFullPath(baseDir) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Path escapes data root.");
        return path;
    }

    public static async Task WriteAtomicAsync(string path, byte[] content, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllBytesAsync(tmp, content, ct);
        File.Move(tmp, path, overwrite: true);
    }
}
