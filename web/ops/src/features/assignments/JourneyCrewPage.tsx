import { useState } from 'react'
import { Link, useLocation, useParams, useSearchParams } from 'react-router'
import { ApiError } from '../../core/api/client'
import type { AssignmentView } from '../../core/api/types'
import { Icon } from '../../core/Icon'
import { t } from '../../core/strings'
import { formatClockShort, formatDayAndTime, todayInColombo } from '../../core/time'
import { Button, cell, Panel, Th } from '../../core/ui'
import { useNow } from '../../core/useNow'
import { EndDialog } from './EndDialog'
import { useAssignmentJourneys, useJourneyAssignments } from './queries'

/**
 * Crew and control of one journey (spec 17 A2): who is assigned now and who was, how each assignment ended, and the actions: assign
 * an operator (a crew change when one is assigned) and end the assignment that is running now.
 */
export function JourneyCrewPage() {
  const { journeyId = '' } = useParams()
  const [params] = useSearchParams()
  const date = params.get('date') ?? todayInColombo()
  const journeys = useAssignmentJourneys(date, 'all')
  const journey = journeys.data?.find((j) => j.journeyId === journeyId)
  const assignments = useJourneyAssignments(journeyId)
  const [ending, setEnding] = useState<AssignmentView | null>(null)
  const [done, setDone] = useState<string | null>(null)
  const location = useLocation()
  const c = t.assignments.crewPage
  const notFound = assignments.error instanceof ApiError && assignments.error.status === 404
  const rows = assignments.data ?? []
  const now = useNow()
  const active = rows.filter((r) => new Date(r.validFrom).getTime() <= now && (r.validTo === null || new Date(r.validTo).getTime() > now))

  return (
    <div className="flex flex-col gap-[14px]">
      <Link to={`/journeys?date=${date}`} className="inline-flex min-h-touch items-center text-[15px] font-semibold text-ink underline">
        ← {t.assignments.back}
      </Link>
      <div>
        <div className="text-label text-muted">
          {journey ? `${t.timetable.to(journey.from, journey.to)} · ${formatClockShort(journey.firstDeparture)}` : t.nav.mainLine}
        </div>
        <h1 className="m-0 text-heading">{c.title(journey?.trainNumber ?? '')}</h1>
      </div>

      {done || (location.state as { message?: string } | null)?.message ? (
        <p role="status" className="m-0 rounded-md border-[1.5px] border-ink bg-page p-[12px] text-label font-semibold">
          {done ?? (location.state as { message?: string }).message}
        </p>
      ) : null}

      <div className="flex flex-wrap items-center gap-[10px]">
        <Link
          to={`/journeys/${journeyId}/assign?date=${date}`}
          className="inline-flex min-h-touch items-center rounded-md border-[1.5px] border-ink bg-ink px-[18px] text-[15px] font-bold text-page no-underline"
        >
          {t.assignments.assign}
        </Link>
        {active.map((row) => (
          <Button key={row.assignmentId} onClick={() => setEnding(row)}>
            {c.end} · {row.operatorName}
          </Button>
        ))}
      </div>

      <Panel title={c.history}>
        {assignments.isError ? (
          <p role="alert" className="m-0 flex items-center gap-sm text-[15px] font-semibold">
            <Icon name="warning" size={20} />
            {notFound ? c.notFound : t.assignments.loadFailed}
          </p>
        ) : assignments.isPending ? (
          <p className="m-0 text-label text-muted">{t.assignments.loading}</p>
        ) : rows.length === 0 ? (
          <p className="m-0 text-[15px]">{c.none}</p>
        ) : (
          <div className="max-h-[min(60vh,520px)] overflow-auto">
            <table className="w-full border-collapse">
              <thead>
                <tr>
                  <Th>{c.cols.operator}</Th>
                  <Th>{c.cols.from}</Th>
                  <Th>{c.cols.to}</Th>
                  <Th>{c.cols.assignedBy}</Th>
                  <Th>{c.cols.ended}</Th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.assignmentId}>
                    <td className={`${cell} font-bold`}>
                      {row.operatorName}
                      {active.includes(row) ? <span className="ml-sm rounded-pill border-[1.5px] border-ink px-[7px] text-caption font-bold">{c.active}</span> : null}
                    </td>
                    <td className={cell}>{formatDayAndTime(row.validFrom)}</td>
                    <td className={cell}>{row.validTo ? formatDayAndTime(row.validTo) : c.untilEnd}</td>
                    <td className={cell}>{row.assignedBy}</td>
                    <td className={`${cell} whitespace-normal`}>
                      {row.endReason ? (
                        <>
                          {c.endedBy(row.endedBy ?? '', c.reasons[row.endReason] ?? row.endReason)}
                          {row.endNote ? <span className="block text-caption text-muted">{row.endNote}</span> : null}
                        </>
                      ) : (
                        '—'
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>

      {ending ? (
        <EndDialog
          journeyId={journeyId}
          assignment={ending}
          onClose={() => setEnding(null)}
          onDone={() => {
            setEnding(null)
            setDone(`${c.ended} ${c.afterwards}`)
          }}
        />
      ) : null}
    </div>
  )
}
