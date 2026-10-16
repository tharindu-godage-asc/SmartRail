import { NavLink, Outlet } from 'react-router'
import { useAuth } from 'react-oidc-context'
import { config } from '../../core/config'
import { freshnessOf } from '../../core/freshness'
import { Icon } from '../../core/Icon'
import { t } from '../../core/strings'
import { formatClock, formatDayAndTime } from '../../core/time'
import { Button, Footer, SafetyBar } from '../../core/ui'
import { Wordmark } from '../../core/Wordmark'
import { useNow } from '../../core/useNow'
import { useAnnouncement } from '../../features/exceptions/announcements'
import { useExceptions } from '../../features/exceptions/queries'
import { useOverview } from '../../features/overview/queries'

/**
 * The operations layout from the designs: safety notice, header, side menu, content. It also owns the two polled reads, so the menu
 * count, the freshness banner and the screen-reader announcements work on every screen. Other modes (displays, D-23) get their own folder.
 */
export function OpsShell() {
  const auth = useAuth()
  const exceptions = useExceptions()
  const overview = useOverview()
  const now = useNow()

  const freshness = freshnessOf([exceptions, overview], now, config.staleAfterMs)
  const notUpToDate = freshness.stale || freshness.neverLoaded
  const announcement = useAnnouncement(exceptions.data?.items, exceptions.data?.openCount)
  const openCount = exceptions.data?.openCount
  const name = String(auth.user?.profile.preferred_username ?? auth.user?.profile.name ?? '')

  return (
    <div className="flex min-h-screen flex-col bg-surface text-ink">
      <SafetyBar />

      <header className="flex flex-wrap items-center justify-between gap-md border-b border-line bg-page px-lg py-sm">
        <span className="inline-flex items-center gap-[14px]">
          <Wordmark />
          <span className="border-l border-line pl-[14px] text-label font-semibold text-muted">{t.operations}</span>
        </span>
        <div className="flex flex-wrap items-center gap-[18px] text-label text-muted">
          <span className="inline-flex items-center gap-[6px] font-semibold text-ink">
            {notUpToDate ? (
              <>
                <Icon name="warning" size={16} />
                {t.header.notUpToDate}
              </>
            ) : (
              <>
                <svg width="12" height="12" viewBox="0 0 12 12" aria-hidden="true">
                  <circle cx="6" cy="6" r="4.5" fill="currentColor" />
                </svg>
                {t.header.connected}
              </>
            )}
          </span>
          <span className="tabular-nums">{formatDayAndTime(now)}</span>
          <span className="font-semibold text-ink">
            {name} · {t.header.controller}
          </span>
          <Button onClick={() => void auth.signoutRedirect()} className="min-h-touch">
            {t.header.signOut}
          </Button>
        </div>
      </header>

      <div className="flex grow flex-col md:flex-row">
        <nav aria-label={t.nav.label} className="shrink-0 border-b border-line bg-page pt-[12px] md:w-[220px] md:border-b-0 md:border-r">
          <div className="mb-sm border-b border-line px-[12px] pb-[12px]">
            <div className="mb-[6px] text-[11px] font-bold uppercase tracking-[.6px] text-muted">{t.nav.line}</div>
            <div className="flex min-h-[48px] items-center gap-[10px] rounded-md border-[1.5px] border-ink px-[10px] text-[15px] font-bold">
              <Icon name="layers" size={20} />
              {t.nav.mainLine}
            </div>
          </div>
          <ul className="m-0 flex list-none flex-row p-0 md:flex-col">
            <MenuItem to="/" end label={t.nav.overview} />
            <MenuItem to="/exceptions" label={t.nav.exceptions} count={openCount} />
          </ul>
        </nav>

        <main className="flex min-w-0 grow flex-col gap-[14px] px-lg py-[20px]">
          {notUpToDate ? (
            <div
              role="status"
              className="flex items-start gap-[10px] rounded-md border-[3px] border-warning bg-warning-bg p-[12px] text-label font-semibold text-warning"
            >
              <Icon name="warning" size={22} />
              <span>
                {freshness.lastGood === null
                  ? t.freshness.neverLoaded
                  : `${t.freshness.banner}. ${t.freshness.lastGood} ${formatClock(freshness.lastGood)}.`}
              </span>
            </div>
          ) : null}
          <div className={notUpToDate && freshness.lastGood !== null ? 'opacity-60' : ''}>
            <Outlet />
          </div>
        </main>
      </div>

      <div role="status" aria-live="polite" className="sr-only">
        {announcement}
      </div>
      <Footer />
    </div>
  )
}

function MenuItem({ to, label, count, end }: { to: string; label: string; count?: number; end?: boolean }) {
  return (
    <li className="flex-1 md:flex-none">
      <NavLink
        to={to}
        end={end}
        className={({ isActive }) =>
          `flex min-h-[44px] items-center justify-between border-b-4 px-md text-[15px] text-ink no-underline md:border-b-0 md:border-l-4 ${
            isActive ? 'border-ink bg-surface font-bold' : 'border-transparent font-medium'
          }`
        }
      >
        <span>{label}</span>
        {count === undefined ? null : (
          <span className="rounded-pill border-[1.5px] border-ink px-[7px] text-caption font-bold tabular-nums" aria-label={`${count} open`}>
            {count}
          </span>
        )}
      </NavLink>
    </li>
  )
}
