import type { CaseAnalysis } from '../types'
import { date, determinationClass, determinationLabel, qar, violationLabel } from '../format'
import { Card, Stat } from './Card'

export function ProceduralPanel({ analysis }: { analysis: CaseAnalysis }) {
  const v = analysis.procedural
  return (
    <Card
      className="procedural-card"
      title="الفحص الشكلي — النظام العام"
      actions={
        <span className={`status-badge ${v.isAdmissibleFormally ? 'bg-emerald-100 text-emerald-800' : 'bg-red-100 text-red-800'}`}>
          {v.isAdmissibleFormally ? 'مقبول شكلاً' : 'غير مقبول شكلاً'}
        </span>
      }
    >
      <div className="stat-grid stat-grid-four">
        <Stat label="تاريخ الإخطار بالربط" value={date(v.assessmentNoticeDate)} />
        <Stat label="آخر ميعاد للاعتراض (م 18)" value={date(v.statutoryObjectionDeadline)} />
        <Stat label="تاريخ الاعتراض الفعلي" value={date(v.actualObjectionDate)} tone={v.actualObjectionDate ? 'default' : 'bad'} />
        <Stat label="أيام منذ الإخطار حتى القيد" value={v.daysElapsedSinceNotice} />
      </div>
      {v.proceduralViolationCode && (
        <div className="legal-callout mt-4 border border-red-200 bg-red-50 text-red-800">
          <b>المخالفة الإجرائية:</b> {violationLabel[v.proceduralViolationCode] ?? v.proceduralViolationCode}
        </div>
      )}
      <div className="legal-callout mt-4 border border-amber-200 bg-amber-50 font-semibold text-amber-900">التوصية: {v.rulingRecommendation}</div>
      <p className="mt-4 text-sm leading-7 text-slate-700">{v.formulatedDefenseClauseAr}</p>
      <details className="legal-details mt-3 text-sm">
        <summary className="cursor-pointer text-maroon-700">السند القانوني والمبدأ القضائي</summary>
        <ul className="mt-2 list-disc space-y-1 pr-5 text-slate-600">
          {v.governingLegalBasis.map((b) => (
            <li key={b.article + b.source}><b>{b.article}</b> — {b.source}: {b.ruleSummary}</li>
          ))}
          <li><b>{v.jurisprudenceDoctrine.court}:</b> {v.jurisprudenceDoctrine.principleAr}</li>
        </ul>
      </details>
    </Card>
  )
}

export function SubstantivePanel({ analysis }: { analysis: CaseAnalysis }) {
  const s = analysis.substantive
  return (
    <Card
      className="substantive-card"
      title={s.defensePosture === 'ALTERNATIVE_RESERVE' ? 'الرد الموضوعي — على سبيل الاحتياط الكلي' : 'الرد الموضوعي'}
    >
      <div className="stat-grid stat-grid-three mb-4">
        <Stat label="إجمالي المبالغ محل النزاع" value={qar(s.totalDisputedClaimedQar)} />
        <Stat label="ما أقرت به الهيئة" value={qar(s.totalConcessionsAdmittedQar)} tone="good" />
        <Stat label="المرفوض المؤيد" value={qar(s.totalDisallowedConfirmedQar)} tone="bad" />
      </div>
      <div className="table-scroll">
        <table className="data-table substantive-table">
          <thead className="bg-slate-50 text-xs text-slate-500">
            <tr>
              <th className="p-2 text-right">البند</th>
              <th className="p-2 text-right">المبلغ</th>
              <th className="p-2 text-right">القرار</th>
              <th className="p-2 text-right">المقر به</th>
              <th className="p-2 text-right">السند</th>
            </tr>
          </thead>
          <tbody>
            {s.lineItemsEvaluation.map((e) => (
              <tr key={e.itemId} className="border-t border-slate-100 align-top">
                <td className="p-2">
                  <div className="font-semibold">{e.lineName}</div>
                  <div className="mt-1 text-xs leading-5 text-slate-500">{e.legalReasoningAr}</div>
                </td>
                <td className="p-2 whitespace-nowrap tabular-nums">{qar(e.claimedAmountQar)}</td>
                <td className="p-2">
                  <span className={`whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-bold ${determinationClass[e.gtaDetermination]}`}>
                    {determinationLabel[e.gtaDetermination]}
                  </span>
                </td>
                <td className="p-2 whitespace-nowrap tabular-nums">{qar(e.admittedDeductionQar)}</td>
                <td className="p-2 text-xs text-slate-600">{e.statutoryReference}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  )
}

export function FinancialPanel({ analysis }: { analysis: CaseAnalysis }) {
  const f = analysis.financial
  const primary = !analysis.procedural.isAdmissibleFormally
  return (
    <Card
      className="financial-card"
      title="إعادة الاحتساب المالي (محرك حتمي)"
      actions={f.isPenaltyCapped && <span className="status-badge bg-amber-100 text-amber-800">تم إعمال سقف 100% (م 24)</span>}
    >
      <div className="stat-grid stat-grid-four mb-4">
        <Stat label="فرق الضريبة المعدل" value={qar(f.revisedTaxDiffQar)} />
        <Stat label="غرامات التأخير المعدلة" value={qar(f.revisedDelayPenaltiesQar)} />
        <Stat label={primary ? 'الإجمالي احتياطياً' : 'الإجمالي المعدل'} value={qar(f.revisedTotalDueQar)} />
        <Stat label={primary ? 'المستحق للخزانة (أصلياً)' : 'المستحق للخزانة'} value={qar(f.finalTreasuryReceivableQar)} tone="bad" />
      </div>
      <div className="table-scroll">
        <table className="data-table financial-table">
          <thead className="bg-slate-50 text-xs text-slate-500">
            <tr>
              <th className="p-2 text-right">البيان</th>
              <th className="p-2 text-right">الربط الأصلي</th>
              <th className="p-2 text-right">المقترح</th>
              <th className="p-2 text-right">الفرق</th>
            </tr>
          </thead>
          <tbody>
            {f.ledgerComparisonMatrix.map((l) => (
              <tr key={l.ledgerEntryName} className="border-t border-slate-100">
                <td className="p-2">
                  <div className="font-semibold">{l.ledgerEntryName}</div>
                  <div className="text-xs text-slate-500">{l.legalBasis}</div>
                </td>
                <td className="p-2 tabular-nums">{qar(l.originalAssessmentQar)}</td>
                <td className="p-2 tabular-nums">{qar(l.settlementProposalQar)}</td>
                <td className={`p-2 tabular-nums ${l.varianceQar < 0 ? 'text-emerald-700' : ''}`}>{qar(l.varianceQar)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="mt-3 text-xs text-slate-500">
        سعر الضريبة {(f.corporateTaxRate * 100).toFixed(0)}% · نمط الاحتساب {f.computationMode} · جميع المبالغ محسوبة في C# دون أي تدخل من النموذج اللغوي.
      </p>
    </Card>
  )
}
