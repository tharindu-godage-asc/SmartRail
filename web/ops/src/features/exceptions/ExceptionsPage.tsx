import { NavLink, Outlet } from 'react-router'
import { t } from '../../core/strings'
import { useExceptions } from './queries'

/**
 * The Exceptions screen: a title and two tabs, Open and Closed (spec 08 section 14). The tabs are links, so a tab can be bookmarked,
 * the back button works, and the current one is announced as the current page.
 */
export function ExceptionsPage() {
  const open = useExceptions()
  const count = open.data?.openCount

  return (
    <div className="flex flex-col gap-[14px]">
      <div>
        <div className="text-label text-muted">{t.nav.mainLine}</div>
        <h1 className="m-0 text-heading">{t.exceptions.title}</h1>
      </div>

      <nav aria-label={t.exceptions.tabs.label} className="flex gap-xs border-b border-line">
        <Tab to="/exceptions" end label={t.exceptions.tabs.open} count={count} />
        <Tab to="/exceptions/closed" label={t.exceptions.tabs.closed} />
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
        <span className="rounded-pill border-[1.5px] border-ink px-[7px] text-caption font-bold tabular-nums" aria-label={`${count} open`}>
          {count}
        </span>
      )}
    </NavLink>
  )
}
