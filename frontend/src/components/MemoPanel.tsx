import type { AssembledMemo, MemoSectionState } from "../types";
import { Card } from "./Card";
import { UiIcon } from "./UiIcon";

interface Props {
  sections: MemoSectionState[];
  memo?: AssembledMemo;
  streaming: boolean;
  aiAvailable: boolean;
  useAi: boolean;
  onToggleAi: (v: boolean) => void;
  onGenerate: () => void;
  docxUrl?: string;
}

export function MemoPanel({
  sections,
  memo,
  streaming,
  aiAvailable,
  useAi,
  onToggleAi,
  onGenerate,
  docxUrl,
}: Props) {
  return (
    <Card
      className="memo-card"
      title="مذكرة رد وتعقيب موضوعي وشكلي على التظلم الضريبي"
      actions={
        <div className="memo-actions">
          <label className={`ai-toggle ${aiAvailable ? "" : "opacity-50"}`}>
            <input
              type="checkbox"
              disabled={!aiAvailable || streaming}
              checked={aiAvailable && useAi}
              onChange={(e) => onToggleAi(e.target.checked)}
            />
            صياغة لغوية عبر Azure OpenAI
          </label>
          <button
            onClick={onGenerate}
            disabled={streaming}
            className="button-primary"
          >
            {/* <UiIcon name="sparkles" /> */}
            {streaming ? "جارٍ الإعداد…" : "إعداد المذكرة"}
          </button>
          {memo && docxUrl && (
            <a href={docxUrl} className="button-secondary">
              <UiIcon name="download" />
              تصدير Word
            </a>
          )}
        </div>
      }
    >
      {sections.length === 0 ? (
        <div className="memo-empty">
          <span className="empty-icon">
            <UiIcon name="document" />
          </span>
          <p>اضغط «إعداد المذكرة» لبث المذكرة مباشرة (SSE).</p>
        </div>
      ) : (
        <article className="memo-paper font-arabic text-[15px] leading-8 text-slate-800">
          {memo && (
            <div className="memo-letterhead mb-6 text-sm font-bold leading-6">
              <div>{memo.metadata.state}</div>
              <div>{memo.metadata.authority}</div>
              <div>{memo.metadata.department}</div>
            </div>
          )}
          {sections.map((s) => (
            <div key={s.key} className="memo-section mb-6">
              {s.key !== "preamble" && (
                <h3 className="memo-section-title">{s.title}</h3>
              )}
              <div className="whitespace-pre-wrap text-justify">
                {s.text}
                {!s.done && (
                  <span className="mr-1 inline-block h-4 w-2 animate-pulse bg-maroon-600 align-middle" />
                )}
              </div>
            </div>
          ))}
          {memo && (
            <>
              <p className="memo-signoff mt-8 text-center font-bold">
                وتفضلوا بقبول فائق الاحترام والتقدير،،،
              </p>
              <p className="mt-4 text-xs text-slate-400">
                مصدر الصياغة:{" "}
                {memo.narrativeSource === "AZURE_OPENAI_REFINED"
                  ? "Azure OpenAI (مُتحقق من سلامة الأرقام)"
                  : "قوالب حتمية"}
              </p>
            </>
          )}
        </article>
      )}
    </Card>
  );
}
