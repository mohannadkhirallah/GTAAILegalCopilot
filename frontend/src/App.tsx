import { useCallback, useEffect, useRef, useState } from 'react'
import { api, streamMemo } from './api'
import { FinancialPanel, ProceduralPanel, SubstantivePanel } from './components/AnalysisPanels'
import { DossierPanel } from './components/DossierPanel'
import { MemoPanel } from './components/MemoPanel'
import { Sidebar } from './components/Sidebar'
import type { AssembledMemo, CaseAnalysis, DemoCaseInfo, DisputeDossier, DossierSummary, Health, MemoSectionState } from './types'

export default function App() {
  const [health, setHealth] = useState<Health>()
  const [demos, setDemos] = useState<DemoCaseInfo[]>([])
  const [dossiers, setDossiers] = useState<DossierSummary[]>([])
  const [dossier, setDossier] = useState<DisputeDossier>()
  const [analysis, setAnalysis] = useState<CaseAnalysis>()
  const [sections, setSections] = useState<MemoSectionState[]>([])
  const [memo, setMemo] = useState<AssembledMemo>()
  const [busy, setBusy] = useState(false)
  const [streaming, setStreaming] = useState(false)
  const [useAi, setUseAi] = useState(true)
  const [error, setError] = useState<string>()
  const closeStream = useRef<() => void>(undefined)

  const refreshList = useCallback(() => api.dossiers().then(setDossiers).catch(() => undefined), [])

  useEffect(() => {
    api.health().then(setHealth).catch(() => setError('تعذر الاتصال بالخادم.'))
    api.demoCases().then(setDemos).catch(() => undefined)
    refreshList()
    return () => closeStream.current?.()
  }, [refreshList])

  const open = useCallback(async (load: () => Promise<DisputeDossier>) => {
    closeStream.current?.()
    setBusy(true)
    setError(undefined)
    setSections([])
    setMemo(undefined)
    setStreaming(false)
    try {
      const d = await load()
      setDossier(d)
      setAnalysis(await api.analyze(d.dossierId))
      refreshList()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }, [refreshList])

  const generate = () => {
    if (!dossier) return
    closeStream.current?.()
    setSections([])
    setMemo(undefined)
    setError(undefined)
    setStreaming(true)
    const patch = (key: string, fn: (s: MemoSectionState) => MemoSectionState) =>
      setSections((prev) => prev.map((s) => (s.key === key ? fn(s) : s)))
    closeStream.current = streamMemo(dossier.dossierId, useAi, {
      onAnalysis: setAnalysis,
      onSectionStart: (key, title) => setSections((prev) => [...prev, { key, title, text: '', done: false }]),
      onDelta: (key, text) => patch(key, (s) => ({ ...s, text: s.text + text })),
      onSectionReplace: (key, text) => patch(key, (s) => ({ ...s, text })),
      onSectionEnd: (key) => patch(key, (s) => ({ ...s, done: true })),
      onMemo: setMemo,
      onError: (m) => { setError(m); setStreaming(false) },
      onDone: () => setStreaming(false),
    })
  }

  return (
    <div className="min-h-screen bg-slate-100 text-slate-900">
      <header className="bg-maroon-700 text-white shadow">
        <div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-4">
          <div>
            <div className="text-xs opacity-80">دولة قطر · الهيئة العامة للضرائب · إدارة ضريبة الدخل</div>
            <h1 className="text-xl font-bold">المساعد القانوني الذكي للتظلمات الضريبية</h1>
          </div>
          <div className="text-left text-xs opacity-90">
            <div>قانون ضريبة الدخل رقم (24) لسنة 2018</div>
            <div>
              Azure OpenAI:{' '}
              <b>{health ? (health.aiNarrativeEnabled ? 'مفعّل' : 'غير مهيأ — وضع القوالب الحتمية') : '…'}</b>
            </div>
          </div>
        </div>
      </header>

      <main className="mx-auto grid max-w-7xl gap-6 px-6 py-6 lg:grid-cols-[300px_1fr]">
        <Sidebar
          demos={demos}
          dossiers={dossiers}
          activeId={dossier?.dossierId}
          busy={busy || streaming}
          aiExtraction={!!health?.aiExtractionEnabled}
          onDemo={(key) => open(() => api.loadDemo(key))}
          onUpload={(files) => open(() => api.upload(files))}
          onSelect={(id) => open(() => api.dossier(id))}
        />

        <div className="flex min-w-0 flex-col gap-6">
          {error && <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</div>}
          {busy && <div className="text-sm text-slate-500">جارٍ التحميل والتحليل…</div>}
          {!dossier && !busy && (
            <div className="rounded-xl border border-dashed border-slate-300 bg-white p-10 text-center text-slate-500">
              اختر حالة تجريبية أو ارفع صحيفة تظلم للبدء.
            </div>
          )}
          {dossier && <DossierPanel dossier={dossier} />}
          {analysis && (
            <>
              <ProceduralPanel analysis={analysis} />
              <SubstantivePanel analysis={analysis} />
              <FinancialPanel analysis={analysis} />
            </>
          )}
          {dossier && (
            <MemoPanel
              sections={sections}
              memo={memo}
              streaming={streaming}
              aiAvailable={!!health?.aiNarrativeEnabled}
              useAi={useAi}
              onToggleAi={setUseAi}
              onGenerate={generate}
              docxUrl={api.docxUrl(dossier.dossierId)}
            />
          )}
        </div>
      </main>
    </div>
  )
}
