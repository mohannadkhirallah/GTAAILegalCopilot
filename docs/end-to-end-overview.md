# End-to-end application overview

## What the application does

GTA AI Legal Copilot is a browser-based tool for assembling Arabic responses to tax grievance petitions. A user loads a fictitious demonstration case, submits a structured JSON dossier, or uploads a PDF/DOCX petition for extraction. The application displays the dossier and its procedural, substantive, and financial analysis, then produces an Arabic response memo that can be exported as a right-to-left Word document.

The repository is organized as a React/TypeScript frontend and a .NET 9 minimal API, with a deterministic C# domain core and local JSON-file persistence. It has no database dependency. Azure OpenAI is optional: demos, JSON dossiers, deterministic analysis, and template-based memos work without it.

## Components and responsibilities

Dossier chat runs a Microsoft Agent Framework agent with the selected case facts, current deterministic analysis, saved memo, and available extracted document text. It searches the existing Qatar-law index for legal explanations and returns cited sources without changing the deterministic results. See [legal chat](legal-chat.md).

| Component | Responsibility |
| --- | --- |
| `frontend/` | Arabic right-to-left user interface; selects demos, uploads dossiers, requests analysis, consumes memo events, and links to the DOCX export. |
| `Gta.LegalCopilot.Api` | HTTP API, upload validation, server-sent-event (SSE) memo stream, static-file hosting, and error handling. |
| `Gta.LegalCopilot.Application` | Coordinates case analysis and memo composition using application ports. |
| `Gta.LegalCopilot.Domain` | Deterministic procedural, substantive, deadline, penalty, and financial rules; does not call AI or perform I/O. |
| `Gta.LegalCopilot.Infrastructure` | Local JSON/filesystem storage, document text extraction, demo catalog, optional Azure OpenAI clients, and Word rendering. |
| `Gta.LegalCopilot.Tests` | xUnit domain and API integration tests. |

## End-to-end workflow

1. **Start the application.** The API is served at `http://localhost:5080`. During development, Vite serves the frontend at `http://localhost:5173` and proxies `/api` calls to the API. For a single-host run, build the frontend into the API's `wwwroot` directory and run the API.
2. **Choose a source dossier.** The user selects one of the three fictitious demos (`siemens`, `dohaTech`, `lusail`), uploads one JSON dossier, or uploads PDF/DOCX petition documents. Uploads allow up to five files, each no larger than the configured 20 MiB default.
3. **Validate and normalize the input.** The API checks uploaded file extensions and signatures, reads JSON directly or extracts PDF/DOCX text, obtains a dossier from the optional Azure structured-output extractor when needed, validates the dossier, assigns its ID, archives the original uploads, and saves the dossier.
4. **Analyze the case.** C# domain services calculate procedural admissibility and deadlines, apply configured deterministic rules to disputed items and evidence indicators, and recalculate tax and penalties. These outcomes and the generated dates and amounts do not depend on the LLM.
5. **Review and generate a memo.** The UI displays the dossier and the three analysis panels. On request, the API streams memo sections over SSE. Templates generate the memo; if Azure narrative generation is enabled, it may rephrase only the *Facts* and *Substantive defense* prose. A numeric-integrity guard rejects rewrites that introduce numbers absent from the deterministic draft and restores the draft. Formal pleas, financial impact, and requests remain deterministic.
6. **Save and export.** The completed memo is saved as JSON. The user can fetch the latest memo or download a generated Arabic RTL `.docx` containing memo sections and tables for determinations and financial comparison.

## AI and deterministic behavior

Azure OpenAI serves two independent, optional purposes:

- **Document extraction:** PDF/DOCX text is sent to a configured model to transcribe dossier facts into a strict JSON schema. The model is instructed not to calculate dates, totals, or penalties; the application recomputes applicable values in C#. Without Azure configuration, PDF/DOCX petition parsing is unavailable. JSON and demo inputs remain available.
- **Narrative refinement:** Only two memo prose sections can be sent for linguistic refinement. When AI is disabled, unavailable, fails, or produces numbers outside the deterministic draft, the application uses the deterministic template text.

AI availability is reported by `GET /api/health`. An Azure endpoint and deployment are needed to enable AI. Authentication can use an API key or `DefaultAzureCredential` (for example, a configured developer identity or managed identity with access to the deployment).

## Inputs, outputs, and local files

### Input types

- Built-in demo cases with fictitious taxpayer data.
- A structured JSON dossier matching the application's `DisputeDossier` model.
- PDF and DOCX petitions (require Azure OpenAI for dossier extraction).

PDF/DOCX text extraction reads embedded text; scanned-image PDFs may need OCR/preprocessing before upload. Extracted petition text is limited to 120,000 characters. A JSON dossier may be uploaded with PDF/DOCX files as attachments; when JSON is present it supplies the dossier, while the other uploaded documents are archived rather than extracted.

### Persisted data

By default, files are stored under `./data`, relative to the API's working directory (`backend/src/Gta.LegalCopilot.Api` when following the local run instructions):

| Path | Contents |
| --- | --- |
| `data/dossiers/*.json` | Normalized dossier records. |
| `data/memos/*.json` | Latest assembled memo records. |
| `data/uploads/{dossierId}/` | Uploaded source files. |
| `data/exports/{dossierId}/` | Generated DOCX exports. |

`Storage:RootPath` changes the root directory. The API uses a short-lived in-memory cache in addition to the JSON files. Keep the data directory backed up and protected according to the sensitivity of the uploaded taxpayer information.

## API route summary

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/health` | API status and AI extraction/narrative availability. |
| `GET` | `/api/demo-cases` | List demos. |
| `POST` | `/api/demo-cases/{key}/load` | Save and return a demo dossier. |
| `GET` | `/api/dossiers` | List saved dossiers. |
| `GET` | `/api/dossiers/{id}` | Retrieve a dossier. |
| `POST` | `/api/dossiers` | Create a dossier from JSON. |
| `POST` | `/api/dossiers/upload` | Upload PDF, DOCX, and/or JSON files as multipart form data. |
| `POST` | `/api/dossiers/{id}/analysis` | Run analysis and return its results. |
| `GET` | `/api/dossiers/{id}/memo/stream?useAi=true` | Stream analysis and memo events using SSE. |
| `POST` | `/api/dossiers/{id}/memo?useAi=true` | Build a memo without SSE. |
| `GET` | `/api/dossiers/{id}/memo` | Retrieve the latest saved memo. |
| `GET` | `/api/dossiers/{id}/memo/docx` | Render and download a Word memo. |

## Legal and deployment considerations

The configured statutory parameters and substantive policy shares are application inputs, not legal advice. In particular, the default admissible shares for evidenced management-fee and transfer-pricing claims are policy placeholders that the GTA Legal Department must confirm. The legal corpus, statutory summaries, demo cases, and generated outputs require review by qualified counsel.

The API currently configures CORS but does not implement authentication or authorization. Local development assumes a trusted machine. Do not expose this application or taxpayer data to an untrusted network without an independently reviewed security, identity, access-control, privacy, and deployment design. When Azure extraction is enabled, petition text is sent to the configured Azure OpenAI resource; verify organizational approval and data-handling requirements before using real taxpayer information.
