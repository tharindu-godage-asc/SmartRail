import { useState } from 'react'
import type { ResolveAction } from '../../core/api/types'
import { Icon } from '../../core/Icon'
import { exceptionTypeLabel, t } from '../../core/strings'
import { formatAge, formatClock, formatClockShort } from '../../core/time'
import { Button, cell, Panel, SeverityBadge, Th } from '../../core/ui'
import { useNow } from '../../core/useNow'
import { liveAge, useExceptions, whereOf } from './queries'
import { BulkVerifyBar } from './BulkVerifyBar'
import { ResolveDialog } from './ResolveDialog'
import { VerifyPanel } from './VerifyPanel'

const actions: ResolveAction[] = ['Acknowledged', 'NoActionNeeded', 'Escalated']

export function OpenExceptionsTab() {
  const query = useExceptions()
  const now = useNow()
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [pending, setPending] = useState<ResolveAction | null>(null)
  const [selecting, setSelecting] = useState(false)
  const [ticked, setTicked] = useState<string[]>([])

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
        {items?.some((i) => i.type === 'ReasonAwaitingVerification') ? (
          <BulkVerifyBar
            selecting={selecting}
            ticked={ticked}
            onToggle={() => {
              setSelecting(!selecting)
              setTicked([])
            }}
            onDone={() => setTicked([])}
          />
        ) : null}
        {items === undefined ? (
          <p className="m-0 text-label text-muted">{query.isError ? t.freshness.neverLoaded : t.loading}</p>
        ) : items.length === 0 ? (
          <p className="m-0 text-[15px]">{t.exceptions.none}</p>
        ) : (
          <div className="max-h-[min(60vh,520px)] overflow-auto">
            <table className="w-full border-collapse">
              <thead>
                <tr>
                  {selecting ? <Th>{t.verify.selectMany}</Th> : null}
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
                      {selecting ? (
                        <td className={cell}>
                          {item.type !== 'ReasonAwaitingVerification' ? null : item.canVerify ? (
                            <input
                              type="checkbox"
                              className="size-[22px]"
                              aria-label={t.verify.selectOne(item.trainNumber)}
                              checked={ticked.includes(item.id)}
                              onChange={(e) => setTicked(e.target.checked ? [...ticked, item.id] : ticked.filter((x) => x !== item.id))}
                            />
                          ) : (
                            <span className="inline-flex items-center gap-xs text-label text-muted">
                              <Icon name="warning" size={16} />
                              {t.verify.lockedRow}
                            </span>
                          )}
                        </td>
                      ) : null}
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
            {selected.type === 'ReasonAwaitingVerification' ? <VerifyPanel key={selected.id} item={selected} /> : null}
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
