import { useEffect, useRef, useState } from "react";
import { askLegalQuestion } from "./api";
import type { ChatTurn, LegalChatMessage } from "./types";

/** Keep complete recent turns within the API's history budget. */
export function buildChatHistory(turns: ChatTurn[]): LegalChatMessage[] {
  const history: LegalChatMessage[] = [];
  let characters = 0;
  for (let i = turns.length - 2; i >= 0 && history.length < 20; i -= 2) {
    const pair = turns.slice(i, i + 2);
    const size = pair.reduce((sum, turn) => sum + turn.text.length, 0);
    if (
      characters + size > 60000 ||
      pair.some((turn) => turn.text.length > 12000)
    )
      break;
    history.unshift(...pair.map(({ role, text }) => ({ role, text })));
    characters += size;
  }
  return history;
}

export function useLegalChat(dossierId: string) {
  const [turns, setTurns] = useState<ChatTurn[]>([]);
  const [input, setInput] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const pending = useRef<AbortController | null>(null);
  const requestNumber = useRef(0);

  useEffect(() => () => pending.current?.abort(), []);

  const send = async (question = input) => {
    const message = question.trim();
    if (!message || message.length > 4000 || pending.current) return;
    const previous = turns;
    const controller = new AbortController();
    const userTurn: ChatTurn = { id: crypto.randomUUID(), role: "user", text: message };
    const answerId = crypto.randomUUID();
    let draft = "";
    const version = ++requestNumber.current;
    pending.current = controller;
    setError(undefined);
    setInput("");
    setBusy(true);
    setTurns([...previous, userTurn]);
    try {
      const result = await askLegalQuestion(
        dossierId,
        message,
        buildChatHistory(previous),
        controller.signal,
        (text) => {
          if (version !== requestNumber.current) return;
          draft += text;
          setTurns([...previous, userTurn, { id: answerId, role: "assistant", text: draft }]);
        },
      );
      if (version !== requestNumber.current) return;
      setTurns([
        ...previous,
        userTurn,
        {
          id: answerId,
          role: "assistant",
          text: result.answer,
          sources: result.sources,
          grounded: result.grounded,
        },
      ]);
    } catch (e) {
      if (version !== requestNumber.current) return;
      setTurns(previous);
      setInput(message);
      if (!controller.signal.aborted) setError((e as Error).message);
    } finally {
      if (version === requestNumber.current) {
        pending.current = null;
        setBusy(false);
      }
    }
  };

  const reset = () => {
    ++requestNumber.current;
    pending.current?.abort();
    pending.current = null;
    setTurns([]);
    setInput("");
    setBusy(false);
    setError(undefined);
  };

  return {
    turns,
    input,
    setInput,
    busy,
    error,
    send,
    reset,
    stop: () => pending.current?.abort(),
  };
}
