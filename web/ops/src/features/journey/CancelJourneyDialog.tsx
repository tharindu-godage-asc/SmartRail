import { useEffect, useRef, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ApiError } from '../../core/api/client'
import { Icon } from '../../core/Icon'
import { t, timetableReason } from '../../core/strings'
import { Button } from '../../core/ui'
import { useApi } from '../auth/apiContext'

const c = t.journeyDetail.cancelJourney
const maxReason = 200

function useCancelJourney(journeyId: string) {
  const { api } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (reason: string) => api.post(`/api/v1/journeys/${journeyId}/cancel`, { reason }),
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['journeyDetail', journeyId] })
      void queryClient.invalidateQueries({ queryKey: ['assignments'] })
      void queryClient.invalidateQueries({ queryKey: ['timetable'] })
      void queryClient.invalidateQueries({ queryKey: ['overview'] })
    },
  })
}

/**
 * Cancel one journey, that is just this day's run of the train (same rules and reason as Cancel service). A native modal dialog like the
 * platform and cancel-service dialogs: focus moves in, Esc closes it, and the confirm button is off while the call is out. A journey that
 * already has reports is refused by the server and the dialog says so; nothing changes.
 */
export function CancelJourneyDialog({
  journeyId,
  trainNumber,
  onClose,
  onDone,
}: {
  journeyId: string
  trainNumber: string
  onClose: () => void
  onDone: (message: string) => void
}) {
  const dialog = useRef<HTMLDialogElement>(null)
  const cancel = useCancelJourney(journeyId)
  const [reason, setReason] = useState('')
  const trimmed = reason.trim()
  const canSend = trimmed.length >= 1 && trimmed.length <= maxReason && !cancel.isPending

  useEffect(() => {
    const element = dialog.current
    if (element && !element.open) {
      element.showModal()
    }
  }, [])

  const error = cancel.error
  const refused = error instanceof ApiError && error.reason === 'journey_has_events'
  const failure = error && !refused ? ((error instanceof ApiError ? timetableReason(error.reason) : undefined) ?? c.failed) : null

  return (
    <dialog
      ref={dialog}
      aria-labelledby="cancel-journey-title"
      onCancel={(event) => {
        event.preventDefault()
        if (!cancel.isPending) onClose()
      }}
      className="m-auto w-[min(560px,calc(100vw-32px))] rounded-lg border border-line bg-page p-lg text-ink backdrop:bg-black/40"
    >
      <h2 id="cancel-journey-title" className="m-0 text-[20px]">
        {c.title(trainNumber)}
      </h2>
      <p className="mb-md mt-sm text-[15px] leading-normal">{c.help}</p>
      <p className="mb-md mt-0 flex items-start gap-sm text-label text-muted">
        <Icon name="info" size={18} />
        {t.timetable.actions.notSentYet}
      </p>

      {refused ? (
        <div role="alert" className="mb-md flex flex-col gap-sm rounded-md border-[3px] border-warning bg-warning-bg p-md text-warning">
          <p className="m-0 flex items-center gap-sm text-[16px] font-bold">
            <Icon name="warning" size={22} />
            {c.refused}
          </p>
          <p className="m-0 text-label">{c.refusedHelp}</p>
        </div>
      ) : (
        <div className="mb-md flex flex-col gap-xs">
          <label htmlFor="cancel-journey-reason" className="text-label font-bold">
            {c.reason}
          </label>
          <textarea
            id="cancel-journey-reason"
            rows={3}
            maxLength={maxReason}
            aria-describedby="cancel-journey-reason-hint"
            className="rounded-md border-[1.5px] border-ink bg-page p-sm text-[15px] text-ink"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
          />
          <span id="cancel-journey-reason-hint" className="flex justify-between text-caption text-muted">
            <span>{c.reasonHint}</span>
            <span className="tabular-nums">{c.counter(reason.length)}</span>
          </span>
        </div>
      )}

      {failure ? (
        <p role="alert" className="mb-md mt-0 rounded-md border-[3px] border-warning bg-warning-bg p-[12px] text-label font-semibold text-warning">
          {failure}
        </p>
      ) : null}

      <div className="flex flex-wrap justify-end gap-[10px]">
        {refused ? (
          <Button variant="primary" onClick={onClose}>
            {c.close}
          </Button>
        ) : (
          <>
            <Button onClick={onClose} disabled={cancel.isPending}>
              {c.keep}
            </Button>
            <Button
              variant="primary"
              onClick={() => {
                if (canSend) cancel.mutate(trimmed, { onSuccess: () => onDone(c.done) })
              }}
              disabled={!canSend}
            >
              {cancel.isPending ? c.sending : c.confirm}
            </Button>
          </>
        )}
      </div>
    </dialog>
  )
}
