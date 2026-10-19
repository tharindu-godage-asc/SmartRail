import type { ReactNode } from 'react'
import { Icon } from '../../core/Icon'
import { t } from '../../core/strings'
import { Button, Panel } from '../../core/ui'

const allDays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'] as const

/**
 * The seven days as small chips: a day the service runs on has a solid outline and a filled box, a day it does not has a dashed one,
 * and the whole group has a text name ("Runs on Mon, Tue, ..."), so the days never depend on colour alone (NFR-30).
 */
export function DaysChips({ days }: { days: string[] | null }) {
  const set = new Set(days ?? [])
  return (
    <span role="img" aria-label={t.timetable.runsOnLabel(allDays.filter((d) => set.has(d)))} className="inline-flex gap-[3px]">
      {allDays.map((d) => (
        <span
          key={d}
          aria-hidden="true"
          className={`inline-flex size-[22px] items-center justify-center rounded-sm text-caption font-bold ${
            set.has(d) ? 'border-[1.5px] border-ink bg-ink text-page' : 'border border-dashed border-muted text-muted'
          }`}
        >
          {d[0]}
        </span>
      ))}
    </span>
  )
}

/** The loading, failed and empty states shared by the three tabs. */
export function ListState({
  loading,
  failed,
  onRetry,
  empty,
  children,
  title,
}: {
  loading: boolean
  failed: boolean
  onRetry: () => void
  /** The sentence for "nothing here", or null when there are rows. */
  empty: string | null
  children: ReactNode
  title?: string
}) {
  return (
    <Panel title={title}>
      {failed ? (
        <div className="flex flex-wrap items-center gap-md">
          <p role="alert" className="m-0 flex items-center gap-sm text-[15px] font-semibold">
            <Icon name="warning" size={20} />
            {t.timetable.loadFailed}
          </p>
          <Button onClick={onRetry}>{t.timetable.retry}</Button>
        </div>
      ) : loading ? (
        <p className="m-0 text-label text-muted">{t.timetable.loading}</p>
      ) : empty ? (
        <p className="m-0 text-[15px]">{empty}</p>
      ) : (
        children
      )}
    </Panel>
  )
}
