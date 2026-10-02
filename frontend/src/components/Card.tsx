import type { ReactNode } from 'react'

export function Card({ title, actions, children, className = '' }: { title: string; actions?: ReactNode; children: ReactNode; className?: string }) {
  return (
    <section className={`ui-card panel-card ${className}`}>
      <header className="panel-header">
        <h2 className="panel-title"><span className="heading-dot" aria-hidden="true" />{title}</h2>
        {actions}
      </header>
      <div className="panel-body">{children}</div>
    </section>
  )
}

export function Stat({ label, value, tone = 'default' }: { label: string; value: ReactNode; tone?: 'default' | 'good' | 'bad' }) {
  const toneClass = tone === 'good' ? 'text-emerald-700' : tone === 'bad' ? 'text-red-700' : 'text-slate-900'
  return (
    <div className="stat-card">
      <div className="stat-label">{label}</div>
      <div className={`stat-value ${toneClass}`}>{value}</div>
    </div>
  )
}
