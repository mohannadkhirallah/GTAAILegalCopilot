namespace Gta.LegalCopilot.Infrastructure;

public sealed class StorageOptions
{
    public const string Section = "Storage";
    public string RootPath { get; set; } = "./data";
    public long MaxUploadBytes { get; set; } = 20 * 1024 * 1024;
}

public sealed class AzureOpenAIOptions
{
    public const string Section = "AzureOpenAI";
    public string? Endpoint { get; set; }
    /// <summary>When empty, use DefaultAzureCredential.</summary>
    public string? ApiKey { get; set; }
    public string Deployment { get; set; } = "gpt-4o";
    public string? EmbeddingDeployment { get; set; }
    public bool Enabled { get; set; } = true;
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(Deployment)
        && Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;
}

public sealed class AzureAISearchOptions
{
    public const string Section = "AzureAISearch";
    public const int VectorDimensions = 3072;
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? IndexName { get; set; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(IndexName)
        && Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;
}
