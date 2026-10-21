import { Link } from 'react-router'
import { Icon } from '../../core/Icon'
import { eventLabel, exceptionTypeLabel, journeyStatusLabel, sourceLabel, t } from '../../core/strings'
import { formatAge, formatClock, formatClockShort, formatMinutes } from '../../core/time'
import { cell, KpiCard, Panel, SeverityBadge, StatusChip, Th } from '../../core/ui'
import { useNow } from '../../core/useNow'
import { liveAge, useExceptions, whereOf } from '../exceptions/queries'
import { useOverview } from './queries'

export function OverviewPage() {
  const overview = useOverview()
  const exceptions = useExceptions()
  const now = useNow()

  const journeys = overview.data?.journeys
  const overdue = overview.data ? (overview.data.overdueToStart ?? []) : undefined
  const items = exceptions.data?.items ?? []
  const critical = items.filter((item) => item.severity === 'Critical').length
  // Both numbers come from the open exceptions (a reason is waiting until a controller resolves it), so the count and its note agree.
  const waiting = items.filter((item) => item.type === 'ReasonAwaitingVerification')
  const awaiting = exceptions.data ? waiting.length : undefined
  const oldestAwaiting = waiting
    .reduce<number | null>((oldest, item) => {
      const age = liveAge(item, exceptions.dataUpdatedAt, now)
      return oldest === null || age > oldest ? age : oldest
    }, null)

  return (
    <div className="flex flex-col gap-[14px]">
      <div className="flex items-end justify-between">
        <div>
          <div className="text-label text-muted">{t.nav.mainLine}</div>
          <h1 className="m-0 text-heading">{t.overview.title}</h1>
        </div>
        {overview.dataUpdatedAt > 0 ? (
          <span className="text-label text-muted tabular-nums">
            {t.freshness.updated} {formatClock(overview.dataUpdatedAt)}
          </span>
        ) : null}
      </div>

      <div className="flex flex-wrap gap-[14px]">
        <KpiCard label={t.overview.runningNow} value={journeys?.length ?? '—'} />
        <KpiCard label={t.overview.overdueKpi} value={overdue?.length ?? '—'} note={overdue && overdue.length > 0 ? t.overview.overdueNote : undefined} />
        <KpiCard
          label={t.overview.openExceptions}
          value={overview.data?.openExceptionCount ?? '—'}
          note={exceptions.data ? t.overview.critical(critical) : undefined}
        />
        <KpiCard
          label={t.overview.awaitingVerification}
          value={awaiting ?? '—'}
          note={oldestAwaiting === null ? undefined : t.overview.oldest(formatAge(oldestAwaiting))}
        />
      </div>

      {overdue !== undefined && overdue.length > 0 ? (
        <Panel title={t.overview.overdueTitle}>
          <p className="m-0 flex items-start gap-sm text-label">
            <Icon name="warning" size={18} />
            {t.overview.overdueHelp}
          </p>
          <div className="max-h-[min(40vh,320px)] overflow-auto">
            <table className="w-full border-collapse">
              <thead>
                <tr>
                  <Th>{t.overview.overdueCols.train}</Th>
                  <Th>{t.overview.overdueCols.route}</Th>
                  <Th>{t.overview.overdueCols.scheduled}</Th>
                  <Th>{t.overview.overdueCols.late}</Th>
                  <Th>{t.overview.overdueCols.operator}</Th>
                  <Th>{t.overview.overdueCols.action}</Th>
                </tr>
              </thead>
              <tbody>
                {overdue.map((j) => (
                  <tr key={j.journeyId}>
                    <td className={`${cell} font-bold`}>{j.trainNumber}</td>
                    <td className={cell}>{t.timetable.to(j.from, j.to)}</td>
                    <td className={cell}>{formatClockShort(j.scheduledDeparture)}</td>
                    <td className={cell}>
                      <span className="inline-flex items-center gap-xs font-semibold">
                        <Icon name="warning" size={16} />
                        {formatMinutes(j.minutesLate)}
                      </span>
                    </td>
                    <td className={cell}>{t.overview.overdueOperator(j.hasOperator)}</td>
                    <td className={cell}>
                      <Link
                        to={j.hasOperator ? `/journeys/${j.journeyId}` : `/journeys/${j.journeyId}/assign`}
                        className="inline-flex min-h-touch items-center text-ink underline"
                      >
                        {t.overview.overdueAction(j.hasOperator)}
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
      ) : null}

      <Panel title={t.overview.running}>
        {journeys === undefined ? (
          <p className="m-0 text-label text-muted">{overview.isError ? t.freshness.neverLoaded : t.loading}</p>
        ) : journeys.length === 0 ? (
          <p className="m-0 text-[15px]">{t.overview.noTrains}</p>
        ) : (
          <div className="max-h-[min(60vh,520px)] overflow-auto">
            <table className="w-full border-collapse">
              <thead>
                <tr>
                  <Th>{t.overview.columns.train}</Th>
                  <Th>{t.overview.columns.route}</Th>
                  <Th>{t.overview.columns.nextStop}</Th>
                  <Th>{t.overview.columns.nextTime}</Th>
                  <Th>{t.overview.columns.status}</Th>
                  <Th>{t.overview.columns.lastReport}</Th>
                  <Th>{t.overview.columns.verification}</Th>
                </tr>
              </thead>
              <tbody>
                {journeys.map((journey) => (
                  <tr key={journey.journeyId}>
                    <td className={`${cell} font-bold`}>
                      <Link to={`/journeys/${journey.journeyId}`} className="text-ink underline underline-offset-4">
                        {journey.trainNumber}
                      </Link>
                    </td>
                    <td className={cell}>
                      {journey.from} → {journey.to}
                    </td>
                    <td className={cell}>{journey.nextStop?.stationName ?? t.overview.noNextStop}</td>
                    <td className={cell}>{journey.nextStop ? formatClockShort(journey.nextStop.expectedAt) : t.overview.noNextStop}</td>
                    <td className={cell}>
                      <StatusChip group={journey.statusGroup}>{journeyStatusLabel(journey)}</StatusChip>
                    </td>
                    <td className={cell}>
                      {journey.lastEvent === null ? (
                        t.overview.noReportYet
                      ) : (
                        <div className="flex flex-col gap-px">
                          <span>
                            {eventLabel(journey.lastEvent.type)}
                            {journey.lastEvent.stationName ? ` · ${journey.lastEvent.stationName}` : ''}
                          </span>
                          <span className="inline-flex items-center gap-xs text-caption text-muted">
                            <Icon name="person" size={14} />
                            {sourceLabel(journey.lastEvent.source)}
                            {journey.secondsSinceUpdate === null
                              ? ''
                              : ` · ${t.overview.ago(formatAge(journey.secondsSinceUpdate + Math.max(0, (now - overview.dataUpdatedAt) / 1000)))}`}
                          </span>
                        </div>
                      )}
                    </td>
                    <td className={cell}>
                      {journey.verification === 'awaiting' ? (
                        <span className="inline-flex items-center gap-xs font-semibold">
                          <Icon name="help" size={16} />
                          {t.overview.verificationAwaiting}
                        </span>
                      ) : (
                        t.overview.verificationNone
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>

      <Panel title={t.overview.latest}>
        {items.length === 0 ? (
          <p className="m-0 text-label text-muted">{t.exceptions.none}</p>
        ) : (
          <ul className="m-0 flex list-none flex-col gap-sm p-0">
            {items.slice(0, 3).map((item) => (
              <li key={item.id} className="flex flex-wrap items-center gap-[10px] text-label">
                <SeverityBadge severity={item.severity} />
                <span className="font-bold">{exceptionTypeLabel(item.type)}</span>
                <span>{whereOf(item)}</span>
                <span className="text-muted tabular-nums">{formatAge(liveAge(item, exceptions.dataUpdatedAt, now))}</span>
              </li>
            ))}
          </ul>
        )}
        <Link to="/exceptions" className="min-h-touch self-start py-sm text-label font-semibold text-ink underline underline-offset-4">
          {t.overview.viewAll}
        </Link>
      </Panel>
    </div>
  )
}
