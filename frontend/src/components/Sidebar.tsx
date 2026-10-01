import { useRef, useState } from 'react'
import type { DemoCaseInfo, DossierSummary } from '../types'
import { date } from '../format'

interface Props {
  demos: DemoCaseInfo[]
  dossiers: DossierSummary[]
  activeId?: string
  busy: boolean
  aiExtraction: boolean
  onDemo: (key: string) => void
  onUpload: (files: File[]) => void
  onSelect: (id: string) => void
}

export function Sidebar({ demos, dossiers, activeId, busy, aiExtraction, onDemo, onUpload, onSelect }: Props) {
  const input = useRef<HTMLInputElement>(null)
  const [files, setFiles] = useState<File[]>([])

  return (
    <aside className="flex flex-col gap-5">
      <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
        <h3 className="mb-3 text-sm font-bold text-maroon-700">الحالات التجريبية الفورية</h3>
        <div className="flex flex-col gap-2">
          {demos.map((d) => (
            <button
              key={d.key}
              disabled={busy}
              onClick={() => onDemo(d.key)}
              className="rounded-lg border border-slate-200 px-3 py-2 text-right transition hover:border-maroon-600 hover:bg-maroon-50 disabled:opacity-50"
            >
              <div className="text-sm font-semibold">{d.titleAr}</div>
              <div className="mt-0.5 text-xs leading-5 text-slate-500">{d.descriptionAr}</div>
            </button>
          ))}
        </div>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
        <h3 className="mb-1 text-sm font-bold text-maroon-700">رفع صحيفة التظلم</h3>
        <p className="mb-3 text-xs leading-5 text-slate-500">
          PDF أو DOCX {aiExtraction ? '(استخراج آلي عبر Azure OpenAI)' : '(يتطلب تهيئة Azure OpenAI)'}، أو ملف JSON منظم للملف.
        </p>
        <input
          ref={input}
          type="file"
          multiple
          accept=".pdf,.docx,.json"
          onChange={(e) => setFiles(Array.from(e.target.files ?? []))}
          className="block w-full text-xs file:ml-3 file:rounded-md file:border-0 file:bg-maroon-50 file:px-3 file:py-1.5 file:text-maroon-700"
        />
        <button
          disabled={busy || files.length === 0}
          onClick={() => {
            onUpload(files)
            setFiles([])
            if (input.current) input.current.value = ''
          }}
          className="mt-3 w-full rounded-lg bg-maroon-600 py-2 text-sm font-semibold text-white hover:bg-maroon-700 disabled:opacity-50"
        >
          رفع وتحليل
        </button>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
        <h3 className="mb-3 text-sm font-bold text-maroon-700">الملفات المحفوظة</h3>
        {dossiers.length === 0 && <p className="text-xs text-slate-400">لا توجد ملفات بعد.</p>}
        <ul className="flex max-h-72 flex-col gap-1 overflow-y-auto">
          {dossiers.map((d) => (
            <li key={d.dossierId}>
              <button
                onClick={() => onSelect(d.dossierId)}
                className={`w-full rounded-md px-2 py-1.5 text-right text-xs hover:bg-slate-100 ${d.dossierId === activeId ? 'bg-maroon-50 font-semibold text-maroon-700' : ''}`}
              >
                <div className="truncate">{d.taxpayerNameAr}</div>
                <div className="text-slate-400" dir="ltr">{d.committeeRecordNumber || d.dossierId} · {date(d.committeeFilingDate)}</div>
              </button>
            </li>
          ))}
        </ul>
      </div>
    </aside>
  )
}
