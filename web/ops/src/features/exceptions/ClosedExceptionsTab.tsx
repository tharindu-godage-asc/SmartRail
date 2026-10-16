import type { ClosedException } from '../../core/api/types'
import { Icon } from '../../core/Icon'
import { exceptionTypeLabel, t } from '../../core/strings'
import { formatAge, formatClock, formatDayAndTime } from '../../core/time'
import { Panel, SeverityBadge } from '../../core/ui'
import { useClosedExceptions, whereOf } from './queries'

/** The status of a closed exception: icon and words, never colour alone (NFR-30). */
function ClosedChip({ resolution }: { resolution: string }) {
  return (
    <span className="inline-flex items-center gap-xs whitespace-nowrap rounded-pill border-[1.5px] border-ink bg-surface px-[10px] py-[2px] text-label font-bold">
      <Icon name="done" size={16} />
      {t.closed.status(t.actionsPast[resolution] ?? resolution)}
    </span>
  )
}

function Fact({ label, children }: { label: string; children: string }) {
  return (
    <div className="flex flex-col gap-px">
      <dt className="text-caption font-bold text-muted">{label}</dt>
      <dd className="m-0 text-label tabular-nums">{children}</dd>
    </div>
  )
}

function ClosedCard({ item }: { item: ClosedException }) {
  return (
    <li className="list-none">
      <article data-exception-id={item.id} className="flex flex-col gap-[10px] rounded-lg border border-line bg-page p-md">
        <div className="flex flex-wrap items-center gap-[10px]">
          <ClosedChip resolution={item.resolution} />
          <SeverityBadge severity={item.severity} />
        </div>
        <h3 className="m-0 text-[16px] font-bold">
          {exceptionTypeLabel(item.type)} <span className="font-normal">· {whereOf(item)}</span>
        </h3>
        <dl className="m-0 grid grid-cols-1 gap-sm sm:grid-cols-3">
          <Fact label={t.closed.raised}>{formatDayAndTime(item.createdAt)}</Fact>
          <Fact label={t.closed.closedAt}>{`${formatDayAndTime(item.resolvedAt)} · ${t.closed.took(formatAge(item.secondsToResolve))}`}</Fact>
          <Fact label={t.closed.closedBy}>{item.resolvedByName ?? t.closed.unnamedController}</Fact>
        </dl>
      </article>
    </li>
  )
}

/** The latest closed exceptions as cards, newest closure first (spec 08 section 14). Read only: nothing here can be changed. */
export function ClosedExceptionsTab() {
  const query = useClosedExceptions()
  const data = query.data

  return (
    <div className="flex flex-col gap-[10px]">
      {query.dataUpdatedAt > 0 ? (
        <span className="self-end text-label text-muted tabular-nums">
          {t.freshness.updated} {formatClock(query.dataUpdatedAt)}
        </span>
      ) : null}

      <Panel title={t.closed.title}>
        {data === undefined ? (
          <p className="m-0 text-label text-muted">{query.isError ? t.freshness.neverLoaded : t.loading}</p>
        ) : data.items.length === 0 ? (
          <p className="m-0 text-[15px]">{t.closed.none}</p>
        ) : (
          <>
            {data.total > data.items.length ? (
              <p className="m-0 text-label text-muted">{t.closed.showing(data.items.length, data.total)}</p>
            ) : null}
            <ul className="m-0 flex flex-col gap-[10px] p-0">
              {data.items.map((item) => (
                <ClosedCard key={item.id} item={item} />
              ))}
            </ul>
          </>
        )}
      </Panel>
    </div>
  )
}
