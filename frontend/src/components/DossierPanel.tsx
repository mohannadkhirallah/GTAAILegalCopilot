import type { DisputeDossier } from '../types'
import { date, qar } from '../format'
import { Card, Stat } from './Card'

export function DossierPanel({ dossier }: { dossier: DisputeDossier }) {
  const t = dossier.taxpayer
  const r = dossier.dhareebaRecord
  return (
    <Card title={`ملف التظلم — ${dossier.committeeRecordNumber || dossier.dossierId}`}>
      <div className="mb-4">
        <div className="text-lg font-bold">{t.nameAr}</div>
        {t.nameEn && <div className="text-sm text-slate-500" dir="ltr">{t.nameEn}</div>}
        <div className="mt-1 text-xs text-slate-500">
          {t.legalForm} · {t.commercialActivity} · الرقم الضريبي {t.tin} · السجل التجاري {t.crNumber} · السنة الضريبية {dossier.disputedFiscalYear}
        </div>
      </div>
      <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
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
