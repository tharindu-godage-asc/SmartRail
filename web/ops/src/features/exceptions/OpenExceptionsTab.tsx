import { useState } from 'react'
import type { ResolveAction } from '../../core/api/types'
import { exceptionTypeLabel, t } from '../../core/strings'
import { formatAge, formatClock, formatClockShort } from '../../core/time'
import { Button, cell, Panel, SeverityBadge, Th } from '../../core/ui'
import { useNow } from '../../core/useNow'
import { liveAge, useExceptions, whereOf } from './queries'
import { ResolveDialog } from './ResolveDialog'

const actions: ResolveAction[] = ['Acknowledged', 'NoActionNeeded', 'Escalated']

export function OpenExceptionsTab() {
  const query = useExceptions()
  const now = useNow()
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [pending, setPending] = useState<ResolveAction | null>(null)

  const items = query.data?.items
  // A selection that is no longer in the list (resolved here or by someone else) simply goes away.
  const selected = items?.find((item) => item.id === selectedId) ?? null

  return (
    <div className="flex flex-col gap-[14px]">
      {query.dataUpdatedAt > 0 ? (
        <span className="self-end text-label text-muted tabular-nums">
          {t.freshness.updated} {formatClock(query.dataUpdatedAt)}
        </span>
      ) : null}

      <Panel title={t.exceptions.open}>
        {items === undefined ? (
          <p className="m-0 text-label text-muted">{query.isError ? t.freshness.neverLoaded : t.loading}</p>
        ) : items.length === 0 ? (
          <p className="m-0 text-[15px]">{t.exceptions.none}</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full border-collapse">
              <thead>
                <tr>
                  <Th>{t.exceptions.columns.severity}</Th>
                  <Th>{t.exceptions.columns.exception}</Th>
                  <Th>{t.exceptions.columns.where}</Th>
                  <Th>{t.exceptions.columns.raised}</Th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => {
                  const isSelected = item.id === selected?.id
                  return (
                    <tr key={item.id} data-exception-id={item.id} className={isSelected ? 'bg-surface outline-[3px] -outline-offset-[3px] outline-ink' : ''}>
                      <td className={cell}>
                        <SeverityBadge severity={item.severity} />
                      </td>
                      <td className={`${cell} font-bold`}>
                        <button
                          type="button"
                          aria-pressed={isSelected}
                          aria-label={t.exceptions.selectRow(item.trainNumber, exceptionTypeLabel(item.type))}
                          onClick={() => setSelectedId(isSelected ? null : item.id)}
                          className="min-h-touch cursor-pointer border-0 bg-transparent p-0 text-left text-label font-bold text-ink underline decoration-line underline-offset-4"
                        >
                          {exceptionTypeLabel(item.type)}
                        </button>
                      </td>
                      <td className={cell}>{whereOf(item)}</td>
                      <td className={cell}>
                        {formatClockShort(item.createdAt)} · {formatAge(liveAge(item, query.dataUpdatedAt, now))}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </Panel>

      <Panel title={selected ? `${exceptionTypeLabel(selected.type)} · ${t.exceptions.detail}` : t.exceptions.detail}>
        {selected === null ? (
          <p className="m-0 text-label text-muted">{t.exceptions.pick}</p>
        ) : (
          <>
            <p className="m-0 max-w-[900px] text-[15px] leading-normal">
              {(t.exceptions.descriptions[selected.type] ?? (() => selected.type))(selected.trainNumber, selected.stationName)}
            </p>
            <div role="group" aria-label={t.exceptions.resolveWith} className="flex flex-wrap gap-[10px]">
              {actions.map((action) => (
                <Button key={action} variant={action === 'Acknowledged' ? 'primary' : 'secondary'} onClick={() => setPending(action)}>
                  {t.actions[action]}
                </Button>
              ))}
            </div>
            <p className="m-0 text-[13px] leading-snug text-muted">{t.exceptions.advice}</p>
          </>
        )}
      </Panel>

      {selected !== null && pending !== null ? (
        <ResolveDialog
          key={`${selected.id}-${pending}`}
          item={selected}
          action={pending}
          onClose={() => {
            setPending(null)
          }}
        />
      ) : null}
    </div>
  )
}
