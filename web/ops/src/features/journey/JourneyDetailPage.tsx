import { Link, useParams, useSearchParams } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { config } from '../../core/config'
import { ApiError } from '../../core/api/client'
import type { ControllerJourneyDetail, DetailEvent, DetailStop } from '../../core/api/types'
import { Icon } from '../../core/Icon'
import { journeyStatusLabel, t } from '../../core/strings'
import { formatClockShort, formatDayAndTime } from '../../core/time'
import { cell, Panel, StatusChip, Th } from '../../core/ui'
import { useApi } from '../auth/apiContext'
import { TrackView } from './TrackView'

const j = t.journeyDetail

function useJourneyDetail(journeyId: string) {
  const { api } = useApi()
  return useQuery({
    queryKey: ['journeyDetail', journeyId],
    queryFn: ({ signal }) => api.get<ControllerJourneyDetail>(`/api/v1/controller/journeys/${journeyId}`, signal),
    refetchInterval: config.pollMs,
    refetchIntervalInBackground: false,
    retry: false,
    networkMode: 'always',
  })
}

/** The time a stop shows: what happened, otherwise the estimate, otherwise the schedule. */
function stopTime(stop: DetailStop): { text: string; kind: 'actual' | 'estimated' | 'scheduled' } | null {
  const actual = stop.actualDeparture ?? stop.actualArrival
  if (actual) return { text: formatClockShort(actual), kind: 'actual' }
  const estimated = stop.estimatedArrival ?? stop.estimatedDeparture
  if (estimated) return { text: formatClockShort(estimated), kind: 'estimated' }
  const scheduled = stop.scheduledArrival ?? stop.scheduledDeparture
  return scheduled ? { text: formatClockShort(scheduled), kind: 'scheduled' } : null
}

function eventText(e: DetailEvent): string {
  switch (e.type) {
    case 'Departed':
      return j.events.departed(e.source, e.stationName ?? '')
    case 'Arrived':
      return j.events.arrived(e.source, e.stationName ?? '')
    case 'DelayReported':
      return j.events.delay(e.source, e.delayUnknown ? j.delayUnknown : j.minutes(e.totalDelayMinutes ?? 0), j.reasons[e.reason ?? ''] ?? e.reason ?? '')
    case 'ReasonVerified':
      return j.events.verified(j.reasons[e.reasonCode ?? ''] ?? e.reasonCode ?? '')
    default:
      return e.type
  }
}

/**
 * The controller's view of one journey (spec 16 F1, read only): where the train is, every stop with its times and state, the events
 * with who made them, the operator and the delay reason. Positions are places, never predictions. Refreshes every 10 s.
 */
export function JourneyDetailPage() {
  const { journeyId = '' } = useParams()
  const [params] = useSearchParams()
  const query = useJourneyDetail(journeyId)
  const d = query.data
  const search = params.toString() ? `?${params.toString()}` : ''

  if (query.isError && query.error instanceof ApiError && query.error.status === 404) {
    return (
      <div className="flex flex-col gap-md">
        <BackLink />
        <p role="alert" className="m-0 text-[15px] font-semibold">
          {j.notFound}
        </p>
      </div>
    )
  }

  if (!d) {
    return (
      <div className="flex flex-col gap-md">
        <BackLink />
        <p className="m-0 text-label text-muted">{query.isError ? j.loadFailed : t.loading}</p>
      </div>
    )
  }

  const first = d.stops[0]
  const last = d.stops[d.stops.length - 1]
  const reason = d.verifiedReason ? j.reasons[d.verifiedReason.code] ?? d.verifiedReason.code : null

  return (
    <div className="flex flex-col gap-[14px]">
      <BackLink />
      <div className="flex flex-wrap items-start justify-between gap-md">
        <div>
          <div className="text-label text-muted">
            {t.nav.mainLine} · {j.direction[d.direction] ?? d.direction} · {j.category[d.category] ?? d.category}
          </div>
          <h1 className="m-0 text-heading">
            {j.title(d.trainNumber)} {first && last ? `· ${first.stationName} → ${last.stationName}` : ''}
          </h1>
        </div>
        <div className="flex flex-wrap items-center gap-sm">
          <StatusChip group={d.statusGroup}>{journeyStatusLabel(d)}</StatusChip>
          {d.verifiedReason ? (
            <StatusChip group="normal" icon="verified">
              {j.verifiedAt(formatClockShort(d.verifiedReason.verifiedAt))}
            </StatusChip>
          ) : null}
        </div>
      </div>

      <div className="grid gap-[14px] lg:grid-cols-[minmax(0,1fr)_340px]">
        <div className="flex min-w-0 flex-col gap-[14px]">
          <Panel title={j.stops}>
            <TrackView detail={d} />
            <p className="m-0 text-label">{positionSentence(d)}</p>
            <div className="max-h-[min(60vh,520px)] overflow-auto">
              <table className="w-full border-collapse">
                <thead>
                  <tr>
                    <Th>{j.cols.stop}</Th>
                    <Th>{j.cols.scheduled}</Th>
                    <Th>{j.cols.estimated}</Th>
                    <Th>{j.cols.state}</Th>
                    <Th>{j.cols.platform}</Th>
                  </tr>
                </thead>
                <tbody>
                  {d.stops.map((stop) => {
                    const time = stopTime(stop)
                    const scheduled = stop.scheduledArrival ?? stop.scheduledDeparture
                    return (
                      <tr key={stop.serviceStopId}>
                        <td className={`${cell} font-bold`}>{stop.stationName}</td>
                        <td className={cell}>{scheduled ? formatClockShort(scheduled) : '—'}</td>
                        <td className={cell}>{time ? `${time.text}${time.kind === 'estimated' ? ` (${j.est})` : ''}` : '—'}</td>
                        <td className={cell}>{j.states[stop.state] ?? stop.state}</td>
                        <td className={cell}>
                          {stop.platform ?? stop.plannedPlatform ?? '—'}
                          {stop.platform && stop.plannedPlatform && stop.platform !== stop.plannedPlatform ? (
                            <span className="ml-xs text-caption text-muted">({j.planned(stop.plannedPlatform)})</span>
                          ) : null}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          </Panel>

          <Panel title={j.events.title}>
            {d.events.length === 0 ? (
              <p className="m-0 text-label text-muted">{j.events.none}</p>
            ) : (
              <ul className="m-0 flex max-h-[min(50vh,420px)] list-none flex-col gap-[8px] overflow-auto p-0">
                {d.events.map((e) => (
                  <li key={e.id} className="text-[15px] leading-snug">
                    <span className="tabular-nums text-muted">{formatClockShort(e.at)}</span> · {eventText(e)}
                    <span className="text-muted"> ({e.actor})</span>
                  </li>
                ))}
              </ul>
            )}
          </Panel>
        </div>

        <div className="flex min-w-0 flex-col gap-[14px]">
          <Panel title={j.delayReason}>
            {d.reportedReason === null ? (
              <p className="m-0 text-label text-muted">{j.noDelayReason}</p>
            ) : (
              <>
                <p className="m-0 text-[15px]">{j.reported(j.reasons[d.reportedReason] ?? d.reportedReason)}</p>
                {d.verifiedReason ? (
                  <p className="m-0 flex items-start gap-sm text-label font-semibold">
                    <Icon name="verified" size={18} />
                    {j.publishedToPassengers(reason ?? '', formatClockShort(d.verifiedReason.verifiedAt))}
                  </p>
                ) : (
                  <p className="m-0 flex items-start gap-sm text-label">
                    <Icon name="help" size={18} />
                    <span>
                      {j.notVerified} <Link to="/exceptions" className="font-semibold text-ink underline">{j.openExceptions}</Link>
                    </span>
                  </p>
                )}
              </>
            )}
          </Panel>

          <Panel title={j.crew.title}>
            <div className="text-label font-semibold text-muted">{j.crew.operator}</div>
            {d.operator ? (
              <>
                <div className="text-[20px] font-bold">{d.operator.name}</div>
                <div className="text-label text-muted">
                  {d.operator.validTo ? j.crew.assignedUntil(formatDayAndTime(d.operator.validFrom), formatDayAndTime(d.operator.validTo)) : j.crew.assignedFrom(formatDayAndTime(d.operator.validFrom))}
                </div>
              </>
            ) : (
              <div className="flex items-center gap-sm text-[15px] font-semibold">
                <Icon name="help" size={18} />
                {j.crew.none}
              </div>
            )}
            <div className="flex flex-wrap gap-sm">
              <Link
                to={`/journeys/${journeyId}/assign${search}`}
                className="inline-flex min-h-touch items-center rounded-md border-[1.5px] border-ink bg-ink px-[16px] text-[15px] font-bold text-page no-underline"
              >
                {t.assignments.assign}
              </Link>
              <Link
                to={`/journeys/${journeyId}/crew${search}`}
                className="inline-flex min-h-touch items-center rounded-md border-[1.5px] border-ink bg-page px-[16px] text-[15px] font-bold text-ink no-underline"
              >
                {j.crew.history}
              </Link>
            </div>
          </Panel>

          <p className="m-0 text-[13px] leading-snug text-muted">{j.recordsOnly}</p>
        </div>
      </div>
    </div>
  )
}

function positionSentence(d: ControllerJourneyDetail): string {
  const p = d.position
  if (d.status === 'Completed') return j.position.completed
  if (!p) return j.position.notStarted
  return p.state === 'Arrived' ? j.position.at(p.stationName, formatClockShort(p.at)) : j.position.between(p.stationName, formatClockShort(p.at), nextAfter(d, p.stationCode))
}

function nextAfter(d: ControllerJourneyDetail, code: string): string {
  const i = d.stops.findIndex((s) => s.stationCode === code)
  return d.stops[i + 1]?.stationName ?? d.stops[i]?.stationName ?? ''
}

function BackLink() {
  const [params] = useSearchParams()
  const search = params.get('date') ? `?date=${params.get('date')}` : ''
  return (
    <Link to={`/journeys/all${search}`} className="inline-flex min-h-touch items-center self-start text-[15px] font-semibold text-ink underline">
      ← {j.back}
    </Link>
  )
}
