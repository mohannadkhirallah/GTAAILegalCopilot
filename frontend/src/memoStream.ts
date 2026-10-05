import type { AssembledMemo, CaseAnalysis } from "./types";

export interface MemoStreamHandlers {
  onAnalysis: (a: CaseAnalysis) => void;
  onSectionStart: (key: string, title: string) => void;
  onDelta: (key: string, text: string) => void;
  onSectionReplace: (key: string, text: string) => void;
  onSectionEnd: (key: string) => void;
  onMemo: (memo: AssembledMemo) => void;
  onError: (message: string) => void;
  onDone: () => void;
}

/** Opens the SSE memo stream. Returns a disposer that closes the connection. */
export function streamMemo(
  id: string,
  useAi: boolean,
  h: MemoStreamHandlers,
): () => void {
  const es = new EventSource(
    `/api/dossiers/${encodeURIComponent(id)}/memo/stream?useAi=${useAi}`,
  );
  const parse = (e: MessageEvent) => JSON.parse(e.data);
  es.addEventListener("analysis", (e) =>
    h.onAnalysis(parse(e as MessageEvent).payload),
  );
  es.addEventListener("section_start", (e) => {
    const d = parse(e as MessageEvent);
    h.onSectionStart(d.section, d.text);
  });
  es.addEventListener("delta", (e) => {
    const d = parse(e as MessageEvent);
    h.onDelta(d.section, d.text);
  });
  es.addEventListener("section_replace", (e) => {
    const d = parse(e as MessageEvent);
    h.onSectionReplace(d.section, d.text);
  });
  es.addEventListener("section_end", (e) =>
    h.onSectionEnd(parse(e as MessageEvent).section),
  );
  es.addEventListener("memo", (e) =>
    h.onMemo(parse(e as MessageEvent).payload),
  );
  es.addEventListener("error", (e) => {
    const data = (e as MessageEvent).data;
    h.onError(
      data
        ? JSON.parse(data).message
        : "انقطع الاتصال بالخادم أثناء إعداد المذكرة.",
    );
    es.close();
  });
  es.addEventListener("done", () => {
    h.onDone();
    es.close();
  });
  return () => es.close();
}
