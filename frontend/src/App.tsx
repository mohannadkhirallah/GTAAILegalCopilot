import { useCallback, useEffect, useRef, useState } from "react";
import { api, streamMemo } from "./api";
import {
  FinancialPanel,
  ProceduralPanel,
  SubstantivePanel,
} from "./components/AnalysisPanels";
import { DossierPanel } from "./components/DossierPanel";
import { MemoPanel } from "./components/MemoPanel";
import { Sidebar } from "./components/Sidebar";
import { UiIcon } from "./components/UiIcon";
import type {
  AssembledMemo,
  CaseAnalysis,
  DemoCaseInfo,
  DisputeDossier,
  DossierSummary,
  Health,
  MemoSectionState,
} from "./types";

export default function App() {
  const [health, setHealth] = useState<Health>();
  const [demos, setDemos] = useState<DemoCaseInfo[]>([]);
  const [dossiers, setDossiers] = useState<DossierSummary[]>([]);
  const [dossier, setDossier] = useState<DisputeDossier>();
  const [analysis, setAnalysis] = useState<CaseAnalysis>();
  const [sections, setSections] = useState<MemoSectionState[]>([]);
  const [memo, setMemo] = useState<AssembledMemo>();
  const [busy, setBusy] = useState(false);
  const [streaming, setStreaming] = useState(false);
  const [useAi, setUseAi] = useState(true);
  const [error, setError] = useState<string>();
  const closeStream = useRef<() => void>(undefined);

  const refreshList = useCallback(
    () =>
      api
        .dossiers()
        .then(setDossiers)
        .catch(() => undefined),
    [],
  );

  useEffect(() => {
    api
      .health()
      .then(setHealth)
      .catch(() => setError("تعذر الاتصال بالخادم."));
    api
      .demoCases()
      .then(setDemos)
      .catch(() => undefined);
    refreshList();
    return () => closeStream.current?.();
  }, [refreshList]);

  const open = useCallback(
    async (load: () => Promise<DisputeDossier>) => {
      closeStream.current?.();
      setBusy(true);
      setError(undefined);
      setSections([]);
      setMemo(undefined);
      setStreaming(false);
      try {
        const d = await load();
        setDossier(d);
        setAnalysis(await api.analyze(d.dossierId));
        refreshList();
      } catch (e) {
        setError((e as Error).message);
      } finally {
        setBusy(false);
      }
    },
    [refreshList],
  );

  const generate = () => {
    if (!dossier) return;
    closeStream.current?.();
    setSections([]);
    setMemo(undefined);
    setError(undefined);
    setStreaming(true);
    const patch = (
      key: string,
      fn: (s: MemoSectionState) => MemoSectionState,
    ) => setSections((prev) => prev.map((s) => (s.key === key ? fn(s) : s)));
    closeStream.current = streamMemo(dossier.dossierId, useAi, {
      onAnalysis: setAnalysis,
      onSectionStart: (key, title) =>
        setSections((prev) => [...prev, { key, title, text: "", done: false }]),
      onDelta: (key, text) =>
        patch(key, (s) => ({ ...s, text: s.text + text })),
      onSectionReplace: (key, text) => patch(key, (s) => ({ ...s, text })),
      onSectionEnd: (key) => patch(key, (s) => ({ ...s, done: true })),
      onMemo: setMemo,
      onError: (m) => {
        setError(m);
        setStreaming(false);
      },
      onDone: () => setStreaming(false),
    });
  };

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="page-container header-content">
          <div className="brand">
            <div className="brand-mark" aria-hidden="true">
              قطر
            </div>
            <div className="brand-copy">
              <div className="brand-badge">
                دولة قطر · الهيئة العامة للضرائب · إدارة ضريبة الدخل
              </div>
              <h1>المساعد القانوني الذكي للتظلمات الضريبية</h1>
            </div>
          </div>
          <nav className="workflow-nav" aria-label="أقسام مساحة العمل">
            <a
              href="#workspace-intake"
              className={`workflow-step ${!dossier ? "workflow-step-current" : ""}`}
              aria-current={!dossier ? "step" : undefined}
            >
              <span className="step-number">1</span> رفع صحيفة التظلم
            </a>
            <span className="step-arrow" aria-hidden="true">
              ←
            </span>
            <a
              href="#workspace-analysis"
              className={`workflow-step ${dossier && sections.length === 0 ? "workflow-step-current" : ""}`}
              aria-current={
                dossier && sections.length === 0 ? "step" : undefined
              }
            >
              <span className="step-number">2</span> الفحص الشكلي والموضوعي
            </a>
            <span className="step-arrow" aria-hidden="true">
              ←
            </span>
            <a
              href="#workspace-memo"
              className={`workflow-step ${sections.length > 0 ? "workflow-step-current" : ""}`}
              aria-current={sections.length > 0 ? "step" : undefined}
            >
              <span className="step-number">3</span> إعداد المذكرة
            </a>
          </nav>
        </div>
      </header>

      <main className="page-container workspace-main">
        <section className="workspace-intro">
          <div className="intro-copy">
            <div className="intro-badge">
              <UiIcon name="scales" /> الهيئة العامة للضرائب · إدارة ضريبة الدخل
            </div>
            <h2>المساعد القانوني الذكي للتظلمات الضريبية</h2>
            <p>مذكرة رد وتعقيب موضوعي وشكلي على التظلم الضريبي</p>
          </div>
          <div className="intro-status">
            <div className="intro-law">
              قانون ضريبة الدخل رقم (24) لسنة 2018
            </div>
            <div className="ai-status">
              {/* <UiIcon name="sparkles" /> */}
              <span>
                Azure OpenAI:{" "}
                <b>
                  {health
                    ? health.aiNarrativeEnabled
                      ? "مفعّل"
                      : "غير مهيأ — وضع القوالب الحتمية"
                    : "…"}
                </b>
              </span>
            </div>
          </div>
          <UiIcon name="scales" className="intro-watermark" />
        </section>

        <div id="workspace-intake" className="workspace-grid">
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

          <div className="workspace-content">
            {error && (
              <div className="status-message status-error">{error}</div>
            )}
            {busy && (
              <div className="status-message status-loading">
                <span className="loading-dot" aria-hidden="true" />
                جارٍ التحميل والتحليل…
              </div>
            )}
            {!dossier && !busy && (
              <div className="workspace-empty">
                <span className="empty-icon">
                  <UiIcon name="document" />
                </span>
                <p>اختر حالة تجريبية أو ارفع صحيفة تظلم للبدء.</p>
              </div>
            )}
            {dossier && <DossierPanel dossier={dossier} />}
            <div id="workspace-analysis" className="analysis-panels">
              {analysis && (
                <>
                  <ProceduralPanel analysis={analysis} />
                  <SubstantivePanel analysis={analysis} />
                  <FinancialPanel analysis={analysis} />
                </>
              )}
            </div>
            <div id="workspace-memo" className="memo-workbench">
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
          </div>
        </div>
      </main>
      <footer className="app-footer">
        <div className="page-container footer-content">
          <div className="footer-brand">
            <span className="footer-mark" aria-hidden="true">
              قطر
            </span>
            دولة قطر · الهيئة العامة للضرائب · إدارة ضريبة الدخل
          </div>
          <span>قانون ضريبة الدخل رقم (24) لسنة 2018</span>
        </div>
      </footer>
    </div>
  );
}
