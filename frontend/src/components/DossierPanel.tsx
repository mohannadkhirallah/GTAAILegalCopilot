import type { DisputeDossier } from '../types'
import { date, qar } from '../format'
import { Card, Stat } from './Card'
import { UiIcon } from './UiIcon'

export function DossierPanel({ dossier, onOpenChat }: { dossier: DisputeDossier; onOpenChat: () => void }) {
  const t = dossier.taxpayer
  const r = dossier.dhareebaRecord
  return (
    <Card className="dossier-card" title={`ملف التظلم — ${dossier.committeeRecordNumber || dossier.dossierId}`}
      actions={<button className="dossier-assistant-button" onClick={onOpenChat} aria-controls="workspace-chat">
        <UiIcon name="sparkles" /> اسأل عن هذا الملف
      </button>}>
      <div className="dossier-identity">
        <div className="taxpayer-name">{t.nameAr}</div>
        {t.nameEn && <div className="text-sm text-slate-500" dir="ltr">{t.nameEn}</div>}
        <div className="mt-1 text-xs text-slate-500">
          {t.legalForm} · {t.commercialActivity} · الرقم الضريبي {t.tin} · السجل التجاري {t.crNumber} · السنة الضريبية {dossier.disputedFiscalYear}
        </div>
      </div>
      <div className="stat-grid stat-grid-four">
        <Stat label="قرار الربط" value={<span dir="ltr" className="text-sm">{r.assessmentNoticeRef}</span>} />
        <Stat label="تاريخ الإخطار" value={date(r.assessmentNoticeDate)} />
        <Stat label="الاعتراض الإداري" value={r.administrativeObjectionFiled ? date(r.administrativeObjectionDate) : 'لم يُقدَّم'} tone={r.administrativeObjectionFiled ? 'default' : 'bad'} />
        <Stat label="قيد التظلم أمام اللجنة" value={date(dossier.committeeFilingDate)} />
        <Stat label="فرق الضريبة الأصلي" value={qar(r.originalAssessedTaxDiffQar)} />
        <Stat label="غرامات التأخير الأصلية" value={qar(r.originalDelayPenaltiesQar)} />
        <Stat label="إجمالي المطالبة" value={qar(r.originalAssessedTaxDiffQar + r.originalDelayPenaltiesQar)} />
        <Stat label="عدد البنود المتنازع عليها" value={dossier.disputedItems.length} />
      </div>
    </Card>
  )
}
