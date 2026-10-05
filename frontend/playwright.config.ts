import { defineConfig } from "@playwright/test";
import { tmpdir } from "node:os";
import { join } from "node:path";

export default defineConfig({
  testDir: "./tests",
  fullyParallel: false,
  workers: 1,
  timeout: 30000,
  use: { baseURL: "http://127.0.0.1:5176", browserName: "chromium" },
  webServer: [
    {
      command:
        "dotnet run --no-build --project ../backend/src/Gta.LegalCopilot.Api -- --urls http://127.0.0.1:5082",
      url: "http://127.0.0.1:5082/api/health",
      env: {
        AzureOpenAI__Enabled: "false",
        Storage__RootPath: join(tmpdir(), `gta-browser-tests-${process.pid}`),
      },
      timeout: 60000,
    },
    {
      command: "npm run dev -- --host 127.0.0.1 --port 5176 --strictPort",
      url: "http://127.0.0.1:5176",
      env: { API_URL: "http://127.0.0.1:5082" },
      timeout: 30000,
    },
  ],
});
