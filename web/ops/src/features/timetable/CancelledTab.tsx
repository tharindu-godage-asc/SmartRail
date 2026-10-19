import { Link } from 'react-router'
import { t } from '../../core/strings'
import { formatDayAndTime } from '../../core/time'
import { cell, Th } from '../../core/ui'
import { formatRange } from './dates'
import { ListState } from './parts'
import { useCancelled } from './queries'

/**
 * Cancelled services that cover a date from today on, with the reason, who and when (spec 15, `Dash-Timetable-Cancelled`). The reason is
 * free text and is only ever shown as text (spec 14 rule 11).
 */
export function CancelledTab() {
  const query = useCancelled()
  const rows = query.data ?? []

  return (
    <ListState
      loading={query.isPending}
      failed={query.isError}
      onRetry={() => void query.refetch()}
      empty={query.data && rows.length === 0 ? t.timetable.empty.cancelled : null}
    >
      <div className="overflow-x-auto">
        <table className="w-full border-collapse">
          <thead>
            <tr>
              <Th>{t.timetable.cols.service}</Th>
              <Th>{t.timetable.cols.route}</Th>
              <Th>{t.timetable.cols.range}</Th>
              <Th>{t.timetable.cols.reason}</Th>
              <Th>{t.timetable.cols.cancelledBy}</Th>
              <Th>{t.timetable.cols.cancelledAt}</Th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.cancellationId}>
                <td className={`${cell} font-bold`}>
                  <Link to={`/timetable/${row.serviceId}`} className="text-ink underline">
                    {row.trainNumber}
                  </Link>
                </td>
                <td className={cell}>{t.timetable.to(row.fromName, row.toName)}</td>
                <td className={cell}>{formatRange(row.from, row.to)}</td>
                <td className={`${cell} whitespace-normal`}>{row.reason}</td>
                <td className={cell}>{row.cancelledBy}</td>
                <td className={cell}>{formatDayAndTime(row.cancelledAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </ListState>
  )
}
