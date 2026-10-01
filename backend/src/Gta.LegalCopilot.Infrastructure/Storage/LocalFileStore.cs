using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Application.Common;

namespace Gta.LegalCopilot.Infrastructure.Storage;

public sealed class LocalFileStore(LocalDataPaths paths) : IFileStore
{
    private static readonly HashSet<string> AllowedExtensions = [".pdf", ".docx", ".json"];

    public async Task<string> SaveUploadAsync(string dossierId, string extension, Stream content, CancellationToken ct = default)
    {
        extension = extension.ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension)) throw new ArgumentException("Unsupported file type.");
        var name = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}{extension}";
        var path = paths.Under(paths.Uploads, dossierId, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, ct);
        return name;
    }

    public async Task<string> SaveExportAsync(string dossierId, string fileName, byte[] content, CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(fileName);
        if (!Identifiers.IsSafe(Path.GetFileNameWithoutExtension(safeName))) throw new ArgumentException("Unsafe export file name.");
        var path = paths.Under(paths.Exports, dossierId, safeName);
        await LocalDataPaths.WriteAtomicAsync(path, content, ct);
        return path;
    }
}
