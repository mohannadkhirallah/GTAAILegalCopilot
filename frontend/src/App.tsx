import { useEffect, useState } from "react";
import { api } from "./api";
import { useDossierWorkspace } from "./hooks/useDossierWorkspace";
import { LegalChatPanel } from "./chat/LegalChatPanel";
import {
  FinancialPanel,
  ProceduralPanel,
  SubstantivePanel,
} from "./components/AnalysisPanels";
import { DossierPanel } from "./components/DossierPanel";
import { MemoPanel } from "./components/MemoPanel";
import { Sidebar } from "./components/Sidebar";
import { UiIcon } from "./components/UiIcon";
import { OperationNotice } from "./components/OperationNotice";

const workflowSections = ["workspace-intake", "workspace-analysis", "workspace-memo"] as const;

export default function App() {
  const [chatDossierId, setChatDossierId] = useState<string>();
  const [activeSection, setActiveSection] = useState<string>(workflowSections[0]);
  const {
    health,
    demos,
    dossiers,
    dossier,
    analysis,
    sections,
    memo,
    busy,
    streaming,
    useAi,
    setUseAi,
    error,
    notice,
    dismissNotice,
    open,
    generate,
  } = useDossierWorkspace();

  useEffect(() => {
    let frame = 0;
    const updateSection = () => {
      frame = 0;
      const headerBottom = document.querySelector(".app-header")?.getBoundingClientRect().bottom ?? 0;
      const threshold = headerBottom + Math.min(120, (window.innerHeight - headerBottom) * 0.2);
      const atBottom = window.scrollY > 0 && window.scrollY + window.innerHeight >= document.documentElement.scrollHeight - 2;
      let current: string = workflowSections[0];
      for (const id of workflowSections) {
        const section = document.getElementById(id);
        if (section && section.getBoundingClientRect().height > 0 &&
          (section.getBoundingClientRect().top <= threshold || atBottom)) current = id;
      }
      setActiveSection(current);
    };
    const scheduleUpdate = () => {
      if (!frame) frame = window.requestAnimationFrame(updateSection);
    };
    const observer = new ResizeObserver(scheduleUpdate);
    for (const selector of [".app-header", ".workspace-main"]) {
      const element = document.querySelector(selector);
      if (element) observer.observe(element);
    }
    window.addEventListener("scroll", scheduleUpdate, { passive: true });
    window.addEventListener("resize", scheduleUpdate);
    scheduleUpdate();
    return () => {
      observer.disconnect();
      window.removeEventListener("scroll", scheduleUpdate);
      window.removeEventListener("resize", scheduleUpdate);
      window.cancelAnimationFrame(frame);
    };
  }, []);

  return (
    <div className="app-shell">
      <OperationNotice notice={notice} onDismiss={dismissNotice} />
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
              className={`workflow-step ${activeSection === "workspace-intake" ? "workflow-step-current" : ""}`}
              aria-current={activeSection === "workspace-intake" ? "step" : undefined}
            >
              <span className="step-number">1</span> رفع صحيفة التظلم
            </a>
            <span className="step-arrow" aria-hidden="true">
              ←
            </span>
            <a
              href="#workspace-analysis"
              className={`workflow-step ${activeSection === "workspace-analysis" ? "workflow-step-current" : ""}`}
              aria-current={activeSection === "workspace-analysis" ? "step" : undefined}
            >
              <span className="step-number">2</span> الفحص الشكلي والموضوعي
            </a>
            <span className="step-arrow" aria-hidden="true">
              ←
            </span>
            <a
              href="#workspace-memo"
              className={`workflow-step ${activeSection === "workspace-memo" ? "workflow-step-current" : ""}`}
              aria-current={activeSection === "workspace-memo" ? "step" : undefined}
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
              <UiIcon name="scales" /> الهيئة العامة للضرائب · إدارة ضريبة
              الدخل
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
            onDemo={(key) => open(() => api.loadDemo(key), "demo")}
            onUpload={(files) => open(() => api.upload(files), "upload", files.map(f => f.name).join("، "))}
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
            {dossier && <DossierPanel dossier={dossier} onOpenChat={() => setChatDossierId(dossier.dossierId)} />}
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
      {dossier && !busy && (
        <LegalChatPanel key={dossier.dossierId} dossier={dossier} health={health}
          open={chatDossierId === dossier.dossierId}
          onOpen={() => setChatDossierId(dossier.dossierId)} onClose={() => setChatDossierId(undefined)} />
      )}
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
