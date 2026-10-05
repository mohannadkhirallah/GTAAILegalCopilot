import type { OperationNotice as Notice } from "../hooks/useDossierWorkspace";
import { UiIcon } from "./UiIcon";

export function OperationNotice({ notice, onDismiss }: { notice?: Notice; onDismiss: () => void }) {
  if (!notice) return null;
  const pending = notice.state === "pending";
  return (
    <div className={`operation-notice operation-notice-${notice.state}`} dir="rtl"
      role={notice.state === "error" ? "alert" : "status"} aria-live={notice.state === "error" ? "assertive" : "polite"} aria-atomic="true">
      <span className="operation-notice-icon" aria-hidden="true">
        {pending ? <span className="operation-spinner" /> : <UiIcon name={notice.state === "success" ? "check" : "alert"} />}
      </span>
      <div className="operation-notice-copy">
        <span className="operation-notice-label">{pending ? "قيد التنفيذ" : notice.state === "success" ? "اكتملت العملية" : "تحتاج إلى مراجعة"}</span>
        <strong>{notice.title}</strong>
        <p title={notice.detail}>{notice.detail}</p>
      </div>
      {!pending && <button type="button" className="operation-notice-close" onClick={onDismiss} aria-label="إغلاق الإشعار"><UiIcon name="close" /></button>}
      {pending && <div className="operation-notice-progress" role="progressbar" aria-label={notice.title} />}
    </div>
  );
}
