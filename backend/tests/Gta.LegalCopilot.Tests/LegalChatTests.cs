using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using Azure.Search.Documents.Models;
using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Application.Chat;
using Gta.LegalCopilot.Application.Common;
using Gta.LegalCopilot.Application.Services;
using Gta.LegalCopilot.Infrastructure.Ai;
using Gta.LegalCopilot.Infrastructure.Ai.Prompts;
using Gta.LegalCopilot.Infrastructure.Search;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Gta.LegalCopilot.Tests;

public sealed class LegalChatTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MafAgent_ReceivesDossierContext_ForFactsAndLegalQuestions(bool legal)
    {
        using var host = new ApiFactory();
        using var client = host.CreateClient();
        (await client.PostAsync("/api/demo-cases/dohaTech/load", null)).EnsureSuccessStatusCode();
        using var scope = host.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IDossierRepository>().GetAsync("DEMO-DOHATECH");
        var analysis = scope.ServiceProvider.GetRequiredService<CaseAnalysisService>().Analyze(stored!.Dossier);
        var context = new LegalChatContext(stored.Dossier, analysis, DocumentText: "stored-test-document");
        var model = new FakeModel(legal ? "حكم قانوني [S1]" : "بحسب الملف: شركة الدوحة")
        {
            IntentJson = legal ? "{\"requiresLegalSources\":true}" : "{\"requiresLegalSources\":false}",
        };
        var search = new FakeSearch([Source()]);
        var answer = await CreateAgent(model, search).AnswerAsync(new LegalChatRequest("اشرح الملف"), context: context);
        Assert.Contains(model.Messages, m => m.Text.Contains("DEMO-DOHATECH") && m.Text.Contains("stored-test-document"));
        Assert.Contains(model.IntentMessages, m => m.Text.Contains("DEMO-DOHATECH"));
        Assert.Equal(legal ? 1 : 0, search.Calls);
        Assert.Equal(legal, answer.Grounded);
        if (legal)
        {
            Assert.Contains(stored.Dossier.DisputedItems[0].DescriptionAr, search.Queries[0]);
            Assert.Contains(stored.Dossier.DisputedItems[0].TaxpayerLegalReference, search.Queries[0]);
        }
    }

    [Fact]
    public async Task MafAgent_ContextualSearch_UsesTheResolvedTopicInsteadOfTheVagueQuestion()
    {
        var search = new FakeSearch([Source()]);
        var model = new FakeModel("شرح موثق [S1]")
        {
            IntentJson = "{\"requiresLegalSources\":true,\"searchQuery\":\"خصم أتعاب الإدارة المادة 33 قانون ضريبة الدخل قطر\"}",
        };
        var result = await CreateAgent(model, search).AnswerAsync(new LegalChatRequest("وما الأساس القانوني؟",
            [new("user", "اشرح أتعاب الإدارة"), new("assistant", "البند يخص أتعاب الإدارة")]));
        Assert.Equal("خصم أتعاب الإدارة المادة 33 قانون ضريبة الدخل قطر", Assert.Single(search.Queries));
        Assert.True(result.Grounded);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MafAgent_StreamsIncrementalText_ThenReturnsCheckedSources(bool legal)
    {
        var model = new FakeModel("شرح تجريبي [S1]")
        {
            IntentJson = legal ? "{\"requiresLegalSources\":true}" : "{\"requiresLegalSources\":false}",
        };
        var chunks = new List<string>();
        var answer = await CreateAgent(model, new FakeSearch([Source()])).AnswerAsync(
            new LegalChatRequest("سؤال"), onDelta: text =>
            {
                chunks.Add(text);
                Assert.False(model.StreamCompleted);
                return Task.CompletedTask;
            });
        Assert.True(chunks.Count > 1);
        Assert.Equal(answer.Answer, string.Concat(chunks));
        Assert.Equal(legal, answer.Grounded);
        Assert.Equal(legal ? 1 : 0, answer.Sources.Count);
    }

    [Fact]
    public async Task MafAgent_StreamingDraftWithoutCitations_IsReplacedByNoEvidenceAnswer()
    {
        var draft = new StringBuilder();
        var answer = await CreateAgent(new FakeModel("إجابة غير موثقة"), new FakeSearch([Source()]))
            .AnswerAsync(new LegalChatRequest("سؤال قانوني"), onDelta: text =>
            {
                draft.Append(text);
                return Task.CompletedTask;
            });
        Assert.Equal("إجابة غير موثقة", draft.ToString());
        Assert.NotEqual(draft.ToString(), answer.Answer);
        Assert.Empty(answer.Sources);
        Assert.False(answer.Grounded);
    }

    [Fact]
    public async Task MafAgent_StreamingSearchTool_ResolvesAdditionalSourcesWithoutStreamingToolData()
    {
        var search = new FakeSearch([Source()])
        {
            Resolve = query => query == "استعلام الأداة" ? [Source("law-chunk-2")] : [Source()],
        };
        var draft = new StringBuilder();
        var answer = await CreateAgent(new FakeModel("إجابة موثقة [S2]", invokeTool: true), search)
            .AnswerAsync(new LegalChatRequest("سؤال تابع"), onDelta: text =>
            {
                draft.Append(text);
                return Task.CompletedTask;
            });
        Assert.Equal(2, search.Calls);
        Assert.Equal("إجابة موثقة [S2]", draft.ToString());
        Assert.Equal("law-chunk-2", Assert.Single(answer.Sources).Id);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("how are you?")]
    [InlineData("مرحبا، كيف حالك؟")]
    [InlineData("ماذا تستطيع أن تفعل؟")]
    public async Task MafAgent_CasualConversation_ReturnsTheReplyWithoutSearchOrSources(string question)
    {
        var search = new FakeSearch([]);
        var model = new FakeModel("أهلاً! كيف يمكنني مساعدتك؟")
        {
            IntentJson = "{\"requiresLegalSources\":false}",
        };
        var answer = await CreateAgent(model, search).AnswerAsync(new LegalChatRequest(question, [
            new("user", "اشرح القانون"), new("assistant", "إجابة قانونية سابقة [S1]") ]));

        Assert.Equal("أهلاً! كيف يمكنني مساعدتك؟", answer.Answer);
        Assert.Empty(answer.Sources);
        Assert.False(answer.Grounded);
        Assert.Equal(0, search.Calls);
        Assert.Empty(model.Options!.Tools ?? []);
        Assert.Contains(model.IntentMessages, m => m.Text == question);
        Assert.Contains(model.IntentMessages, m => m.Text == "اشرح القانون");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task MafAgent_UncertainIntent_KeepsLegalRetrievalAndCitationRequirements(string intentJson)
    {
        var search = new FakeSearch([Source()]);
        var model = new FakeModel("إجابة غير موثقة") { IntentJson = intentJson };
        var answer = await CreateAgent(model, search).AnswerAsync(new LegalChatRequest("وما الموعد؟"));
        Assert.Equal(1, search.Calls);
        Assert.NotEqual("إجابة غير موثقة", answer.Answer);
        Assert.False(answer.Grounded);
    }

    [Theory]
    [InlineData("hello, what is the objection deadline?")]
    [InlineData("مرحبا، اشرح المادة 18")]
    [InlineData("وما الموعد؟")]
    public async Task MafAgent_LegalQuestionsAndFollowUps_StillRequireSources(string question)
    {
        var search = new FakeSearch([Source()]);
        var model = new FakeModel("شرح موثق [S1]");
        var answer = await CreateAgent(model, search).AnswerAsync(new LegalChatRequest(question,
            [new("user", "اشرح الاعتراض"), new("assistant", "إجابة سابقة")]));
        Assert.Equal(1, search.Calls);
        Assert.True(answer.Grounded);
        Assert.Single(answer.Sources);
        Assert.Contains(model.IntentMessages, m => m.Text == "اشرح الاعتراض");
    }

    private static LegalSource Source(string id = "law-chunk-1") => AzureLegalKnowledgeSearch.Map(new SearchDocument
    {
        ["id"] = id, ["LawId"] = "24-2018", ["OfficialTitle"] = "قانون الضريبة على الدخل",
        ["Number"] = "24", ["Year"] = "2018", ["ArticleNumber"] = "18",
        ["Status"] = "قيد التطبيق", ["AmendedLawName"] = "القانون الأصلي",
        ["SourceUrl"] = "https://www.almeezan.qa/example", ["Content"] = "نص قانوني تجريبي",
    }, 0.8);

    [Fact]
    public async Task MafAgent_RetrievesBeforeAnswering_AndPreservesFollowUpHistory()
    {
        var search = new FakeSearch([Source()]);
        var model = new FakeModel("شرح المادة [S1]");
        var agent = CreateAgent(model, search);
        var answer = await agent.AnswerAsync(new LegalChatRequest("اشرح المادة", [
            new("user", "ما هو الاعتراض؟"), new("assistant", "طلب مراجعة القرار.") ]));

        Assert.True(answer.Grounded);
        Assert.Equal("S1", Assert.Single(answer.Sources).CitationId);
        Assert.Equal(1, search.Calls);
        Assert.Contains(model.Messages, m => m.Role == ChatRole.User && m.Text == "ما هو الاعتراض؟");
        Assert.Contains(model.Messages, m => m.Text.Contains("المصادر المسترجعة"));
        Assert.Contains("search_qatar_laws", model.Options!.Tools!.OfType<AIFunction>().Select(t => t.Name));
    }

    [Fact]
    public async Task MafAgent_InvokesItsSearchTool_AndResolvesNewSources()
    {
        var search = new FakeSearch([Source()])
        {
            Resolve = query => query == "استعلام الأداة" ? [Source("law-chunk-2")] : [Source()],
        };
        var model = new FakeModel("الإجابة بعد البحث الإضافي [S2]", invokeTool: true);
        var answer = await CreateAgent(model, search).AnswerAsync(new LegalChatRequest("سؤال تابع"));

        Assert.True(answer.Grounded);
        Assert.Equal(2, search.Calls);
        Assert.Equal("law-chunk-2", Assert.Single(answer.Sources).Id);
        Assert.Equal("S2", answer.Sources[0].CitationId);
        Assert.Contains(model.Messages, message => message.Role == ChatRole.Tool);
    }

    [Theory]
    [InlineData("إجابة بلا مصدر")]
    [InlineData("حكم قانوني [S999]")]
    public async Task MafAgent_DoesNotReturnAnUncitedOrInventedSourceAnswer(string candidate)
    {
        var answer = await CreateAgent(new FakeModel(candidate), new FakeSearch([Source()]))
            .AnswerAsync(new LegalChatRequest("سؤال قانوني"));
        Assert.False(answer.Grounded);
        Assert.Empty(answer.Sources);
        Assert.DoesNotContain(candidate, answer.Answer);
    }

    [Fact]
    public async Task MafAgent_NoResults_ReturnsAnExplicitNoEvidenceAnswer()
    {
        var answer = await CreateAgent(new FakeModel("إجابة [S1]"), new FakeSearch([]))
            .AnswerAsync(new LegalChatRequest("سؤال قانوني"));
        Assert.False(answer.Grounded);
        Assert.Empty(answer.Sources);
    }

    [Fact]
    public async Task MafAgent_ModelFailure_ReportsUnavailableRatherThanAnAnswer()
    {
        var model = new FakeModel("unused") { Failure = new InvalidOperationException("provider failure") };
        await Assert.ThrowsAsync<LegalChatUnavailableException>(() =>
            CreateAgent(model, new FakeSearch([Source()])).AnswerAsync(new LegalChatRequest("سؤال")));
    }

    [Fact]
    public async Task SearchContext_DeduplicatesChunks_AndEnforcesCallLimit()
    {
        var search = new FakeSearch([Source()]);
        var context = new LegalSearchContext(search, 2);
        var first = await context.SearchAsync("استعلام أول");
        var second = await context.SearchAsync("استعلام ثان");
        Assert.Equal(first[0].CitationId, second[0].CitationId);
        Assert.Single(context.Sources);
        Assert.Empty(await context.SearchAsync("استعلام ثالث"));
        Assert.Equal(2, search.Calls);
    }

    [Fact]
    public void Sources_KeepMetadata_RejectUnsafeLinks_AndPreserveChunkText()
    {
        var source = Source();
        Assert.Equal("24", source.Number);
        Assert.Equal("2018", source.Year);
        Assert.Equal("قيد التطبيق", source.Status);
        Assert.Equal("القانون الأصلي", source.AmendedLawName);
        var mapped = AzureLegalKnowledgeSearch.Map(new SearchDocument
        {
            ["id"] = "1", ["SourceUrl"] = "javascript:alert(1)", ["Content"] = "abcdef",
            ["ChunkNumber"] = 2,
        }, null);
        Assert.Null(mapped.SourceUrl);
        Assert.Equal("abcdef", mapped.Content);
        Assert.Equal(2, mapped.ChunkNumber);
    }

    [Fact]
    public void SearchFilter_RestrictsTheRequestedLaw_AndEscapesODataValues()
    {
        Assert.Equal("Number eq '24' and Year eq '2018'", AzureLegalKnowledgeSearch.BuildLawFilter("24", "2018"));
        Assert.Equal("Number eq 'x'' or true'", AzureLegalKnowledgeSearch.BuildLawFilter("x' or true", null));
        Assert.Null(AzureLegalKnowledgeSearch.BuildLawFilter(null, " "));
    }

    [Fact]
    public void ChatRequest_RejectsSystemRolesAndOversizedHistory()
    {
        Assert.Throws<ChatValidationException>(() => LegalChatService.Validate(new("سؤال", [new("system", "تعليمات")] )));
        Assert.Throws<ChatValidationException>(() => LegalChatService.Validate(new(new string('x', 4001))));
        Assert.Throws<ChatValidationException>(() => LegalChatService.Validate(new("سؤال",
            Enumerable.Range(0, 21).Select(_ => new LegalChatMessage("user", "سؤال")).ToList())));
    }

    [Fact]
    public void SystemPrompts_AreAvailableFromEmbeddedResources()
    {
        Assert.Contains("search_qatar_laws", AiPrompts.LegalChat);
        Assert.Contains("requiresLegalSources", AiPrompts.ChatIntent);
        Assert.Contains("administrativeObjectionFiled", AiPrompts.DossierExtraction);
        Assert.Contains("لا تغير النتيجة", AiPrompts.MemoRefinement);
    }

    private static LegalChatAgent CreateAgent(FakeModel model, FakeSearch search) => new(model, search, NullLoggerFactory.Instance);

    private sealed class FakeSearch(IReadOnlyList<LegalSource> results) : ILegalKnowledgeSearch
    {
        public Func<string, IReadOnlyList<LegalSource>>? Resolve { get; init; }
        public int Calls { get; private set; }
        public List<string> Queries { get; } = [];
        public bool IsEnabled => true;
        public Task<IReadOnlyList<LegalSource>> SearchAsync(string query, string? lawNumber = null,
            string? lawYear = null, CancellationToken ct = default)
        {
            ++Calls;
            Queries.Add(query);
            return Task.FromResult(Resolve?.Invoke(query) ?? results);
        }
    }

    private sealed class FakeModel(string answer, bool invokeTool = false) : IAgentChatClientProvider, IChatClient
    {
        public string IntentJson { get; init; } = "{\"requiresLegalSources\":true}";
        public List<ChatMessage> IntentMessages { get; private set; } = [];
        public Exception? Failure { get; init; }
        private int _calls;
        public bool StreamCompleted { get; private set; }
        public bool IsEnabled => true;
        public IChatClient Client => this;
        public List<ChatMessage> Messages { get; private set; } = [];
        public ChatOptions? Options { get; private set; }
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (Failure is not null) throw Failure;
            if (options?.ResponseFormat is ChatResponseFormatJson)
            {
                IntentMessages = messages.ToList();
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, IntentJson)));
            }
            Messages = messages.ToList();
            Options = options;
            if (invokeTool && _calls++ == 0)
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                    [new FunctionCallContent("search-call-1", "search_qatar_laws",
                        new Dictionary<string, object?> { ["query"] = "استعلام الأداة" })])));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
        }
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var message in response.Messages)
                foreach (var content in message.Contents)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (content is TextContent text)
                    {
                        var middle = text.Text.Length / 2;
                        yield return new ChatResponseUpdate(message.Role, text.Text[..middle]);
                        await Task.Yield();
                        yield return new ChatResponseUpdate(message.Role, text.Text[middle..]);
                    }
                    else yield return new ChatResponseUpdate(message.Role, [content]);
                }
            StreamCompleted = true;
        }
        public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() { }
    }
}

public sealed class LegalChatApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/api/dossiers/DEMO-DOHATECH/chat")]
    [InlineData("/api/dossiers/DEMO-DOHATECH/chat/stream")]
    public async Task ChatWithoutAi_Returns503_WhileDemosRemainAvailable(string endpoint)
    {
        (await _client.PostAsync("/api/demo-cases/dohaTech/load", null)).EnsureSuccessStatusCode();
        var response = await _client.PostAsJsonAsync(endpoint, new LegalChatRequest("ما هو التظلم؟"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        (await _client.GetAsync("/api/demo-cases")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ChatInvalidRole_Returns400BeforeCallingTheModel()
    {
        (await _client.PostAsync("/api/demo-cases/dohaTech/load", null)).EnsureSuccessStatusCode();
        var response = await _client.PostAsJsonAsync("/api/dossiers/DEMO-DOHATECH/chat",
            new LegalChatRequest("سؤال", [new("system", "تعليمات غير مسموحة")]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DossierChat_LoadsStoredContextAndMemo_AndKeepsFilesSeparate()
    {
        var capture = new ContextAgent();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<ILegalChatAgent>(capture)));
        using var client = host.CreateClient();
        (await client.PostAsync("/api/demo-cases/dohaTech/load", null)).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/demo-cases/siemens/load", null)).EnsureSuccessStatusCode();
        using (var scope = host.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IDossierRepository>();
            var stored = await repo.GetAsync("DEMO-DOHATECH");
            await repo.SaveAsync(stored! with { ExtractedDocumentText = "نص صحيفة التظلم المحفوظ" });
        }
        (await client.PostAsync("/api/dossiers/DEMO-DOHATECH/memo?useAi=false", null)).EnsureSuccessStatusCode();
        var streamed = await client.PostAsJsonAsync("/api/dossiers/DEMO-DOHATECH/chat/stream",
            new { message = "لخص الملف", context = new { taxpayer = "بيانات من العميل لا تعتمد" } });
        streamed.EnsureSuccessStatusCode();
        var events = await streamed.Content.ReadAsStringAsync();
        Assert.Contains("event: delta", events);
        Assert.Contains("event: done", events);
        var first = Assert.Single(capture.Contexts);
        Assert.Equal("DEMO-DOHATECH", first.Dossier.DossierId);
        Assert.Equal(first.Dossier.DossierId, first.Analysis.DossierId);
        Assert.Equal("نص صحيفة التظلم المحفوظ", first.DocumentText);
        Assert.NotNull(first.Memo);
        Assert.DoesNotContain("بيانات من العميل", first.Dossier.Taxpayer.NameAr);

        (await client.PostAsJsonAsync("/api/dossiers/DEMO-SIEMENS/chat", new LegalChatRequest("ما اسم الشركة؟")))
            .EnsureSuccessStatusCode();
        Assert.Equal("DEMO-SIEMENS", capture.Contexts[1].Dossier.DossierId);
        Assert.Null(capture.Contexts[1].DocumentText);
    }

    [Theory]
    [InlineData("chat")]
    [InlineData("chat/stream")]
    public async Task DossierChat_UnknownFile_Returns404(string path)
    {
        var response = await _client.PostAsJsonAsync($"/api/dossiers/DOES-NOT-EXIST/{path}", new LegalChatRequest("سؤال"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class ContextAgent : ILegalChatAgent
    {
        public bool IsEnabled => true;
        public List<LegalChatContext> Contexts { get; } = [];
        public async Task<LegalChatAnswer> AnswerAsync(LegalChatRequest request, CancellationToken ct = default,
            Func<string, Task>? onDelta = null, LegalChatContext? context = null)
        {
            Assert.NotNull(context);
            Contexts.Add(context);
            if (onDelta is not null) await onDelta(context.Dossier.Taxpayer.NameAr);
            return new LegalChatAnswer(context.Dossier.Taxpayer.NameAr, [], false);
        }
    }

    [Fact]
    public async Task HealthReportsChatAvailability()
    {
        var response = await _client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/health");
        Assert.False(response.GetProperty("legalChatEnabled").GetBoolean());
        Assert.True(response.TryGetProperty("legalSearchEnabled", out _));
    }
}
