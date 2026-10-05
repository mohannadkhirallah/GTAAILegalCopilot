import { useCallback, useEffect, useRef, useState } from "react";
import { api, streamMemo } from "../api";
import type {
  AssembledMemo,
  CaseAnalysis,
  DemoCaseInfo,
  DisputeDossier,
  DossierSummary,
  Health,
  MemoSectionState,
} from "../types";

export interface OperationNotice {
  state: "pending" | "success" | "error";
  title: string;
  detail: string;
}

/** Owns dossier loading, deterministic analysis, and memo streaming. */
export function useDossierWorkspace() {
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
  const [notice, setNotice] = useState<OperationNotice>();
  const closeStream = useRef<() => void>(undefined);

  useEffect(() => {
    if (!notice || notice.state === "pending") return;
    const timer = window.setTimeout(() => setNotice(undefined), 3500);
    return () => window.clearTimeout(timer);
  }, [notice]);

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
    async (load: () => Promise<DisputeDossier>, action: "upload" | "demo" | "open" = "open", fileNames = "") => {
      closeStream.current?.();
      setBusy(true);
      setError(undefined);
      setSections([]);
      setMemo(undefined);
      setAnalysis(undefined);
      setStreaming(false);
      const showNotice = action === "upload";
      setNotice(showNotice ? { state: "pending", title: action === "upload" ? "جارٍ رفع المستند وتجهيز بياناته" : "جارٍ تجهيز الملف",
        detail: action === "upload" ? fileNames : "سيظهر الفحص بمجرد اكتمال تجهيز البيانات." } : undefined);
      let saved = false;
      try {
        const d = await load();
        saved = action !== "open";
        setDossier(d);
        refreshList();
        if (showNotice) setNotice({ state: "pending", title: "تم حفظ الملف · جارٍ الفحص",
          detail: d.taxpayer.nameAr });
        setAnalysis(await api.analyze(d.dossierId));
        if (showNotice) setNotice({ state: "success", title: "تم حفظ الملف وتجهيزه بنجاح",
          detail: "أُضيف إلى الملفات المحفوظة وأصبح جاهزًا للمراجعة." });
      } catch (e) {
        const message = (e as Error).message;
        setError(message);
        if (showNotice) setNotice({ state: "error", title: saved ? "تم حفظ الملف، وتعذّر إكمال الفحص" : action === "upload" ? "تعذّر رفع المستند" : "تعذّر تجهيز الملف",
          detail: message });
      } finally {
        setBusy(false);
      }
    },
    [refreshList],
  );

  const generate = () => {
    if (!dossier || busy || streaming) return;
    closeStream.current?.();
    setSections([]);
    setMemo(undefined);
    setError(undefined);
    setStreaming(true);
    setNotice({ state: "pending", title: "جارٍ إعداد المذكرة", detail: "تُحفظ المذكرة تلقائيًا عند اكتمال الإعداد." });
    const patch = (
      key: string,
      fn: (s: MemoSectionState) => MemoSectionState,
    ) => setSections((prev) => prev.map((s) => (s.key === key ? fn(s) : s)));
    closeStream.current = streamMemo(dossier.dossierId, useAi, {
      onAnalysis: setAnalysis,
      onSectionStart: (key, title) => {
        setSections((prev) => [...prev, { key, title, text: "", done: false }]);
        setNotice({ state: "pending", title: "جارٍ إعداد المذكرة", detail: title });
      },
      onDelta: (key, text) =>
        patch(key, (s) => ({ ...s, text: s.text + text })),
      onSectionReplace: (key, text) => patch(key, (s) => ({ ...s, text })),
      onSectionEnd: (key) => patch(key, (s) => ({ ...s, done: true })),
      onMemo: (completed) => {
        setMemo(completed);
        setNotice({ state: "success", title: "تم إعداد المذكرة وحفظها", detail: "المذكرة جاهزة للمراجعة والتنزيل." });
      },
      onError: (m) => {
        setError(m);
        setStreaming(false);
        setNotice({ state: "error", title: "تعذّر إكمال المذكرة", detail: m });
      },
      onDone: () => setStreaming(false),
    });
  };

  return {
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
    dismissNotice: () => setNotice(undefined),
    open,
    generate,
  };
}
