using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Gta.LegalCopilot.Application.Chat;
using Gta.LegalCopilot.Infrastructure.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;

namespace Gta.LegalCopilot.Infrastructure.Search;

/// <summary>Queries the existing law index; never creates or changes its schema.</summary>
public sealed class AzureLegalKnowledgeSearch : ILegalKnowledgeSearch
{
    private static readonly string[] SelectedFields =
    [
        "id", "LawId", "RegulationType", "Number", "Year", "OfficialTitle", "Date", "Status", "Type",
        "AmendedLawNumber", "AmendedLawName", "AmendedLawYear", "ArticlesCount", "SourceUrl",
        "OfficialGazetteIssueNumber", "OfficialGazettePublicationDate", "OfficialGazettePage", "LawDescription",
        "LawHeaderTokenSize", "ArticleNumber", "ChunkNumber", "Content", "TokenSize",
    ];

    private readonly AzureAISearchOptions _options;
    private readonly ChatClientFactory _openAI;
    private readonly ILogger<AzureLegalKnowledgeSearch> _logger;
    private readonly Lazy<SearchClient> _client;

    public AzureLegalKnowledgeSearch(IOptions<AzureAISearchOptions> options, ChatClientFactory openAI,
        ILogger<AzureLegalKnowledgeSearch> logger)
    {
        _options = options.Value;
        _openAI = openAI;
        _logger = logger;
        _client = new(() => string.IsNullOrWhiteSpace(_options.ApiKey)
            ? new SearchClient(new Uri(_options.Endpoint!), _options.IndexName, new DefaultAzureCredential())
            : new SearchClient(new Uri(_options.Endpoint!), _options.IndexName, new AzureKeyCredential(_options.ApiKey)));
    }

    public bool IsEnabled => _options.IsConfigured && _openAI.EmbeddingsConfigured;

    public async Task<IReadOnlyList<LegalSource>> SearchAsync(string query, string? lawNumber = null,
        string? lawYear = null, CancellationToken ct = default)
    {
        if (!IsEnabled) throw new LegalChatUnavailableException("البحث القانوني غير مهيأ.");
        if (string.IsNullOrWhiteSpace(query)) return [];
        query = query.Trim();
        if (query.Length > LegalChatService.MaxMessageLength) query = query[..LegalChatService.MaxMessageLength];
        try
        {
            var searchOptions = new SearchOptions { Size = 6, QueryType = SearchQueryType.Simple };
            foreach (var field in new[] { "Content", "OfficialTitle", "LawDescription", "Number", "Year", "ArticleNumber" })
                searchOptions.SearchFields.Add(field);
            searchOptions.Filter = BuildLawFilter(lawNumber, lawYear);
            foreach (var field in SelectedFields) searchOptions.Select.Add(field);
            var embedding = await _openAI.EmbeddingClient.GenerateEmbeddingAsync(query,
                new EmbeddingGenerationOptions { Dimensions = AzureAISearchOptions.VectorDimensions }, ct);
            var vector = embedding.Value.ToFloats();
            if (vector.Length != AzureAISearchOptions.VectorDimensions)
                throw new InvalidOperationException("Embedding dimensions do not match the law index.");
            searchOptions.VectorSearch = new VectorSearchOptions();
            searchOptions.VectorSearch.Queries.Add(new VectorizedQuery(vector)
            {
                KNearestNeighborsCount = 50,
                Fields = { "ContentVector" },
            });
            var response = await _client.Value.SearchAsync<SearchDocument>(query, searchOptions, ct);
            var sources = new List<LegalSource>();
            await foreach (var result in response.Value.GetResultsAsync().WithCancellation(ct))
            {
                var source = Map(result.Document, result.Score);
                if (!string.IsNullOrWhiteSpace(source.Id) && !string.IsNullOrWhiteSpace(source.Content)) sources.Add(source);
            }
            return sources;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Legal knowledge retrieval failed");
            throw new LegalChatUnavailableException("تعذر البحث في المصادر القانونية. تحقق من خدمة البحث ونموذج التضمين ثم أعد المحاولة.", ex);
        }
    }

    public static string? BuildLawFilter(string? lawNumber, string? lawYear)
    {
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(lawNumber)) filters.Add(SearchFilter.Create($"Number eq {lawNumber.Trim()}"));
        if (!string.IsNullOrWhiteSpace(lawYear)) filters.Add(SearchFilter.Create($"Year eq {lawYear.Trim()}"));
        return filters.Count == 0 ? null : string.Join(" and ", filters);
    }

    public static LegalSource Map(SearchDocument document, double? score)
    {
        string? Text(string name) => document.TryGetValue(name, out var value) ? value?.ToString() : null;
        int? Integer(string name) => int.TryParse(Text(name), out var value) ? value : null;
        var content = Text("Content") ?? string.Empty;
        var url = Text("SourceUrl");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) url = null;
        return new LegalSource(Text("id") ?? string.Empty, Text("LawId"), Text("RegulationType"), Text("Number"),
            Text("Year"), Text("OfficialTitle"), Text("Date"), Text("Status"), Text("Type"), Text("AmendedLawNumber"),
            Text("AmendedLawName"), Text("AmendedLawYear"), Integer("ArticlesCount"), url,
            Text("OfficialGazetteIssueNumber"), Text("OfficialGazettePublicationDate"), Text("OfficialGazettePage"),
            Text("LawDescription"), Integer("LawHeaderTokenSize"), Text("ArticleNumber"), Integer("ChunkNumber"),
            content, Integer("TokenSize"), score);
    }
}
