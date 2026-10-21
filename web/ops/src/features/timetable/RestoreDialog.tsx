import { useEffect, useRef } from 'react'
import { ApiError } from '../../core/api/client'
import type { CancellationView } from '../../core/api/types'
import { t } from '../../core/strings'
import { Button } from '../../core/ui'
import { formatRange } from './dates'
import { useRestoreService } from './queries'

const r = t.timetable.restore

/**
 * Restore a cancelled service (the other half of spec 15 W3): the same modal pattern as Cancel. It says what comes back, that the
 * cancellation stays in the history, and that passengers are not messaged. The confirm button is off while the call is out.
 */
export function RestoreDialog({ row, onClose, onDone }: { row: CancellationView; onClose: () => void; onDone: (message: string) => void }) {
  const dialog = useRef<HTMLDialogElement>(null)
  const restore = useRestoreService(row.cancellationId)

  useEffect(() => {
    const element = dialog.current
    if (element && !element.open) element.showModal()
  }, [])

  const confirm = () => {
    if (restore.isPending) return
    restore.mutate(undefined, { onSuccess: (result) => onDone(r.done(row.trainNumber, result.restoredJourneys)) })
  }

  const failure = restore.error
    ? restore.error instanceof ApiError && restore.error.status === 409
      ? r.already
      : restore.error instanceof ApiError && restore.error.status === 404
        ? r.notFound
        : r.failed
    : null

  return (
    <dialog
      ref={dialog}
      aria-labelledby="restore-title"
      onCancel={(e) => {
        e.preventDefault()
        if (!restore.isPending) onClose()
      }}
      className="m-auto w-[min(520px,calc(100vw-32px))] rounded-lg border border-line bg-page p-lg text-ink backdrop:bg-black/40"
    >
      <h2 id="restore-title" className="m-0 text-[20px]">
        {r.title(row.trainNumber)}
      </h2>
      <p className="mb-sm mt-sm text-[15px] leading-normal">{r.help(formatRange(row.from, row.to))}</p>
      <p className="mb-md mt-0 text-label text-muted">{r.keeps}</p>
      <p className="mb-md mt-0 text-label text-muted">{t.timetable.actions.notSentYet}</p>

      {failure ? (
        <p role="alert" className="mb-md mt-0 rounded-md border-[3px] border-warning bg-warning-bg p-[12px] text-label font-semibold text-warning">
          {failure}
        </p>
      ) : null}

      <div className="flex flex-wrap justify-end gap-[10px]">
        <Button onClick={onClose} disabled={restore.isPending}>
          {failure ? r.close : r.keep}
        </Button>
        {failure ? null : (
          <Button variant="primary" onClick={confirm} disabled={restore.isPending}>
            {restore.isPending ? r.restoring : r.confirm}
          </Button>
        )}
      </div>
    </dialog>
  )
}
