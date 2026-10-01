using System.Runtime.CompilerServices;
using System.Text;
using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Gta.LegalCopilot.Application.Services;

public record MemoStreamEvent(string Type, string? Section = null, string? Text = null, object? Payload = null);

public static class MemoStreamEventTypes
{
    public const string Analysis = "analysis";
    public const string SectionStart = "section_start";
    public const string Delta = "delta";
    public const string SectionReplace = "section_replace";
    public const string SectionEnd = "section_end";
    public const string Memo = "memo";
    public const string Error = "error";
}

/// <summary>Assembles the memo: deterministic engines + templates, with optional guarded LLM polishing.</summary>
public sealed class MemoOrchestrator(
    CaseAnalysisService analysis,
    MemoComposer composer,
    ILegalNarrativeGenerator narrative,
    IDossierRepository repository,
    ILogger<MemoOrchestrator> logger)
{
    /// <summary>Sections the LLM may rephrase. Formal plea, financial impact and requests are always deterministic.</summary>
    public static readonly IReadOnlySet<string> RefinableSections =
        new HashSet<string> { MemoSections.Facts, MemoSections.SubstantiveDefense };

    public async Task<AssembledMemo> BuildAsync(DisputeDossier dossier, bool useAi, CancellationToken ct = default)
    {
        AssembledMemo? memo = null;
        await foreach (var e in StreamAsync(dossier, useAi, ct))
            if (e.Type == MemoStreamEventTypes.Memo) memo = (AssembledMemo)e.Payload!;
        return memo!;
    }

    public async IAsyncEnumerable<MemoStreamEvent> StreamAsync(DisputeDossier dossier, bool useAi, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var a = analysis.Analyze(dossier);
        yield return new MemoStreamEvent(MemoStreamEventTypes.Analysis, Payload: a);

        var drafts = composer.ComposeSections(dossier, a);
        var final = new Dictionary<string, string>();
        var aiActive = useAi && narrative.IsEnabled;
        var aiUsed = false;

        foreach (var key in MemoSections.Ordered)
        {
            var draft = drafts[key];
            yield return new MemoStreamEvent(MemoStreamEventTypes.SectionStart, key, MemoSections.TitlesAr[key]);

            if (aiActive && RefinableSections.Contains(key))
            {
                var buffer = new StringBuilder();
                var failed = false;
                await using var e = narrative.StreamRefinedSectionAsync(key, draft, ct).GetAsyncEnumerator(ct);
                while (true)
                {
                    string? chunk;
                    try
                    {
                        if (!await e.MoveNextAsync()) break;
                        chunk = e.Current;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "LLM refinement failed for section {Section}; using deterministic draft", key);
                        failed = true;
                        break;
                    }
                    buffer.Append(chunk);
                    yield return new MemoStreamEvent(MemoStreamEventTypes.Delta, key, chunk);
                }

                var refined = buffer.ToString().Trim();
                IReadOnlyList<decimal> foreign = [];
                if (!failed && NumericIntegrityGuard.IsFaithful(draft, refined, out foreign))
                {
                    final[key] = refined;
                    aiUsed = true;
                }
                else
                {
                    if (!failed)
                        logger.LogWarning("Numeric integrity guard rejected LLM output for {Section}: foreign numbers {Numbers}", key, string.Join(",", foreign));
                    final[key] = draft;
                    yield return new MemoStreamEvent(MemoStreamEventTypes.SectionReplace, key, draft);
                }
            }
            else
            {
                final[key] = draft;
                foreach (var chunk in Chunk(draft))
                    yield return new MemoStreamEvent(MemoStreamEventTypes.Delta, key, chunk);
            }
            yield return new MemoStreamEvent(MemoStreamEventTypes.SectionEnd, key);
        }

        var memo = new AssembledMemo(
            MemoComposer.DocumentType, composer.BuildMetadata(dossier), final,
            composer.Requests(dossier, a), a.Procedural, a.Substantive, a.Financial,
            aiUsed ? "AZURE_OPENAI_REFINED" : "DETERMINISTIC_TEMPLATE", DateTimeOffset.UtcNow);
        await repository.SaveMemoAsync(memo, dossier.DossierId, ct);
        yield return new MemoStreamEvent(MemoStreamEventTypes.Memo, Payload: memo);
    }

    private static IEnumerable<string> Chunk(string text, int size = 120)
    {
        for (var i = 0; i < text.Length; i += size)
            yield return text.Substring(i, Math.Min(size, text.Length - i));
    }
}
