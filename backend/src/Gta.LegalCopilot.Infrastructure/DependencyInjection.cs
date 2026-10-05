using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Application.Services;
using Gta.LegalCopilot.Application.Chat;
using Gta.LegalCopilot.Domain.Services;
using Gta.LegalCopilot.Domain.Statutes;
using Gta.LegalCopilot.Infrastructure.Ai;
using Gta.LegalCopilot.Infrastructure.Demo;
using Gta.LegalCopilot.Infrastructure.Documents;
using Gta.LegalCopilot.Infrastructure.Storage;
using Gta.LegalCopilot.Infrastructure.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gta.LegalCopilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLegalCopilot(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Section));
        services.Configure<AzureOpenAIOptions>(configuration.GetSection(AzureOpenAIOptions.Section));
        services.Configure<AzureAISearchOptions>(configuration.GetSection(AzureAISearchOptions.Section));
        services.AddMemoryCache();

        // Deterministic domain core
        var statutory = configuration.GetSection("Statutory").Get<StatutoryParameters>() ?? StatutoryParameters.Default;
        var policy = configuration.GetSection("SubstantivePolicy").Get<SubstantivePolicy>() ?? SubstantivePolicy.Default;
        services.AddSingleton(statutory);
        services.AddSingleton(policy);
        services.AddSingleton<StatutoryDeadlineCalculator>();
        services.AddSingleton<DelayPenaltyCalculator>();
        services.AddSingleton<ProceduralAdmissibilityService>();
        services.AddSingleton<SubstantiveRuleEngine>();
        services.AddSingleton<FinancialRecalculationService>();

        // Application
        services.AddSingleton<CaseAnalysisService>();
        services.AddSingleton<MemoComposer>();
        services.AddScoped<MemoOrchestrator>();

        // Infrastructure
        services.AddSingleton<LocalDataPaths>();
        services.AddSingleton<IFileStore, LocalFileStore>();
        services.AddSingleton<IDossierRepository, LocalDossierRepository>();
        services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();
        services.AddSingleton<IMemoDocumentRenderer, WordMemoRenderer>();
        services.AddSingleton<IDemoCaseCatalog, DemoCaseCatalog>();
        services.AddSingleton<ChatClientFactory>();
        services.AddSingleton<IAgentChatClientProvider>(sp => sp.GetRequiredService<ChatClientFactory>());
        services.AddSingleton<ILegalKnowledgeSearch, AzureLegalKnowledgeSearch>();
        services.AddScoped<ILegalChatAgent, LegalChatAgent>();
        services.AddScoped<LegalChatService>();
        services.AddSingleton<AzureOpenAiNarrativeGenerator>();
        services.AddSingleton<AzureOpenAiDossierExtractor>();
        services.AddSingleton<ILegalNarrativeGenerator>(sp => sp.GetRequiredService<ChatClientFactory>().IsConfigured
            ? sp.GetRequiredService<AzureOpenAiNarrativeGenerator>() : new DisabledNarrativeGenerator());
        services.AddSingleton<IDossierExtractor>(sp => sp.GetRequiredService<ChatClientFactory>().IsConfigured
            ? sp.GetRequiredService<AzureOpenAiDossierExtractor>() : new DisabledDossierExtractor());
        return services;
    }
}
