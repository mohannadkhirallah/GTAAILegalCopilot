import { useRef, useState } from "react";
import type { DemoCaseInfo, DossierSummary } from "../types";
import { date } from "../format";
import { UiIcon } from "./UiIcon";

interface Props {
  demos: DemoCaseInfo[];
  dossiers: DossierSummary[];
  activeId?: string;
  busy: boolean;
  aiExtraction: boolean;
  onDemo: (key: string) => void;
  onUpload: (files: File[]) => void;
  onSelect: (id: string) => void;
}

export function Sidebar({
  demos,
  dossiers,
  activeId,
  busy,
  aiExtraction,
  onDemo,
  onUpload,
  onSelect,
}: Props) {
  const input = useRef<HTMLInputElement>(null);
  const [files, setFiles] = useState<File[]>([]);

  return (
    <aside className="workspace-sidebar" aria-label="ملفات التظلم">
      <div className="ui-card case-library">
        <h3 className="sidebar-title">
          <span className="heading-dot" aria-hidden="true" />
          الحالات التجريبية الفورية
        </h3>
        <div className="case-list">
          {demos.map((d) => (
            <button
              key={d.key}
              disabled={busy}
              onClick={() => onDemo(d.key)}
              className="case-choice"
            >
              <div className="case-choice-title">
                <UiIcon name="document" />
                <span>{d.titleAr}</span>
                <span className="case-choice-arrow" aria-hidden="true">
                  ←
                </span>
              </div>
              <div className="case-choice-description">{d.descriptionAr}</div>
            </button>
          ))}
        </div>
      </div>

      <div className="ui-card upload-card">
        <h3 className="sidebar-title">
          <span className="heading-dot" aria-hidden="true" />
          رفع صحيفة التظلم
        </h3>
        <p className="upload-description">
          PDF أو DOCX{" "}
          {aiExtraction
            ? "(استخراج آلي عبر Azure OpenAI)"
            : "(يتطلب تهيئة Azure OpenAI)"}
          ، أو ملف JSON منظم للملف.
        </p>
        <div className="upload-area">
          <span className="upload-icon" aria-hidden="true">
            <UiIcon name="upload" />
          </span>
          <input
            ref={input}
            type="file"
            multiple
            accept=".pdf,.docx,.json"
            onChange={(e) => setFiles(Array.from(e.target.files ?? []))}
            aria-label="رفع صحيفة التظلم"
            className="upload-input"
          />
        </div>
        <button
          disabled={busy || files.length === 0}
          onClick={() => {
            onUpload(files);
            setFiles([]);
            if (input.current) input.current.value = "";
          }}
          className="button-primary upload-button"
        >
          {/* <UiIcon name="sparkles" /> */}
          رفع وتحليل
          <span aria-hidden="true">←</span>
        </button>
      </div>

      <div className="ui-card saved-dossiers">
        <h3 className="sidebar-title">
          <UiIcon name="folder" />
          الملفات المحفوظة
        </h3>
        {dossiers.length === 0 && (
          <p className="saved-empty">لا توجد ملفات بعد.</p>
        )}
        <ul className="saved-list">
          {dossiers.map((d) => (
            <li key={d.dossierId}>
              <button
                disabled={busy}
                onClick={() => onSelect(d.dossierId)}
                className={`saved-file ${d.dossierId === activeId ? "saved-file-active" : ""}`}
              >
                <div className="truncate">{d.taxpayerNameAr}</div>
                <div className="saved-file-reference" dir="ltr">
                  {d.committeeRecordNumber || d.dossierId} ·{" "}
                  {date(d.committeeFilingDate)}
                </div>
              </button>
            </li>
          ))}
        </ul>
      </div>
    </aside>
  );
}
