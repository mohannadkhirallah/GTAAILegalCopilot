import type { AssembledMemo, CaseAnalysis, DemoCaseInfo, DisputeDossier, DossierSummary, Health } from './types'

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, init)
  if (!res.ok) {
    let message = `${res.status} ${res.statusText}`
    try {
      const body = await res.json()
      message = body.title ?? body.error ?? message
      if (Array.isArray(body.errors) && body.errors.length) message += ': ' + body.errors.join('، ')
    } catch { /* non-JSON error body */ }
    throw new Error(message)
  }
  return res.json() as Promise<T>
}

export const api = {
  health: () => request<Health>('/api/health'),
  demoCases: () => request<DemoCaseInfo[]>('/api/demo-cases'),
  loadDemo: (key: string) => request<DisputeDossier>(`/api/demo-cases/${encodeURIComponent(key)}/load`, { method: 'POST' }),
  dossiers: () => request<DossierSummary[]>('/api/dossiers'),
  dossier: (id: string) => request<DisputeDossier>(`/api/dossiers/${encodeURIComponent(id)}`),
  analyze: (id: string) => request<CaseAnalysis>(`/api/dossiers/${encodeURIComponent(id)}/analysis`, { method: 'POST' }),
  upload: (files: File[]) => {
    const form = new FormData()
    files.forEach((f) => form.append('files', f))
    return request<DisputeDossier>('/api/dossiers/upload', { method: 'POST', body: form })
  },
  docxUrl: (id: string) => `/api/dossiers/${encodeURIComponent(id)}/memo/docx`,
}

export interface MemoStreamHandlers {
  onAnalysis: (a: CaseAnalysis) => void
  onSectionStart: (key: string, title: string) => void
  onDelta: (key: string, text: string) => void
  onSectionReplace: (key: string, text: string) => void
  onSectionEnd: (key: string) => void
  onMemo: (memo: AssembledMemo) => void
  onError: (message: string) => void
  onDone: () => void
}

/** Opens the SSE memo stream. Returns a disposer that closes the connection. */
export function streamMemo(id: string, useAi: boolean, h: MemoStreamHandlers): () => void {
  const es = new EventSource(`/api/dossiers/${encodeURIComponent(id)}/memo/stream?useAi=${useAi}`)
  const parse = (e: MessageEvent) => JSON.parse(e.data)
  es.addEventListener('analysis', (e) => h.onAnalysis(parse(e as MessageEvent).payload))
  es.addEventListener('section_start', (e) => { const d = parse(e as MessageEvent); h.onSectionStart(d.section, d.text) })
  es.addEventListener('delta', (e) => { const d = parse(e as MessageEvent); h.onDelta(d.section, d.text) })
  es.addEventListener('section_replace', (e) => { const d = parse(e as MessageEvent); h.onSectionReplace(d.section, d.text) })
  es.addEventListener('section_end', (e) => h.onSectionEnd(parse(e as MessageEvent).section))
  es.addEventListener('memo', (e) => h.onMemo(parse(e as MessageEvent).payload))
  es.addEventListener('error', (e) => {
    const data = (e as MessageEvent).data
    h.onError(data ? JSON.parse(data).message : 'انقطع الاتصال بالخادم أثناء إعداد المذكرة.')
    es.close()
  })
  es.addEventListener('done', () => { h.onDone(); es.close() })
  return () => es.close()
}
