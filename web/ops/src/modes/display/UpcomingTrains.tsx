import { useEffect, useState } from 'react'
import { formatClockShort } from '../../core/time'
import { d } from './displayStrings'
import { StatusChip, TriText } from './parts'
import { expectedTime, pagesOf, statusOf, statusText, UPCOMING_PAGE_SECONDS } from './screens'
import type { BoardCall } from './types'

/**
 * The trains after the first one on this platform (spec 09 section 16), three at a time. With more than three, the next three rise into
 * place every few seconds, so a passenger sees every train that is coming without anything else on the screen moving. The first train
 * stays the largest element above this panel (D-50).
 */
export function UpcomingTrains({ calls, offline }: { calls: BoardCall[]; offline: boolean }) {
  const pages = pagesOf(calls)
  const [page, setPage] = useState(0)

  useEffect(() => {
    if (pages.length < 2) {
      return
    }

    const id = window.setInterval(() => setPage((current) => current + 1), UPCOMING_PAGE_SECONDS * 1000)
    return () => window.clearInterval(id)
  }, [pages.length])

  if (pages.length === 0) {
    return null
  }

  const index = page % pages.length
  const rows = pages[index] ?? []
  return (
    <section aria-label={d.nextTrains.en} className="flex flex-col gap-[8px] border-t border-line pt-[16px]">
      <div className="flex items-baseline justify-between text-level4 text-muted">
        <TriText text={d.nextTrains} className="font-semibold" />
        {pages.length > 1 ? <span className="tabular-nums">{index + 1} / {pages.length}</span> : null}
      </div>
      {/* Keyed by the page and its first train, so the rows rise in again whenever the page or the list changes. */}
      <ul key={`${index}-${rows[0]?.trainNumber}-${rows[0]?.operatingDate}`} className="animate-rise m-0 flex list-none flex-col p-0">
        {rows.map((call) => (
          <Row key={`${call.trainNumber}-${call.operatingDate}`} call={call} offline={offline} />
        ))}
      </ul>
    </section>
  )
}

function Row({ call, offline }: { call: BoardCall; offline: boolean }) {
  const time = expectedTime(call)
  const status = statusOf(call)
  return (
    <li className="grid grid-cols-[190px_1fr_auto] items-center gap-x-[28px] py-[4px]">
      <span className={`text-level3 font-bold leading-none tabular-nums ${offline ? 'text-muted' : ''}`}>{time ? formatClockShort(time.at) : '—'}</span>
      <span className="flex flex-wrap items-baseline gap-x-[24px]">
        <span lang="en" className="text-[36px] font-bold leading-[1.2]">{call.destination.name.en}</span>
        <span lang="si" className="text-level4 text-muted" style={{ fontFamily: 'var(--font-family-sinhala)' }}>{call.destination.name.si}</span>
        <span lang="ta" className="text-level4 text-muted" style={{ fontFamily: 'var(--font-family-tamil)' }}>{call.destination.name.ta}</span>
        <span className="text-level4 tabular-nums text-muted">{call.trainNumber}</span>
      </span>
      <StatusChip group={status.group} text={statusText(status.kind, call.delayMinutes)} size="small" />
    </li>
  )
}
