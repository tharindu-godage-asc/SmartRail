import { Link } from 'react-router'
import { Icon } from '../../core/Icon'
import { t } from '../../core/strings'
import { cell, Th } from '../../core/ui'
import { DaysChips, ListState } from './parts'
import { useWeekly } from './queries'

/** The weekly schedule: the current version of each weekly service (spec 15, `Dash-Timetable`). */
export function WeeklyTab() {
  const query = useWeekly()
  const rows = query.data ?? []

  return (
    <ListState
      loading={query.isPending}
      failed={query.isError}
      onRetry={() => void query.refetch()}
      empty={query.data && rows.length === 0 ? t.timetable.empty.weekly : null}
    >
      <div className="overflow-x-auto">
        <table className="w-full border-collapse">
          <thead>
            <tr>
              <Th>{t.timetable.cols.service}</Th>
              <Th>{t.timetable.cols.category}</Th>
              <Th>{t.timetable.cols.route}</Th>
              <Th>{t.timetable.cols.runsOn}</Th>
              <Th>{t.timetable.cols.departs}</Th>
              <Th>{t.timetable.cols.stops}</Th>
              <Th>{t.timetable.cols.version}</Th>
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
                <td className={cell}>{t.timetable.categories[row.category] ?? row.category}</td>
                <td className={cell}>
                  {t.timetable.to(row.fromName, row.toName)}
                  {row.serviceType === 'Express' ? `, ${t.timetable.types.Express?.toLowerCase()}` : ''}
                </td>
                <td className={cell}>
                  <DaysChips days={row.runsOn} />
                </td>
                <td className={cell}>{row.firstDeparture}</td>
                <td className={cell}>{row.stopsLabel}</td>
                <td className={cell}>
                  <span className="inline-flex flex-wrap items-center gap-sm">
                    {t.timetable.version(row.version)}
                    {row.amended ? (
                      <span className="inline-flex items-center gap-xs rounded-pill border-[1.5px] border-ink px-[7px] text-caption font-bold">
                        <Icon name="done" size={13} />
                        {t.timetable.amended}
                      </span>
                    ) : null}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="m-0 text-label text-muted">{t.timetable.note}</p>
    </ListState>
  )
}
