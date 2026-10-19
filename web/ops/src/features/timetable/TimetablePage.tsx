import { Link, NavLink, Outlet, useLocation } from 'react-router'
import { t } from '../../core/strings'
import { useCancelled, useExtra, useWeekly } from './queries'

const linkButton =
  'inline-flex min-h-touch items-center rounded-md border-[1.5px] border-ink px-[18px] text-[15px] font-bold no-underline'

/**
 * The Timetable screen (spec 15): a title, two actions and three tabs. The tabs are links, like the Exceptions tabs, so one can be
 * bookmarked and the back button works. A message from the last change ("Service 8072 scheduled") is shown above the tabs.
 */
export function TimetablePage() {
  const weekly = useWeekly()
  const extra = useExtra()
  const cancelled = useCancelled()
  const location = useLocation()
  const message = (location.state as { message?: string } | null)?.message

  return (
    <div className="flex flex-col gap-[14px]">
      <div className="flex flex-wrap items-end justify-between gap-md">
        <div>
          <div className="text-label text-muted">{t.nav.mainLine}</div>
          <h1 className="m-0 text-heading">{t.timetable.title}</h1>
        </div>
        <div className="flex flex-wrap gap-[10px]">
          <Link to="/timetable/extra/new" className={`${linkButton} bg-page text-ink`}>
            {t.timetable.addExtra}
          </Link>
          <Link to="/timetable/new" className={`${linkButton} bg-ink text-page`}>
            {t.timetable.newWeekly}
          </Link>
        </div>
      </div>

      {message ? (
        <p role="status" className="m-0 rounded-md border-[1.5px] border-ink bg-page p-[12px] text-label font-semibold">
          {message}
        </p>
      ) : null}

      <nav aria-label={t.timetable.tabs.label} className="flex flex-wrap gap-xs border-b border-line">
        <Tab to="/timetable" end label={t.timetable.tabs.weekly} count={weekly.data?.length} />
        <Tab to="/timetable/extra" label={t.timetable.tabs.extra} count={extra.data?.length} />
        <Tab to="/timetable/cancelled" label={t.timetable.tabs.cancelled} count={cancelled.data?.length} />
      </nav>

      <Outlet />
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
