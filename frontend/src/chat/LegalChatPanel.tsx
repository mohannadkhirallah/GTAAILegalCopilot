import { useEffect, useRef } from "react";
import Markdown from "react-markdown";
import remarkGfm from "remark-gfm";
import type { DisputeDossier, Health } from "../types";
import { UiIcon } from "../components/UiIcon";
import { useLegalChat } from "./useLegalChat";
import type { ChatTurn, LegalSource } from "./types";

const suggestions = [
  "لخص هذا التظلم والبنود المتنازع عليها.",
  "ما نتيجة التحليل الحالي والمبالغ المعاد احتسابها؟",
  "ما الأساس القانوني للبنود المتنازع عليها في هذا الملف؟",
];

function sourceTarget(turn: ChatTurn, source: LegalSource) {
  return `source-${turn.id}-${source.citationId}`;
}

function AnswerText({ turn }: { turn: ChatTurn }) {
  if (turn.role === "user") return <p className="chat-message-text">{turn.text}</p>;
  const content = turn.text.replace(/\[(S\d+)\]/g, (original, id: string) => {
    const source = turn.sources?.find(s => s.citationId === id);
    return source ? `[${id}](#${sourceTarget(turn, source)})` : original;
  });
  return (
    <div className="chat-message-text chat-markdown">
      <Markdown remarkPlugins={[remarkGfm]} skipHtml components={{
        a: ({ href, children }) => {
          const source = turn.sources?.find(s => href === `#${sourceTarget(turn, s)}`);
          return source ? <a href={href} className="chat-citation" aria-label={`عرض المصدر ${source.citationId}`}
            onClick={event => {
              event.preventDefault();
              const target = document.getElementById(sourceTarget(turn, source));
              const details = target?.closest<HTMLDetailsElement>("details.chat-sources");
              if (details) details.open = true;
              target?.scrollIntoView({ behavior: "smooth", block: "nearest" });
            }}>[{source.citationId}]</a> : <span>{children}</span>;
        },
        img: () => null,
        table: ({ children }) => <div className="chat-table-scroll"><table>{children}</table></div>,
      }}>{content}</Markdown>
    </div>
  );
}

function SourceCard({ source, turn }: { source: LegalSource; turn: ChatTurn }) {
  const safeUrl =
    source.sourceUrl && /^https?:\/\//i.test(source.sourceUrl)
      ? source.sourceUrl
      : undefined;
  return (
    <div className="chat-source" id={sourceTarget(turn, source)}>
      <div className="chat-source-heading">
        <span className="chat-source-id" dir="ltr">
          [{source.citationId}]
        </span>
        <b>{source.officialTitle || source.lawId || "نص تشريعي"}</b>
      </div>
      <div className="chat-source-meta">
        {source.regulationType && <span>{source.regulationType}</span>}
        {source.number && <span>رقم {source.number}</span>}
        {source.year && <span>لسنة {source.year}</span>}
        {source.articleNumber && <span>المادة {source.articleNumber}</span>}
        {source.status && <span>الحالة: {source.status}</span>}
      </div>
      {source.amendedLawName && (
        <p className="chat-source-meta">
          التشريع المعدّل: {source.amendedLawName}
          {source.amendedLawNumber && ` رقم ${source.amendedLawNumber}`}
          {source.amendedLawYear && ` لسنة ${source.amendedLawYear}`}
        </p>
      )}
      <details>
        <summary>عرض النص المسترجع</summary>
        <p className="chat-source-content">{source.content}</p>
        {source.officialGazetteIssueNumber && (
          <p className="chat-source-meta">
            الجريدة الرسمية: العدد {source.officialGazetteIssueNumber}
            {source.officialGazettePage &&
              `، الصفحة ${source.officialGazettePage}`}
          </p>
        )}
      </details>
      {safeUrl && (
        <a
          className="chat-source-link"
          href={safeUrl}
          target="_blank"
          rel="noopener noreferrer"
        >
          فتح المصدر ↗
        </a>
      )}
    </div>
  );
}

export function LegalChatPanel({ health, dossier, open, onOpen, onClose }: {
  health?: Health; dossier: DisputeDossier; open: boolean; onOpen: () => void; onClose: () => void;
}) {
  const chat = useLegalChat(dossier.dossierId);
  const end = useRef<HTMLDivElement>(null);
  const panel = useRef<HTMLElement>(null);
  const launcher = useRef<HTMLButtonElement>(null);
  const wasOpen = useRef(false);
  const enabled = !!health?.legalChatEnabled;
  useEffect(() => {
    if (open) end.current?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }, [chat.turns, chat.busy, open]);
  useEffect(() => {
    if (open) panel.current?.querySelector<HTMLButtonElement>(".chat-close")?.focus();
    else if (wasOpen.current) launcher.current?.focus();
    wasOpen.current = open;
  }, [open]);

  const close = () => onClose();

  return (
    <>
    <button ref={launcher} className="assistant-launcher" hidden={open} onClick={onOpen}
      aria-controls="workspace-chat" aria-expanded={open}>
      <UiIcon name="sparkles" /><span>المستشار القانوني</span><span className="assistant-launcher-dot" />
    </button>
    <aside ref={panel} id="workspace-chat" hidden={!open} className="legal-chat assistant-drawer"
      aria-labelledby="legal-chat-title" onKeyDown={e => { if (e.key === "Escape") close(); }}>
      <header className="legal-chat-header">
        <div className="assistant-brand">
          <span className="assistant-mark"><UiIcon name="sparkles" /></span>
          <h2 id="legal-chat-title">
            المستشار القانوني
          </h2>
        </div>
        <div className="assistant-header-actions">
        <button
          className="assistant-icon-button"
          aria-label="محادثة جديدة" title="محادثة جديدة"
          onClick={chat.reset}
          disabled={chat.turns.length === 0 && !chat.error}
        >
          <UiIcon name="plus" />
        </button>
        <button className="assistant-icon-button chat-close" onClick={close} aria-label="إغلاق المساعد" title="إغلاق المساعد">
          <UiIcon name="close" />
        </button>
        </div>
      </header>
      <div className="assistant-context">
        <span className="assistant-context-icon"><UiIcon name="document" /></span>
        <b title={dossier.taxpayer.nameAr}>{dossier.taxpayer.nameAr}</b>
        <small>{dossier.committeeRecordNumber || dossier.dossierId} · {dossier.disputedFiscalYear}</small>
      </div>
      {!enabled && (
        <div className="chat-connection" role="status">
          {health
            ? "المحادثة غير متاحة حالياً؛ يلزم تهيئة خدمة البحث ونماذج الذكاء الاصطناعي."
            : "جارٍ التحقق من اتصال الخدمة…"}
        </div>
      )}
      <div
        className="chat-transcript"
        role="log"
        aria-label="محادثة المستشار القانوني"
        aria-live="polite"
      >
        {chat.turns.length === 0 && (
          <div className="chat-welcome">
            <div className="assistant-welcome-mark"><UiIcon name="scales" /></div>
            <span className="assistant-eyebrow">من التفاصيل إلى الصورة الكاملة</span>
            <h3>نقرأ الملف معاً.</h3>
            <p>
              المساعد يستخدم بيانات الملف والتحليل الحالي، ويبحث في التشريعات
              عند الحاجة إلى أساس قانوني. يمكنك طرح أسئلة متابعة في المحادثة نفسها.
            </p>
            <div className="chat-suggestions">
              {suggestions.map((question) => (
                <button
                  key={question}
                  disabled={!enabled || chat.busy}
                  onClick={() => void chat.send(question)}
                >
                  <UiIcon name="chat" /><span>{question}</span><span aria-hidden="true">↗</span>
                </button>
              ))}
            </div>
          </div>
        )}
        {chat.turns.map((turn) => (
          <article key={turn.id} className={`chat-turn chat-turn-${turn.role}`}>
            <span className="chat-speaker">
              {turn.role === "user" ? "أنت" : "المساعد القانوني"}
            </span>
            <AnswerText turn={turn} />
            {!!turn.sources?.length && (
              <details className="chat-sources" aria-label="مصادر الإجابة">
                <summary><UiIcon name="document" /> المراجع القانونية <span>{turn.sources.length}</span></summary>
                {turn.sources.map((source) => (
                  <SourceCard key={source.id} source={source} turn={turn} />
                ))}
              </details>
            )}
          </article>
        ))}
        {chat.busy && (
          <div className="chat-loading" role="status">
            <span className="loading-dot" />
            {chat.turns.at(-1)?.role === "assistant"
              ? "جارٍ كتابة الإجابة…"
              : "جارٍ إعداد الإجابة…"}
          </div>
        )}
        <div ref={end} />
      </div>
      {chat.error && (
        <div className="status-message status-error" role="alert">
          {chat.error}
        </div>
      )}
      <form
        className="chat-composer"
        onSubmit={(e) => {
          e.preventDefault();
          if (enabled) void chat.send();
        }}
      >
        <div className="chat-input-wrap">
        <textarea
          id="legal-question"
          aria-label="سؤالك عن الملف"
          value={chat.input}
          onChange={(e) => chat.setInput(e.target.value)}
          disabled={!enabled || chat.busy}
          maxLength={4000}
          rows={1}
          placeholder="اسأل عن وقائع التظلم أو المبالغ أو الأساس القانوني…"
          onKeyDown={(e) => {
            if (
              e.key === "Enter" &&
              !e.shiftKey &&
              !e.nativeEvent.isComposing
            ) {
              e.preventDefault();
              if (enabled) void chat.send();
            }
          }}
        />
          {chat.busy ? (
            <button
              type="button"
              className="chat-send-button"
              aria-label="إيقاف" title="إيقاف"
              onClick={chat.stop}
            >
              <UiIcon name="stop" />
            </button>
          ) : (
            <button
              type="submit"
              className="chat-send-button"
              aria-label="إرسال السؤال"
              title="إرسال السؤال"
              disabled={!enabled || !chat.input.trim()}
            >
              <UiIcon name="send" />
            </button>
          )}
        </div>
        <div className="chat-composer-hint">Enter للإرسال · {chat.input.length}/4000</div>
      </form>
    </aside>
    </>
  );
}
