import { Link, NavLink, useLocation, useSearchParams } from 'react-router'
import { Icon } from '../../core/Icon'
import { t } from '../../core/strings'
import { formatClockShort, todayInColombo } from '../../core/time'
import { Button, cell, Panel, Th } from '../../core/ui'
import { formatDate } from '../timetable/dates'
import { inputClass } from '../timetable/wizard/styles'
import { useAssignmentJourneys } from './queries'

/**
 * The journeys of one day (spec 17 A2, `Dash-Journeys-Unassigned`): the Unassigned tab lists those nobody covers, with Assign operator;
 * All journeys shows who is assigned. The day is chosen with a date box; a message from the last change is shown above the tabs.
 */
export function JourneysPage({ all = false }: { all?: boolean }) {
  const [params, setParams] = useSearchParams()
  const date = params.get('date') ?? todayInColombo()
  const unassigned = useAssignmentJourneys(date, 'unassigned')
  const everything = useAssignmentJourneys(date, 'all')
  const query = all ? everything : unassigned
  const rows = query.data ?? []
  const location = useLocation()
  const message = (location.state as { message?: string } | null)?.message
  const search = `?date=${date}`

  return (
    <div className="flex flex-col gap-[14px]">
      <div className="flex flex-wrap items-end justify-between gap-md">
        <div>
          <div className="text-label text-muted">{t.nav.mainLine}</div>
          <h1 className="m-0 text-heading">{t.assignments.title}</h1>
        </div>
        <div className="flex flex-col gap-xs">
          <label htmlFor="journeys-date" className="text-label font-bold">
            {t.assignments.date}
          </label>
          <input
            id="journeys-date"
            type="date"
            className={inputClass}
            value={date}
            onChange={(e) => e.target.value && setParams({ date: e.target.value })}
          />
        </div>
      </div>

      {message ? (
        <p role="status" className="m-0 rounded-md border-[1.5px] border-ink bg-page p-[12px] text-label font-semibold">
          {message}
        </p>
      ) : null}

      <nav aria-label={t.assignments.tabs.label} className="flex flex-wrap gap-xs border-b border-line">
        <Tab to={`/journeys${search}`} end label={t.assignments.tabs.unassigned} count={unassigned.data?.length} />
        <Tab to={`/journeys/all${search}`} label={t.assignments.tabs.all} count={everything.data?.length} />
      </nav>

      <Panel>
        {query.isError ? (
          <div className="flex flex-wrap items-center gap-md">
            <p role="alert" className="m-0 flex items-center gap-sm text-[15px] font-semibold">
              <Icon name="warning" size={20} />
              {t.assignments.loadFailed}
            </p>
            <Button onClick={() => void query.refetch()}>{t.assignments.retry}</Button>
          </div>
        ) : query.isPending ? (
          <p className="m-0 text-label text-muted">{t.assignments.loading}</p>
        ) : rows.length === 0 ? (
          <p className="m-0 text-[15px]">{all ? t.assignments.empty.all : t.assignments.empty.unassigned}</p>
        ) : (
          <div className="max-h-[min(60vh,520px)] overflow-auto">
            <table className="w-full border-collapse">
              <thead>
                <tr>
                  <Th>{t.assignments.cols.train}</Th>
                  <Th>{t.assignments.cols.route}</Th>
                  <Th>{t.assignments.cols.departs}</Th>
                  <Th>{t.assignments.cols.source}</Th>
                  <Th>{t.assignments.cols.operator}</Th>
                  <Th>{t.assignments.cols.action}</Th>
                </tr>
              </thead>
              <tbody>
                {rows.map((journey) => (
                  <tr key={journey.journeyId}>
                    <td className={`${cell} font-bold`}>
                      <Link to={`/journeys/${journey.journeyId}${search}`} className="text-ink underline underline-offset-4">
                        {journey.trainNumber}
                      </Link>
                    </td>
                    <td className={cell}>{t.timetable.to(journey.from, journey.to)}</td>
                    <td className={cell}>
                      {formatClockShort(journey.firstDeparture)} · {formatDate(journey.operatingDate)}
                    </td>
                    <td className={cell}>{t.assignments.sources[journey.source] ?? journey.source}</td>
                    <td className={cell}>
                      {journey.operator ? (
                        journey.operator.name
                      ) : (
                        <span className="inline-flex items-center gap-xs font-semibold">
                          <Icon name="help" size={16} />
                          {t.assignments.noOperator}
                        </span>
                      )}
                    </td>
                    <td className={cell}>
                      <Link
                        to={journey.operator ? `/journeys/${journey.journeyId}/crew${search}` : `/journeys/${journey.journeyId}/assign${search}`}
                        className="inline-flex min-h-touch items-center text-ink underline"
                      >
                        {journey.operator ? t.assignments.crew : t.assignments.assign}
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>
    </div>
  )
}

function Tab({ to, label, count, end }: { to: string; label: string; count?: number; end?: boolean }) {
  return (
    <NavLink
      to={to}
      end={end}
      className={({ isActive }) =>
        `-mb-px flex min-h-touch items-center gap-sm border-b-4 px-md text-[15px] text-ink no-underline ${
          isActive ? 'border-ink font-bold' : 'border-transparent font-medium'
        }`
      }
    >
      {label}
      {count === undefined ? null : (
        <span className="rounded-pill border-[1.5px] border-ink px-[7px] text-caption font-bold tabular-nums" aria-label={`${count} ${label}`}>
          {count}
        </span>
      )}
    </NavLink>
  )
}
