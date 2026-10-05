import type { LegalChatAnswer, LegalChatMessage } from "./types";

export async function askLegalQuestion(
  dossierId: string,
  message: string,
  history: LegalChatMessage[],
  signal: AbortSignal,
  onDelta: (text: string) => void,
) {
  const response = await fetch(`/api/dossiers/${encodeURIComponent(dossierId)}/chat/stream`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ message, history }),
    signal,
  });
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.title ?? "تعذر الاتصال بخدمة المحادثة.");
  }
  if (!response.body) throw new Error("لم تصل إجابة من خدمة المحادثة.");

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  try {
    while (true) {
      const { value, done } = await reader.read();
      buffer += decoder.decode(value, { stream: !done });
      let separator: RegExpExecArray | null;
      while ((separator = /\r?\n\r?\n/.exec(buffer))) {
        const frame = buffer.slice(0, separator.index);
        buffer = buffer.slice(separator.index + separator[0].length);
        const event = /^event: *(.*)$/m.exec(frame)?.[1].trim();
        const data = frame.split(/\r?\n/).filter(line => line.startsWith("data:"))
          .map(line => line.slice(5).trimStart()).join("\n");
        if (!data) continue;
        const payload = JSON.parse(data);
        if (event === "delta") onDelta(payload.text);
        if (event === "error") throw new Error(payload.message);
        if (event === "done") return payload as LegalChatAnswer;
      }
      if (done) throw new Error("انقطع الاتصال قبل اكتمال الإجابة. أعد المحاولة.");
    }
  } finally {
    await reader.cancel().catch(() => {});
    reader.releaseLock();
  }
}
