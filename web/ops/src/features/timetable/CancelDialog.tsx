import { useEffect, useRef, useState } from 'react'
import { ApiError } from '../../core/api/client'
import type { ServiceDetail } from '../../core/api/types'
import { Icon } from '../../core/Icon'
import { t, timetableReason } from '../../core/strings'
import { todayInColombo } from '../../core/time'
import { Button } from '../../core/ui'
import { formatDate } from './dates'
import { useCancelService } from './queries'
import { inputClass } from './wizard/styles'

const maxReason = 200

/**
 * Cancel a service (spec 15 W3, `Dash-CancelService`): this date or from a date onward, with a reason. A native modal dialog like the
 * resolve dialog: focus moves in, Esc closes it, the page behind cannot be used, and the confirm button is off while the call is out, so
 * a double click sends one request. A refused cancel (a journey has reports) is shown in the dialog and changes nothing.
 */
export function CancelDialog({ detail, onClose, onDone }: { detail: ServiceDetail; onClose: () => void; onDone: (message: string) => void }) {
  const c = t.timetable.cancel
  const dialog = useRef<HTMLDialogElement>(null)
  const cancel = useCancelService(detail.serviceId)
  const [mode, setMode] = useState<'date' | 'onward'>('date')
  const [date, setDate] = useState(todayInColombo())
  const [reason, setReason] = useState('')
  const [already, setAlready] = useState(false)
  const trimmed = reason.trim()
  const canSend = trimmed.length >= 1 && trimmed.length <= maxReason && date !== '' && !cancel.isPending

  useEffect(() => {
    const element = dialog.current
    if (element && !element.open) {
      element.showModal()
    }
  }, [])

  const confirm = () => {
    if (!canSend) return
    cancel.mutate(
      { from: date, to: mode === 'date' ? date : null, reason: trimmed },
      {
        onSuccess: (result) => {
          if (result.cancelledJourneys === 0 && cancelledBefore(detail, date, mode)) {
            setAlready(true)
          } else {
            onDone(result.cancelledJourneys === 0 ? c.doneNone : c.done(result.cancelledJourneys))
          }
        },
      },
    )
  }

  const error = cancel.error
  const blockedOn =
    error instanceof ApiError && error.reason === 'journey_has_events' && typeof (error.body as { date?: unknown } | undefined)?.date === 'string'
      ? ((error.body as { date: string }).date)
      : null
  const failure =
    error instanceof ApiError && blockedOn === null
      ? (timetableReason(error.reason) ?? c.failed)
      : error && blockedOn === null
        ? c.failed
        : null

  return (
    <dialog
      ref={dialog}
      aria-labelledby="cancel-title"
      onCancel={(event) => {
        event.preventDefault()
        if (!cancel.isPending) onClose()
      }}
      className="m-auto w-[min(560px,calc(100vw-32px))] rounded-lg border border-line bg-page p-lg text-ink backdrop:bg-black/40"
    >
      <h2 id="cancel-title" className="m-0 text-[20px]">
        {c.title(detail.trainNumber)}
      </h2>
      <p className="mb-md mt-sm text-[15px] leading-normal">{c.help}</p>
      <p className="mb-md mt-0 flex items-start gap-sm text-label text-muted">
        <Icon name="info" size={18} />
        {t.timetable.actions.notSentYet}
      </p>

      {blockedOn !== null ? (
        <div role="alert" className="mb-md flex flex-col gap-sm rounded-md border-[3px] border-warning bg-warning-bg p-md text-warning">
          <p className="m-0 flex items-center gap-sm text-[16px] font-bold">
            <Icon name="warning" size={22} />
            {c.refused(formatDate(blockedOn))}
          </p>
          <p className="m-0 text-label">{c.refusedHelp}</p>
        </div>
      ) : already ? (
        <p role="status" className="mb-md mt-0 rounded-md border-[1.5px] border-ink p-[12px] text-[15px] font-semibold">
          {c.already}
        </p>
      ) : (
        <div className="mb-md flex flex-col gap-md">
          <fieldset className="m-0 flex flex-col gap-xs border-0 p-0">
            <legend className="sr-only">{t.timetable.actions.cancelService}</legend>
            {(['date', 'onward'] as const).map((value) => (
              <label key={value} className="flex min-h-touch items-center gap-sm text-[15px]">
                <input type="radio" name="cancel-mode" checked={mode === value} onChange={() => setMode(value)} />
                {value === 'date' ? c.modeDate : c.modeOnward}
              </label>
            ))}
          </fieldset>
          <div className="flex flex-col gap-xs">
            <label htmlFor="cancel-date" className="text-label font-bold">
              {c.date}
            </label>
            <input id="cancel-date" type="date" className={`${inputClass} max-w-[220px]`} value={date} min={todayInColombo()} onChange={(e) => setDate(e.target.value)} />
          </div>
          <div className="flex flex-col gap-xs">
            <label htmlFor="cancel-reason" className="text-label font-bold">
              {c.reason}
            </label>
            <textarea
              id="cancel-reason"
              rows={3}
              maxLength={maxReason}
              aria-describedby="cancel-reason-hint"
              className="rounded-md border-[1.5px] border-ink bg-page p-sm text-[15px] text-ink"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
            <span id="cancel-reason-hint" className="flex justify-between text-caption text-muted">
              <span>{c.reasonHint}</span>
              <span className="tabular-nums">{c.counter(reason.length)}</span>
            </span>
          </div>
        </div>
      )}

      {failure ? (
        <p role="alert" className="mb-md mt-0 rounded-md border-[3px] border-warning bg-warning-bg p-[12px] text-label font-semibold text-warning">
          {failure}
        </p>
      ) : null}

      <div className="flex flex-wrap justify-end gap-[10px]">
        {blockedOn !== null ? (
          <>
            <Button onClick={onClose}>{c.close}</Button>
            <Button variant="primary" onClick={() => cancel.reset()}>
              {c.tryOther}
            </Button>
          </>
        ) : already ? (
          <Button variant="primary" onClick={onClose}>
            {c.close}
          </Button>
        ) : (
          <>
            <Button onClick={onClose} disabled={cancel.isPending}>
              {c.keep}
            </Button>
            <Button variant="primary" onClick={confirm} disabled={!canSend}>
              {cancel.isPending ? c.sending : c.confirm}
            </Button>
          </>
        )}
      </div>
    </dialog>
  )
}

/** True when the same range is already in the service's cancellations, so a 200 with 0 journeys means "already cancelled". */
function cancelledBefore(detail: ServiceDetail, date: string, mode: 'date' | 'onward'): boolean {
  return detail.cancellations.some((x) => x.from === date && (mode === 'onward' ? x.to === null : x.to === date))
}
