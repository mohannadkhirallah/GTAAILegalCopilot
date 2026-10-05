# Legal chat and Azure AI Search

The dossier assistant opens in a floating side panel from the dossier heading or the side tab. The main document and its analysis remain available alongside the assistant. Closing the panel preserves the conversation; switching dossiers starts a new one. It answers questions about that dossier using its extracted facts, current deterministic analysis, saved memo when available, and extracted document text. Legal questions also use Microsoft Agent Framework (MAF) and the existing Qatar-law Azure AI Search index. Chat does not change case determinations, calculations, or stored dossiers.

On each request, the server loads the dossier by the route ID, recomputes its analysis, and loads the saved memo. The browser sends only the question and recent history, never the dossier facts. Unknown dossier IDs return 404. Switching dossiers cancels the pending reply and starts a fresh conversation so history from one file is not reused for another.

New PDF/DOCX uploads save their extracted text with the dossier. Older dossiers and JSON/demo dossiers use their structured data when original text is unavailable; the agent must not claim to have read unavailable attachments.

## Components

| Component | Responsibility |
| --- | --- |
| `Application/Chat/LegalChat.cs` | Validates question/history limits and allowed roles. |
| `Infrastructure/Ai/LegalChatAgent.cs` | Handles conversation routing, the MAF agent, search tools, and source citations in one file. |
| `Infrastructure/Ai/ChatClientFactory.cs` | Shares Azure OpenAI chat and embedding clients. |
| `Infrastructure/Options.cs` | Holds the Azure connection and storage settings. |
| `Infrastructure/Search/AzureLegalKnowledgeSearch.cs` | Generates query embeddings and queries the existing search index. |
| `Infrastructure/Ai/Prompts/LegalChat.md` | Embedded, versioned system instructions. |
| `Api/Endpoints/LegalChatEndpoints.cs` | Exposes `POST /api/dossiers/{id}/chat`, cancellation, and request timeout. |
| `frontend/src/chat/` | Chat transport, conversation state, and Arabic RTL answer/source UI. |

The implementation follows the chat-client/tool patterns in the supplied local `MafLearn/agent-framework` repository and the hybrid retrieval/source collection pattern in `LegalCounselor`. It uses published NuGet packages; neither reference checkout is required to build this application.

Dossier extraction, memo refinement, embeddings, and Agent Framework share the OpenAI .NET SDK against Azure's `/openai/v1/` endpoint. `ChatClientFactory` appends this path to the configured resource URL automatically. Using one SDK avoids the runtime incompatibility between `Azure.AI.OpenAI` 2.1.0 and the newer OpenAI dependency required by Agent Framework.

## Retrieval and citations

A structured-output MAF routing step uses the current message and recent conversation to distinguish casual/general conversation from legal questions. Greetings, general nonlegal questions, factual questions about the dossier, and summaries of its recorded analysis receive replies without law searches or citations. Legal interpretation and evaluation still require retrieved law sources; the dossier and memo are case context rather than legal authority. These replies return `sources: []` and `grounded: false`; the UI shows them as ordinary messages. Invalid or uncertain classification defaults to requiring legal sources. Mixed greetings/legal questions and context-dependent legal follow-ups retain retrieval and citation checks.

The same routing step also resolves a focused Arabic search query from the question, current dossier topics, and follow-up history. Generic questions such as asking for the legal basis of the disputed items therefore search for the actual item descriptions and relevant references. If query planning is unavailable, retrieval uses the question plus dossier item descriptions and claimed references. References in the dossier are search hints, not verified law sources.

Legal requests retrieve sources before generating the answer. Hybrid search sends both the resolved query and its embedding to Azure AI Search:

- Keyword search uses `Content`, `OfficialTitle`, `LawDescription`, `Number`, `Year`, and `ArticleNumber`; their searchability was confirmed against the supplied live index.
- Vector search uses `ContentVector` with **3072 dimensions** and 50 nearest neighbors, returning six results.
- Query embeddings use `AzureOpenAI:EmbeddingDeployment`. Use the same embedding model and dimensions that generated the existing index vectors; matching dimensions alone is insufficient.
- The vector profile (`hnsw-profile`) is already defined on the index. A query does not recreate or change that profile.

All supplied nonvector fields are selected, including `Status`, amendment metadata, article numbers, and gazette metadata. `ContentVector` is never returned to the UI. Retrieved chunk text is returned in full. The search tool can perform additional focused searches for follow-up questions, up to three searches per answer (including the initial retrieval).

The search tool accepts exact `lawNumber`/`lawYear` filters, using escaped OData values. These fields were confirmed filterable in the existing index. The prompt directs general income-tax questions to Law 24/2018 and distinguishes Qatar Financial Centre rules, rather than substituting similarly named legislation. No semantic reranker is configured by default because the reference index has no semantic configuration.

During the live check, the reference index had no matching income-tax-law title for 2018. Number `24`/year `2018` instead returned a ministerial decision about a company conversion. Number/year alone therefore do not uniquely identify a law. The agent must verify the title and subject, and the chat returned no evidence for the general income-tax question. A separate question about QFC tax regulations present in the index returned a cited answer. Index coverage remains an input dependency; this implementation does not ingest missing laws.

The agent cites server-assigned identifiers such as `[S1]`. For legal answers, the server returns only cited source records, and falls back to an explicit no-evidence answer if there are no citations or an identifier was invented. Source links accept only HTTP/HTTPS. This checks source identity; it does not prove that every sentence accurately interprets the cited passage. The UI renders Markdown headings, emphasis, lists, and tables, while keeping legal citation links tied to server-returned sources. References are collapsed by default and expand when a citation is selected. Retrieved HTML, images, and unverified external links are not rendered. Users can inspect retrieved text and open its verified source.

Search failures are reported as unavailable, rather than answered from model memory. The system prompt treats retrieved material as data, addresses amendments/status, and instructs the agent to explain insufficient evidence.

## Configuration

The following settings are added to the API's `appsettings.json`:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<resource>.openai.azure.com/",
    "Deployment": "<chat-deployment>",
    "EmbeddingDeployment": "text-embedding-3-large",
    "ApiKey": ""
  },
  "AzureAISearch": {
    "Endpoint": "https://<search-service>.search.windows.net",
    "IndexName": "<existing-law-index>",
    "ApiKey": ""
  }
}
```

The demo always uses hybrid search. There is no separate `LegalChat` configuration section: agent name, output limit, search-call limit, and request timeout use fixed defaults in the code. Edit `LegalChat.md` to change the system prompt and rebuild. Dossier extraction and memo refinement have separate embedded prompt files.

Keep API keys in environment variables or ignored local configuration. In **Development**, `appsettings.local.json` is loaded if present; environment variables override it. The supplied reference project's search key was reused only in this ignored local file. Existing Azure OpenAI settings were preserved. Without keys, both clients use `DefaultAzureCredential` (the identity needs the relevant Azure model/search access).

PowerShell environment examples:

```powershell
$env:AzureOpenAI__EmbeddingDeployment = "text-embedding-3-large"
$env:AzureAISearch__Endpoint = "https://<search-service>.search.windows.net"
$env:AzureAISearch__IndexName = "<existing-law-index>"
$env:AzureAISearch__ApiKey = "<query-key>"
```

Restart the API after configuration changes. `/api/health` exposes `legalSearchEnabled` and `legalChatEnabled`; these indicate configuration readiness, not a live connectivity test. Missing chat configuration leaves demo/JSON/template workflows available.

## API and browser behavior

```http
POST /api/dossiers/{id}/chat
Content-Type: application/json
```

```json
{
  "message": "ما الفرق بين الاعتراض والتظلم الضريبي؟",
  "history": [
    { "role": "user", "text": "اشرح قانون ضريبة الدخل" },
    { "role": "assistant", "text": "الإجابة السابقة..." }
  ]
}
```

The response has `answer`, `sources`, and `grounded`. `grounded` means citation identifiers were resolved against this turn's retrieved results. Only `user` and `assistant` history roles are accepted. Questions allow 4,000 characters; history allows 20 messages, 12,000 characters per message, and 60,000 characters total.

The browser retains complete recent turns within these limits. It supports follow-up questions, suggested questions, source inspection, stopping a request, and starting a new conversation. The chat is available after loading or uploading a dossier. Follow-up history belongs to the current file and resets when switching files. Chat is held in browser memory, not saved to disk, and is cleared by a page reload or a new conversation. The browser posts to `POST /api/dossiers/{id}/chat/stream` and renders SSE `delta` events as the agent generates text. The `done` event contains the final answer, sources, and grounding result; it replaces the draft after citation validation. Unverified drafts may therefore be replaced with the no-evidence message. Interrupted or failed answers are removed from conversation history and the question is restored for retry. `POST /api/dossiers/{id}/chat` remains available for a single JSON response. Memo generation continues to use SSE.

## Validation and references

Run `dotnet test` from `backend`, and `npm run lint` / `npm run build` from `frontend`. Chat tests use a fake model/search with the real MAF agent pipeline, so they run without Azure credentials. Existing domain/API/Word-export tests remain applicable.

For browser checks, build the backend first, then run `npx playwright install chromium` once and `npm run test:e2e` from `frontend`. These tests start isolated API/Vite servers with AI disabled, mock chat answers, and cover sources, follow-up history, error recovery, cancellation, mobile layout, and the real deterministic demo-to-Word workflow.

See Microsoft's [function-tool guidance](https://learn.microsoft.com/en-us/agent-framework/agents/tools/function-tools) and [hybrid-search overview](https://learn.microsoft.com/en-us/azure/search/hybrid-search-overview) for the underlying patterns.
