# GTA AI Legal Copilot — المساعد القانوني الذكي للتظلمات الضريبية

Production-grade tax-dispute response system for the **General Tax Authority (الهيئة العامة للضرائب), State of Qatar — Income Tax Department**.
It ingests taxpayer grievance petitions against assessment notices and produces court-ready Arabic rebuttal memos:
«مذكرة رد وتعقيب موضوعي وشكلي على التظلم الضريبي» addressed to «السيد / رئيس لجنة التظلم الضريبي المحترم،،،».

Governing law: Income Tax Law No. (24) of 2018, Executive Regulations (Decision No. 39 of 2019), Cabinet Decision No. (38) of 2020.

## Architecture

The dossier chat uses extracted case facts, current analysis, and saved memo context. Its Microsoft Agent Framework agent searches the Qatar-law Azure AI Search index for legal questions, using hybrid keyword/vector retrieval (`Content` / 3072-dimension `ContentVector`). It streams answers, supports follow-ups within the selected file, and displays legal sources. See [chat setup and architecture](docs/legal-chat.md).

```
backend/                         .NET 9 / C# 13 — Clean Architecture
  src/Gta.LegalCopilot.Domain          Models + deterministic engines (no I/O, no AI)
    Services/StatutoryDeadlineCalculator   Arts. 17/18/19 calendar-day deadlines
    Services/ProceduralAdmissibilityService Public-order (النظام العام) admissibility screen
    Services/DelayPenaltyCalculator        Art. 24 — 1.5%/month (or part), capped at 100% of principal
    Services/SubstantiveRuleEngine         Item classification + GTA determination (IFRS 9, fair value, mgmt fees, WHT, TP)
    Services/FinancialRecalculationService Revised tax / penalties / ledger matrix
  src/Gta.LegalCopilot.Application     Ports, CaseAnalysisService, MemoComposer (templates), MemoOrchestrator (SSE), NumericIntegrityGuard
  src/Gta.LegalCopilot.Infrastructure  Local ./data storage + IMemoryCache, PdfPig/OpenXml extraction, Word renderer, Azure OpenAI, demo cases
  src/Gta.LegalCopilot.Api             Minimal API endpoints, SSE streaming, ProblemDetails
  tests/Gta.LegalCopilot.Tests         xUnit domain + API integration tests
frontend/                        React 19 + TypeScript + Vite + Tailwind CSS 4 (Arabic RTL)
```

### Inviolable directives — how they are enforced

| Directive | Enforcement |
|---|---|
| Deterministic-LLM isolation | All dates, deadlines, penalties, totals and item determinations are computed in C# domain services. The LLM is only used to (a) transcribe facts from uploaded petitions via strict JSON-schema structured output and (b) rephrase the *Facts* and *Substantive* prose. Deadlines and totals of extracted dossiers are recomputed in C#. `NumericIntegrityGuard` rejects any LLM rewrite that contains a number not present in the deterministic draft and falls back to the template. The formal plea, financial section and requests are never sent to the LLM. |
| Public-order defense | If no Art. (18) objection was filed within 30 days (or the grievance was filed early/late under Art. 19), the verdict is inadmissible and the primary request is «الحكم بعدم قبول التظلم شكلاً لفوات المواعيد وتخطي مرحلة الاعتراض الإداري الوجوبية». |
| Alternative reserve defense | Whenever the formal plea is raised, substantive rebuttal and recalculation are presented «على سبيل الاحتياط الكلي» (`ALTERNATIVE_RESERVE`). |
| Input flexibility | Multipart upload of PDF / DOCX (AI extraction) or a JSON dossier, plus three instant demo cases: `siemens`, `dohaTech`, `lusail`. |

## Running locally

Prerequisites: .NET 9 SDK, Node.js 20+.

```bash
# API (http://localhost:5080) — works without any Azure configuration (deterministic template mode)
cd backend/src/Gta.LegalCopilot.Api
dotnet run

# Frontend (http://localhost:5173, proxies /api to :5080)
cd frontend
npm install
npm run dev
```

Single-host deployment: `npm run build:api` writes the SPA into the API's `wwwroot/`, which the API serves.

### Azure OpenAI (optional)

Set via environment variables or user secrets (never commit keys):

```bash
export AzureOpenAI__Endpoint="https://<resource>.openai.azure.com/"
export AzureOpenAI__Deployment="gpt-4o"
export AzureOpenAI__ApiKey="<key>"   # omit to use DefaultAzureCredential / Managed Identity
```

Without an endpoint, PDF/DOCX extraction returns `503` (JSON dossiers and demo cases still work) and memos use deterministic templates.

### Local storage

Everything lives under `Storage:RootPath` (default `./data`, relative to the API working directory):
`dossiers/*.json`, `memos/*.json`, `uploads/{dossierId}/…`, `exports/{dossierId}/*.docx`. No database is required.

## API

`POST /api/dossiers/{id}/chat` answers a question about a stored dossier; `POST /api/dossiers/{id}/chat/stream` streams it with optional recent history and final legal source references. `/api/health` also reports `legalSearchEnabled` and `legalChatEnabled`.

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/health` | Status and AI availability |
| GET | `/api/demo-cases` | List demo cases |
| POST | `/api/demo-cases/{key}/load` | Load `siemens` / `dohaTech` / `lusail` |
| GET | `/api/dossiers` | List stored dossiers |
| GET | `/api/dossiers/{id}` | Get a dossier |
| POST | `/api/dossiers` | Create from JSON body |
| POST | `/api/dossiers/upload` | Multipart `files` (PDF, DOCX, JSON; ≤5 files, ≤20 MB each) |
| POST | `/api/dossiers/{id}/analysis` | Procedural + substantive + financial analysis |
| GET | `/api/dossiers/{id}/memo/stream?useAi=true` | SSE: `analysis`, `section_start`, `delta`, `section_replace`, `section_end`, `memo`, `done`, `error` |
| POST | `/api/dossiers/{id}/memo?useAi=true` | Build memo (non-streaming) |
| GET | `/api/dossiers/{id}/memo` | Latest memo |
| GET | `/api/dossiers/{id}/memo/docx` | Export Word (RTL) memo |

## Configuration (`appsettings.json`)

- `Statutory`: objection window (30), GTA decision window (60), grievance window (30), monthly penalty rate (0.015), penalty cap ratio (1.0), corporate tax rate (0.10).
- `SubstantivePolicy`: admissible share for **evidenced** management fees and transfer-pricing claims (default 0.50 each). These are policy placeholders and must be confirmed by the GTA Legal Department before production use.

## Testing

```bash
cd backend && dotnet test        # domain + API integration tests (incl. OpenXML schema validation of the Word memo)
cd frontend && npm run lint && npm run build
```

> Demo-case taxpayer data is fictitious. Statutory summaries and the jurisprudence principle are curated references in `LegalCorpus.cs` and should be reviewed by GTA legal counsel.
