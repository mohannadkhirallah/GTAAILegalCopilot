namespace Gta.LegalCopilot.Infrastructure.Ai.Prompts;

/// <summary>Loads versioned system instructions embedded with the application.</summary>
public static class AiPrompts
{
    public static string LegalChat { get; } = Load("LegalChat.md");
    public static string ChatIntent { get; } = Load("ChatIntent.md");
    public static string DossierExtraction { get; } = Load("DossierExtraction.md");
    public static string MemoRefinement { get; } = Load("MemoRefinement.md");

    private static string Load(string fileName)
    {
        using var stream = typeof(AiPrompts).Assembly.GetManifestResourceStream($"{typeof(AiPrompts).Namespace}.{fileName}")
            ?? throw new InvalidOperationException($"Missing embedded AI prompt: {fileName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
