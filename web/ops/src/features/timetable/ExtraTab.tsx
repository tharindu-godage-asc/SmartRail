import { Link } from 'react-router'
import { t } from '../../core/strings'
import { cell, Th } from '../../core/ui'
import { formatDate } from './dates'
import { ListState } from './parts'
import { useExtra } from './queries'

/** Extra trains: one-off services for chosen dates (spec 15, `Dash-Timetable-Extra`). */
export function ExtraTab() {
  const query = useExtra()
  const rows = query.data ?? []

  return (
    <ListState
      loading={query.isPending}
      failed={query.isError}
      onRetry={() => void query.refetch()}
      empty={query.data && rows.length === 0 ? t.timetable.empty.extra : null}
    >
      <div className="max-h-[min(60vh,520px)] overflow-auto">
        <table className="w-full border-collapse">
          <thead>
            <tr>
              <Th>{t.timetable.cols.service}</Th>
              <Th>{t.timetable.cols.date}</Th>
              <Th>{t.timetable.cols.category}</Th>
              <Th>{t.timetable.cols.route}</Th>
              <Th>{t.timetable.cols.departs}</Th>
              <Th>{t.timetable.cols.stops}</Th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.serviceId}>
                <td className={`${cell} font-bold`}>
                  <Link to={`/timetable/${row.serviceId}`} className="text-ink underline">
                    {row.trainNumber}
                  </Link>
                </td>
                <td className={cell}>{row.date ? formatDate(row.date) : ''}</td>
                <td className={cell}>{t.timetable.categories[row.category] ?? row.category}</td>
                <td className={cell}>{t.timetable.to(row.fromName, row.toName)}</td>
                <td className={cell}>{row.firstDeparture}</td>
                <td className={cell}>{row.stopsLabel}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </ListState>
  )
}
