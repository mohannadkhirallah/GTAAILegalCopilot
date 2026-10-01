import type { DeterminationType } from './types'

const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

export const qar = (v: number) => `${money.format(v)} ر.ق`
export const date = (iso?: string | null) => (iso ? iso.replaceAll('-', '/') : '—')

export const determinationLabel: Record<DeterminationType, string> = {
  RejectedFully: 'رفض كلي',
  AcceptedPartially: 'قبول جزئي',
  AcceptedFully: 'قبول كلي',
}

export const determinationClass: Record<DeterminationType, string> = {
  RejectedFully: 'bg-red-100 text-red-800',
  AcceptedPartially: 'bg-amber-100 text-amber-800',
  AcceptedFully: 'bg-emerald-100 text-emerald-800',
}

export const violationLabel: Record<string, string> = {
  ART18_ADMIN_OBJECTION_BYPASSED: 'تخطي الاعتراض الإداري الوجوبي (م 18)',
  ART18_OBJECTION_TIME_BARRED: 'اعتراض بعد فوات الميعاد (م 18)',
  ART19_GRIEVANCE_BEFORE_OBJECTION: 'تظلم سابق على الاعتراض (م 19)',
  ART19_GRIEVANCE_TIME_BARRED: 'تظلم بعد فوات الميعاد (م 19)',
}
