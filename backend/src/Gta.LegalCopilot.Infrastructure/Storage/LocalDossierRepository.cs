using System.Collections.Concurrent;
using System.Text.Json;
using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Application.Common;
using Gta.LegalCopilot.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Gta.LegalCopilot.Infrastructure.Storage;

/// <summary>JSON-file persistence under ./data with an IMemoryCache read-through layer.</summary>
public sealed class LocalDossierRepository(LocalDataPaths paths, IMemoryCache cache, ILogger<LocalDossierRepository> logger) : IDossierRepository
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    private string DossierPath(string id) => paths.Under(paths.Dossiers, id) + ".json";
    private string MemoPath(string id) => paths.Under(paths.Memos, id) + ".json";

    public async Task SaveAsync(StoredDossier dossier, CancellationToken ct = default)
    {
        var id = dossier.Dossier.DossierId;
        await WriteAsync(DossierPath(id), dossier, ct);
        cache.Set(Key("d", id), dossier, CacheTtl);
        cache.Remove("dossier-list");
    }

    public Task<StoredDossier?> GetAsync(string dossierId, CancellationToken ct = default) =>
        !Identifiers.IsSafe(dossierId) ? Task.FromResult<StoredDossier?>(null) : ReadAsync<StoredDossier>(Key("d", dossierId), DossierPath(dossierId), ct);

    public async Task<IReadOnlyList<DossierSummary>> ListAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue("dossier-list", out IReadOnlyList<DossierSummary>? cached) && cached is not null) return cached;
        var list = new List<DossierSummary>();
        foreach (var file in Directory.EnumerateFiles(paths.Dossiers, "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var stored = await GetAsync(id, ct);
            if (stored is null) continue;
            var d = stored.Dossier;
            list.Add(new DossierSummary(d.DossierId, d.CommitteeRecordNumber, d.Taxpayer.NameAr, d.DisputedFiscalYear, d.CommitteeFilingDate, stored.Source));
        }
        IReadOnlyList<DossierSummary> result = list.OrderByDescending(s => s.DossierId, StringComparer.Ordinal).ToList();
        cache.Set("dossier-list", result, TimeSpan.FromMinutes(1));
        return result;
    }

    public async Task SaveMemoAsync(AssembledMemo memo, string dossierId, CancellationToken ct = default)
    {
        await WriteAsync(MemoPath(dossierId), memo, ct);
        cache.Set(Key("m", dossierId), memo, CacheTtl);
    }

    public Task<AssembledMemo?> GetMemoAsync(string dossierId, CancellationToken ct = default) =>
        !Identifiers.IsSafe(dossierId) ? Task.FromResult<AssembledMemo?>(null) : ReadAsync<AssembledMemo>(Key("m", dossierId), MemoPath(dossierId), ct);

    private static string Key(string kind, string id) => $"{kind}:{id}";

    private static async Task WriteAsync<T>(string path, T value, CancellationToken ct)
    {
        var gate = Locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await LocalDataPaths.WriteAtomicAsync(path, JsonSerializer.SerializeToUtf8Bytes(value, JsonDefaults.Indented), ct);
        }
        finally { gate.Release(); }
    }

    private async Task<T?> ReadAsync<T>(string cacheKey, string path, CancellationToken ct) where T : class
    {
        if (cache.TryGetValue(cacheKey, out T? hit) && hit is not null) return hit;
        if (!File.Exists(path)) return null;
        try
        {
            await using var fs = File.OpenRead(path);
            var value = await JsonSerializer.DeserializeAsync<T>(fs, JsonDefaults.Options, ct);
            if (value is not null) cache.Set(cacheKey, value, CacheTtl);
            return value;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Corrupted JSON at {Path}", path);
            return null;
        }
    }
}
