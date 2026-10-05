import type {
  CaseAnalysis,
  DemoCaseInfo,
  DisputeDossier,
  DossierSummary,
  Health,
} from "./types";

import { request } from "./http";

export const api = {
  health: () => request<Health>("/api/health"),
  demoCases: () => request<DemoCaseInfo[]>("/api/demo-cases"),
  loadDemo: (key: string) =>
    request<DisputeDossier>(`/api/demo-cases/${encodeURIComponent(key)}/load`, {
      method: "POST",
    }),
  dossiers: () => request<DossierSummary[]>("/api/dossiers"),
  dossier: (id: string) =>
    request<DisputeDossier>(`/api/dossiers/${encodeURIComponent(id)}`),
  analyze: (id: string) =>
    request<CaseAnalysis>(`/api/dossiers/${encodeURIComponent(id)}/analysis`, {
      method: "POST",
    }),
  upload: (files: File[]) => {
    const form = new FormData();
    files.forEach((f) => form.append("files", f));
    return request<DisputeDossier>("/api/dossiers/upload", {
      method: "POST",
      body: form,
    });
  },
  docxUrl: (id: string) => `/api/dossiers/${encodeURIComponent(id)}/memo/docx`,
};

export { streamMemo } from "./memoStream";
export type { MemoStreamHandlers } from "./memoStream";
