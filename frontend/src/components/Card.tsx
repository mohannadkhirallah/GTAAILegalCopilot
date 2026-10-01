import type { ReactNode } from 'react'

export function Card({ title, actions, children }: { title: string; actions?: ReactNode; children: ReactNode }) {
  return (
    <section className="rounded-xl border border-slate-200 bg-white shadow-sm">
      <header className="flex items-center justify-between gap-2 border-b border-slate-100 px-5 py-3">
        <h2 className="text-base font-bold text-maroon-700">{title}</h2>
        {actions}
      </header>
      <div className="p-5">{children}</div>
    </section>
  )
}

export function Stat({ label, value, tone = 'default' }: { label: string; value: ReactNode; tone?: 'default' | 'good' | 'bad' }) {
  const toneClass = tone === 'good' ? 'text-emerald-700' : tone === 'bad' ? 'text-red-700' : 'text-slate-900'
  return (
    <div className="rounded-lg bg-slate-50 px-4 py-3">
      <div className="text-xs text-slate-500">{label}</div>
      <div className={`mt-1 font-bold tabular-nums ${toneClass}`}>{value}</div>
    </div>
  )
}
