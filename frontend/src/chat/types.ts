export interface LegalSource {
  id: string;
  citationId: string;
  lawId?: string | null;
  regulationType?: string | null;
  number?: string | null;
  year?: string | null;
  officialTitle?: string | null;
  date?: string | null;
  status?: string | null;
  type?: string | null;
  amendedLawNumber?: string | null;
  amendedLawName?: string | null;
  amendedLawYear?: string | null;
  articleNumber?: string | null;
  sourceUrl?: string | null;
  officialGazetteIssueNumber?: string | null;
  officialGazettePublicationDate?: string | null;
  officialGazettePage?: string | null;
  chunkNumber?: number | null;
  content: string;
}

export interface LegalChatMessage {
  role: "user" | "assistant";
  text: string;
}
export interface LegalChatAnswer {
  answer: string;
  sources: LegalSource[];
  grounded: boolean;
}
export interface ChatTurn extends LegalChatMessage {
  id: string;
  sources?: LegalSource[];
  grounded?: boolean;
}
