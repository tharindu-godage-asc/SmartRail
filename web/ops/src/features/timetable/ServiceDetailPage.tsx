import { useState } from 'react'
import { Link, useLocation, useParams } from 'react-router'
import { ApiError } from '../../core/api/client'
import { Icon } from '../../core/Icon'
import { t } from '../../core/strings'
import { formatDayAndTime } from '../../core/time'
import { Button, cell, Panel, Th } from '../../core/ui'
import { CancelDialog } from './CancelDialog'
import { formatDate, formatRange } from './dates'
import { DaysChips } from './parts'
import { useServiceDetail } from './queries'

/**
 * One service: its stops, its versions with what changed, and its cancellations (spec 15, `Dash-ServiceDetail`). Read only here: the
 * Cancel and Amend actions are not offered until they exist, so there is no button that goes nowhere.
 */
export function ServiceDetailPage() {
  const { serviceId = '' } = useParams()
  const query = useServiceDetail(serviceId)
  const notFound = query.error instanceof ApiError && query.error.status === 404
  const detail = query.data
  const location = useLocation()
  const [cancelling, setCancelling] = useState(false)
  const [done, setDone] = useState<string | null>(null)
  const message = done ?? (location.state as { message?: string } | null)?.message
  // Amend is for the newest version of a weekly service only (spec 15 rule 9).
  const newest = detail ? detail.versions[detail.versions.length - 1] : undefined
  const isNewest = newest === undefined || newest.serviceId === detail?.serviceId

  return (
    <div className="flex flex-col gap-[14px]">
      <Link to="/timetable" className="inline-flex min-h-touch items-center text-[15px] font-semibold text-ink underline">
        ← {t.timetable.back}
      </Link>

      {detail === undefined ? (
        <Panel>
          {query.isError ? (
            <div className="flex flex-wrap items-center gap-md">
              <p role="alert" className="m-0 flex items-center gap-sm text-[15px] font-semibold">
                <Icon name="warning" size={20} />
                {notFound ? t.timetable.detail.unknownService : t.timetable.loadFailed}
              </p>
              {notFound ? null : <Button onClick={() => void query.refetch()}>{t.timetable.retry}</Button>}
            </div>
          ) : (
            <p className="m-0 text-label text-muted">{t.timetable.loading}</p>
          )}
        </Panel>
      ) : (
        <>
          <div>
            <div className="text-label text-muted">
              {detail.runsOn === null ? t.timetable.detail.extraOn(formatDate(detail.effectiveFrom)) : t.timetable.detail.weeklyFrom(formatDate(detail.effectiveFrom))}
            </div>
            <h1 className="m-0 text-heading">
              {t.timetable.title} · {detail.trainNumber}
            </h1>
            <p className="m-0 mt-xs flex flex-wrap items-center gap-md text-[15px]">
              <span>{t.timetable.categories[detail.category] ?? detail.category}</span>
              <span>{t.timetable.types[detail.serviceType] ?? detail.serviceType}</span>
              {detail.runsOn ? <DaysChips days={detail.runsOn} /> : null}
              <span className="font-semibold">{t.timetable.version(detail.version)}</span>
            </p>
          </div>

          {message ? (
            <p role="status" className="m-0 rounded-md border-[1.5px] border-ink bg-page p-[12px] text-label font-semibold">
              {message}
            </p>
          ) : null}

          <div className="flex flex-wrap items-center gap-[10px]">
            {detail.kind === 'Weekly' && isNewest ? (
              <Link
                to={`/timetable/${detail.serviceId}/amend`}
                className="inline-flex min-h-touch items-center rounded-md border-[1.5px] border-ink bg-page px-[18px] text-[15px] font-bold text-ink no-underline"
              >
                {t.timetable.actions.amendService}
              </Link>
            ) : detail.kind === 'Weekly' && newest ? (
              <span className="text-label text-muted">
                {t.timetable.actions.oldVersion}{' '}
                <Link to={`/timetable/${newest.serviceId}`} className="text-ink underline">
                  {t.timetable.actions.goCurrent}
                </Link>
              </span>
            ) : (
              <span className="text-label text-muted">{t.timetable.actions.extraNoAmend}</span>
            )}
            <Button onClick={() => setCancelling(true)}>{t.timetable.actions.cancelService}</Button>
          </div>
          <p className="m-0 flex items-start gap-sm text-label text-muted">
            <Icon name="info" size={18} />
            {t.timetable.actions.notSentYet}
          </p>

          {cancelling ? (
            <CancelDialog
              detail={detail}
              onClose={() => setCancelling(false)}
              onDone={(text) => {
                setDone(text)
                setCancelling(false)
              }}
            />
          ) : null}

          <Panel title={t.timetable.detail.stops}>
            <div className="max-h-[min(60vh,520px)] overflow-auto">
              <table className="w-full border-collapse">
                <thead>
                  <tr>
                    <Th>{t.timetable.detail.station}</Th>
                    <Th>{t.timetable.detail.arrives}</Th>
                    <Th>{t.timetable.detail.leaves}</Th>
                    <Th>{t.timetable.detail.platform}</Th>
                    <Th>{t.timetable.wizard.notes}</Th>
                  </tr>
                </thead>
                <tbody>
                  {detail.stops.map((stop) => (
                    <tr key={stop.stationCode}>
                      <td className={`${cell} font-bold`}>
                        {stop.names.en ?? stop.stationCode}
                        {stop.names.si ? (
                          <span lang="si" className="ml-sm font-normal text-muted">
                            {stop.names.si}
                          </span>
                        ) : null}
                      </td>
                      <td className={cell}>{stop.arrival ?? '—'}</td>
                      <td className={cell}>{stop.departure ?? '—'}</td>
                      <td className={cell}>{stop.platform ? `${t.timetable.detail.platform} ${stop.platform}` : t.timetable.detail.notSet}</td>
                      <td className={cell}>{stop.isFirst ? t.timetable.detail.first : stop.isLast ? t.timetable.detail.last : ''}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Panel>

          <Panel title={t.timetable.detail.versions}>
            <ul className="m-0 flex list-none flex-col gap-sm p-0">
              {detail.versions.map((version) => (
                <li key={version.serviceId} className="flex flex-col gap-px rounded-md border border-line p-sm text-label">
                  <span className="font-bold">
                    {t.timetable.version(version.version)} · {formatRange(version.effectiveFrom, version.effectiveTo)}
                  </span>
                  <span className="text-muted">
                    {version.summary ?? t.timetable.detail.noSummary}
                    {version.createdBy ? ` · ${t.timetable.detail.by} ${version.createdBy}` : ''}
                    {version.createdAt ? ` · ${formatDayAndTime(version.createdAt)}` : ''}
                  </span>
                </li>
              ))}
            </ul>
          </Panel>

          {detail.cancellations.length > 0 ? (
            <Panel title={t.timetable.detail.cancellations}>
              <ul className="m-0 flex list-none flex-col gap-sm p-0">
                {detail.cancellations.map((c) => (
                  <li key={c.cancellationId} className="flex flex-col gap-px rounded-md border border-line p-sm text-label">
                    <span className="font-bold">{formatRange(c.from, c.to)}</span>
                    <span>{c.reason}</span>
                    <span className="text-muted">
                      {t.timetable.detail.by} {c.cancelledBy} · {formatDayAndTime(c.cancelledAt)}
                    </span>
                  </li>
                ))}
              </ul>
            </Panel>
          ) : null}

          <p className="m-0 text-label text-muted">{t.timetable.detail.neverDeleted}</p>
          <p className="m-0 text-label text-muted">{t.timetable.detail.journeysNote}</p>
        </>
      )}
    </div>
  )
}
