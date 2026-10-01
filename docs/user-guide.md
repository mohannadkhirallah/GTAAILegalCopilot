# Prerequisites and user guide

## Prerequisites

### Required for local development

- Git and a checkout of this repository.
- .NET 9 SDK. The backend targets `net9.0`; `backend/global.json` requests SDK `9.0.100` with latest-feature roll-forward.
- Node.js 20 or later and npm.
- A supported desktop browser with access to the local API and frontend ports.

No database, Azure account, or API key is required to run the demo cases and JSON-dossier workflow in deterministic/template mode.

### Optional for PDF/DOCX extraction or AI narrative refinement

- An Azure OpenAI resource with a deployed chat model.
- Its resource endpoint and deployment name.
- One authentication method:
  - an Azure OpenAI API key, or
  - credentials supported by `DefaultAzureCredential`, such as a signed-in developer identity or a managed identity authorized to use the deployed model.

AI is optional overall, but it is required to extract a dossier from PDF/DOCX petitions. When it is not configured, upload a JSON dossier or choose a demo instead. Do not commit credentials to the repository.

## Run locally in development mode

Open two terminals from the repository root.

**Terminal 1 — API**

```bash
cd backend/src/Gta.LegalCopilot.Api
dotnet run
```

The API listens at `http://localhost:5080` using the repository's HTTP launch profile. On first run, .NET restores the project dependencies. Its default local data directory is `./data` relative to this working directory.

**Terminal 2 — frontend**

```bash
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`. The Vite development server forwards `/api` requests to `http://localhost:5080`. If the API is running elsewhere, set `API_URL` for the Vite process to the API base URL before starting the frontend.

### Optional: configure Azure OpenAI

Set configuration with environment variables before starting the API. The `__` separator maps to nested .NET configuration keys:

```bash
export AzureOpenAI__Endpoint="https://<resource>.openai.azure.com/"
export AzureOpenAI__Deployment="<deployment-name>"
export AzureOpenAI__ApiKey="<key>"
```

When using `DefaultAzureCredential`, omit `AzureOpenAI__ApiKey` and ensure the developer or managed identity can access the deployment. Restart the API after changing its environment. Check the UI status or request `GET http://localhost:5080/api/health`; both `aiExtractionEnabled` and `aiNarrativeEnabled` should be `true` when configured.

The optional key may also be supplied through .NET user secrets for the API project. Keep secrets out of committed files, shell history where avoidable, logs, and screenshots.

### Optional: single-host build

To build the SPA into the API's static-file directory:

```bash
cd frontend
npm install
npm run build:api
cd ../backend/src/Gta.LegalCopilot.Api
dotnet run
```

Open `http://localhost:5080`. This configuration serves both the SPA and API from one host and does not require the Vite development server.

## Use the application

1. **Check connection and AI status.** The header reports whether Azure narrative refinement is available. The frontend also requests API health at startup.
2. **Load a demo or provide a dossier.**
   - For a quick walkthrough, select a demo case from the left panel. The available cases cover a procedural issue (`siemens`), substantive disputes (`dohaTech`), and withholding-tax/transfer-pricing issues (`lusail`). Demo taxpayer data is fictitious.
   - To upload a structured dossier, choose a JSON file and select **رفع وتحليل** (upload and analyze). [`sample-dossier.json`](sample-dossier.json) is a fictitious example that can be used as a starting point.
   - To extract a dossier from petition text, choose PDF and/or DOCX files and select **رفع وتحليل**. Azure extraction must be enabled. Scanned documents may require text OCR before upload.
   - Upload supports `.pdf`, `.docx`, and `.json`, up to five files at once, with a default per-file size limit of 20 MiB. The API checks file signatures as well as extensions.
3. **Review the dossier.** Confirm the taxpayer, assessment notice and dates, disputed items, and original amounts. For AI-extracted data, check every field against the source petition and correct issues in the source JSON/re-upload as needed; this UI does not provide an inline dossier editor.
4. **Review the analysis.** Inspect the procedural, substantive, and financial panels before drafting. The domain layer applies its configured rules deterministically. Verify the legal basis, evidence status, dates, and financial results with qualified reviewers.
5. **Generate the response memo.** Select **إعداد المذكرة** (prepare memo). If Azure narrative refinement is enabled, the checkbox controls whether the two eligible prose sections may be refined. Otherwise the deterministic templates are used. The memo streams section by section.
6. **Export the Word document.** Once generation completes, select **تصدير Word**. The API renders the current saved memo as an Arabic RTL `.docx` file.
7. **Return to saved cases.** Select a dossier from the saved-files panel. Dossiers and their latest memos remain on disk under the configured storage root across API restarts.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Frontend reports it cannot connect | Confirm the API is running at `http://localhost:5080`; check `GET /api/health`. If using Vite with a different API address, set `API_URL` before starting Vite. |
| PDF/DOCX upload reports Azure is not configured | This is expected without a valid Azure OpenAI endpoint and deployment. Use a JSON dossier/demo, or configure Azure and restart the API. |
| AI shows unavailable after configuration | Check endpoint URL, deployment name, authentication credentials/permissions, and API logs. Confirm the API process inherited the environment variables. |
| An upload is rejected | Use a supported PDF, DOCX, or JSON file; ensure its contents match the extension; stay within five files and the configured size limit. |
| Extracted dossier data is incomplete or incorrect | Verify the petition has selectable text and that the source is within the text limit. Validate AI-extracted fields against the original; use a corrected JSON dossier when needed. |
| A date, legal position, or amount seems wrong | Do not rely on the generated draft without review. Confirm source facts, `Statutory` and `SubstantivePolicy` configuration, and current law with authorized legal and tax reviewers. |
| Saved records are missing | Check `Storage:RootPath` and the API's working directory; the default `./data` path is relative to the API process working directory. |

## Validate the checkout

Run the repository's existing checks:

```bash
cd backend
dotnet test

cd ../frontend
npm run lint
npm run build
```

## Important use limitation

This is an application demonstration, not a legal decision-maker. Confirm legal content, the current law, tax policy settings, source documents, and all output with qualified GTA reviewers. Protect real taxpayer records. The current API has no authentication/authorization and is not ready to expose to an untrusted network.
