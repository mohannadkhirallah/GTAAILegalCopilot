using Azure.Identity;
using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace Gta.LegalCopilot.Infrastructure.Ai;

public sealed class ChatClientFactory : IAgentChatClientProvider
{
    private readonly AzureOpenAIOptions _options;
    private readonly Lazy<OpenAIClient?> _client;
    private readonly Lazy<ChatClient?> _chat;
    private readonly Lazy<IChatClient> _agent;

    public ChatClientFactory(IOptions<AzureOpenAIOptions> options)
    {
        _options = options.Value;
        _client = new(() => Create(_options));
        _chat = new(() => _client.Value?.GetChatClient(_options.Deployment));
        _agent = new(() => Client.AsIChatClient());
    }

    bool IAgentChatClientProvider.IsEnabled => IsConfigured;
    IChatClient IAgentChatClientProvider.Client => _agent.Value;

    public bool IsConfigured => _options.IsConfigured;
    public bool EmbeddingsConfigured => IsConfigured && !string.IsNullOrWhiteSpace(_options.EmbeddingDeployment);
    public ChatClient Client => _chat.Value ?? throw new InvalidOperationException("Azure OpenAI is not configured.");
    public EmbeddingClient EmbeddingClient => EmbeddingsConfigured
        ? _client.Value!.GetEmbeddingClient(_options.EmbeddingDeployment)
        : throw new InvalidOperationException("Azure OpenAI embeddings are not configured.");

    private static OpenAIClient? Create(AzureOpenAIOptions o)
    {
        if (!o.IsConfigured) return null;
        // Azure's v1 endpoint uses the same SDK as Agent Framework, avoiding incompatible SDK versions.
        var endpoint = o.Endpoint!.TrimEnd('/');
        if (!endpoint.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
            endpoint += "/openai/v1";
        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint + "/") };
#pragma warning disable OPENAI001 // Entra ID authentication is experimental in the OpenAI SDK.
        return string.IsNullOrWhiteSpace(o.ApiKey)
            ? new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), options)
            : new OpenAIClient(new ApiKeyCredential(o.ApiKey), options);
#pragma warning restore OPENAI001
    }
}

public interface IAgentChatClientProvider
{
    bool IsEnabled { get; }
    IChatClient Client { get; }
}
