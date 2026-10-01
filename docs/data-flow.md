# Data-flow diagram

The diagram distinguishes deterministic processing from optional AI calls. Azure OpenAI is only used for petition extraction and (when selected) limited narrative refinement. All case decisions and financial calculations are performed by the C# domain services.

```mermaid
flowchart TD
    U[User] --> UI[React and TypeScript browser UI]
    UI -->|GET health, demos, dossier list| API[.NET 9 minimal API]
    API -->|health and demo catalogue| INFRA[Infrastructure services]
    INFRA -->|health flags and demo cases| API
    API --> UI

    U -->|Select demo| UI
    UI -->|POST demo-cases / load| API
    API -->|Create demo dossier| INFRA

    U -->|Upload JSON, PDF or DOCX| UI
    UI -->|multipart upload| API
    API --> VALIDATE[Validate type, size and file signature]
    VALIDATE -->|JSON dossier| NORMALIZE[Normalize and validate dossier]
    VALIDATE -->|PDF/DOCX| TEXT[Extract embedded document text]
    TEXT -->|petition text| AZUREX[Optional Azure OpenAI structured extraction]
    AZUREX -->|dossier facts; no computed dates/totals| NORMALIZE
    NORMALIZE -->|invalid input| ERR[Validation error]
    NORMALIZE -->|valid dossier| REPO[Local JSON repository and file store]
    REPO -->|dossier record, source uploads| DISK[(Storage root: dossiers, uploads, memos, exports)]
    ERR --> API
    REPO --> API
    API -->|created dossier| UI

    UI -->|POST dossier analysis| API
    API -->|load dossier| REPO
    REPO -->|dossier| ANALYSIS[Deterministic C# domain analysis]
    ANALYSIS --> PROC[Procedural admissibility and deadlines]
    ANALYSIS --> SUB[Substantive item/evidence determinations]
    ANALYSIS --> FIN[Tax, penalty and ledger recalculation]
    PROC --> RESULT[Analysis results]
    SUB --> RESULT
    FIN --> RESULT
    RESULT --> API
    API -->|procedural, substantive, financial panels| UI

    U -->|Generate memo, optionally use AI| UI
    UI -->|GET memo/stream?useAi=...| API
    API --> ORCH[Memo orchestrator and deterministic templates]
    ANALYSIS -.->|dossier analysis| ORCH
    ORCH -->|Facts and substantive prose drafts only| AZUREN[Optional Azure OpenAI narrative refinement]
    AZUREN -->|candidate prose| GUARD[Numeric integrity guard]
    GUARD -->|faithful candidate| ORCH
    GUARD -->|foreign numbers or failure: use original draft| ORCH
    ORCH -->|formal plea, financial impact, requests stay deterministic| MEMO[Assembled memo]
    ORCH -->|section events and completed memo| API
    API -->|SSE analysis, section, delta, replace, memo, done events| UI
    MEMO --> REPO
    REPO --> DISK

    U -->|Export Word| UI
    UI -->|GET memo/docx| API
    API -->|latest memo or deterministic memo| REPO
    REPO -->|memo sections and analysis| WORD[Arabic RTL DOCX renderer]
    WORD -->|DOCX export| DISK
    WORD -->|download| UI
```

## Flow notes

- The browser talks to the API for dossier operations and subscribes to the memo stream with `EventSource`/SSE.
- PDF/DOCX uploads pass through extension, size, and file-signature validation before text extraction. The text is sent to Azure OpenAI only if the extractor is enabled. JSON uploads bypass text extraction and AI extraction.
- With JSON and PDF/DOCX in the same upload, the JSON is the dossier source and the other files are archived as attachments.
- `CaseAnalysisService` invokes deterministic domain services. AI does not determine procedural admissibility, dates, statutory deadlines, item outcomes, penalties, or totals.
- The memo templates retain deterministic formal plea, financial-impact, and requests sections. Only the Facts and Substantive defense sections are eligible for Azure rephrasing. The numeric guard enforces fallback to template text if the candidate is empty or contains an unapproved number.
- Dossiers, latest memo JSON, uploaded source files, and generated DOCX exports are stored beneath `Storage:RootPath` (default `./data` from the API working directory). An in-memory cache accelerates reads; it is not the persistent record.
