import type { AssembledMemo, MemoSectionState } from '../types'
import { Card } from './Card'

interface Props {
  sections: MemoSectionState[]
  memo?: AssembledMemo
  streaming: boolean
  aiAvailable: boolean
  useAi: boolean
  onToggleAi: (v: boolean) => void
  onGenerate: () => void
  docxUrl?: string
}

export function MemoPanel({ sections, memo, streaming, aiAvailable, useAi, onToggleAi, onGenerate, docxUrl }: Props) {
  return (
    <Card
      title="مذكرة رد وتعقيب موضوعي وشكلي على التظلم الضريبي"
      actions={
        <div className="flex items-center gap-3">
          <label className={`flex items-center gap-1 text-xs ${aiAvailable ? '' : 'opacity-50'}`}>
            <input type="checkbox" disabled={!aiAvailable || streaming} checked={aiAvailable && useAi} onChange={(e) => onToggleAi(e.target.checked)} />
            صياغة لغوية عبر Azure OpenAI
          </label>
          <button
            onClick={onGenerate}
            disabled={streaming}
            className="rounded-lg bg-maroon-600 px-4 py-1.5 text-sm font-semibold text-white hover:bg-maroon-700 disabled:opacity-50"
          >
            {streaming ? 'جارٍ الإعداد…' : 'إعداد المذكرة'}
          </button>
          {memo && docxUrl && (
            <a href={docxUrl} className="rounded-lg border border-maroon-600 px-4 py-1.5 text-sm font-semibold text-maroon-700 hover:bg-maroon-50">
              تصدير Word
            </a>
          )}
        </div>
      }
    >
      {sections.length === 0 ? (
        <p className="text-sm text-slate-400">اضغط «إعداد المذكرة» لبث المذكرة مباشرة (SSE).</p>
      ) : (
        <article className="mx-auto max-w-3xl font-arabic text-[15px] leading-8 text-slate-800">
          {memo && (
            <div className="mb-6 text-sm font-bold leading-6">
              <div>{memo.metadata.state}</div>
              <div>{memo.metadata.authority}</div>
              <div>{memo.metadata.department}</div>
            </div>
          )}
          {sections.map((s) => (
            <div key={s.key} className="mb-6">
              {s.key !== 'preamble' && <h3 className="mb-2 font-bold text-maroon-700 underline underline-offset-8">{s.title}</h3>}
              <div className="whitespace-pre-wrap text-justify">
                {s.text}
                {!s.done && <span className="mr-1 inline-block h-4 w-2 animate-pulse bg-maroon-600 align-middle" />}
              </div>
            </div>
          ))}
          {memo && (
            <>
              <p className="mt-8 text-center font-bold">وتفضلوا بقبول فائق الاحترام والتقدير،،،</p>
              <p className="mt-4 text-xs text-slate-400">
                مصدر الصياغة: {memo.narrativeSource === 'AZURE_OPENAI_REFINED' ? 'Azure OpenAI (مُتحقق من سلامة الأرقام)' : 'قوالب حتمية'}
              </p>
            </>
          )}
        </article>
      )}
    </Card>
  )
}
