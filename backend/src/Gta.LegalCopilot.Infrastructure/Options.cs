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
    /// <summary>Optional. When empty, DefaultAzureCredential (Managed Identity / Entra ID) is used.</summary>
    public string? ApiKey { get; set; }
    public string Deployment { get; set; } = "gpt-4o";
    public bool Enabled { get; set; } = true;
    public bool IsConfigured => Enabled && Uri.TryCreate(Endpoint, UriKind.Absolute, out _);
}
