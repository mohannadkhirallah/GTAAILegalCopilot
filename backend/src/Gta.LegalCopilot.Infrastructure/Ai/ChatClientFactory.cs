using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace Gta.LegalCopilot.Infrastructure.Ai;

public sealed class ChatClientFactory(IOptions<AzureOpenAIOptions> options)
{
    private readonly Lazy<ChatClient?> _client = new(() => Create(options.Value));

    public bool IsConfigured => options.Value.IsConfigured;
    public ChatClient Client => _client.Value ?? throw new InvalidOperationException("Azure OpenAI is not configured.");

    private static ChatClient? Create(AzureOpenAIOptions o)
    {
        if (!o.IsConfigured) return null;
        var endpoint = new Uri(o.Endpoint!);
        var client = string.IsNullOrWhiteSpace(o.ApiKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
            : new AzureOpenAIClient(endpoint, new AzureKeyCredential(o.ApiKey));
        return client.GetChatClient(o.Deployment);
    }
}
