import { useEffect, useRef } from 'react'
import { ApiError } from '../../core/api/client'
import type { ExceptionItem, ResolveAction } from '../../core/api/types'
import { t } from '../../core/strings'
import { Button } from '../../core/ui'
import { useResolve } from './useResolve'

function failureText(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 409) return t.resolveDialog.alreadyResolved
    if (error.status === 404) return t.resolveDialog.notFound
  }

  return t.resolveDialog.failed
}

/**
 * The one deliberate confirm before an exception is resolved (spec 08 rule 9), in the pattern of the canvas's override dialog.
 * A native modal dialog: focus moves in, Esc closes it, the page behind cannot be used. The confirm button is disabled while the
 * request is out, so a double click sends one request.
 */
export function ResolveDialog({
  item,
  action,
  onClose,
}: {
  item: ExceptionItem
  action: ResolveAction
  /** Called when the dialog closes, whether cancelled, resolved, or after the problem was shown and dismissed. */
  onClose: () => void
}) {
  const dialog = useRef<HTMLDialogElement>(null)
  const resolve = useResolve()

  useEffect(() => {
    const element = dialog.current
    if (element && !element.open) {
      element.showModal()
    }
  }, [])

  const confirm = () => {
    if (resolve.isPending) {
      return
    }

    resolve.mutate({ id: item.id, action }, { onSuccess: onClose })
  }

  return (
    <dialog
      ref={dialog}
      aria-labelledby="resolve-title"
      onCancel={(event) => {
        event.preventDefault()
        if (!resolve.isPending) onClose()
      }}
      className="m-auto w-[min(520px,calc(100vw-32px))] rounded-lg border border-line bg-page p-lg text-ink backdrop:bg-black/40"
    >
      <h2 id="resolve-title" className="m-0 text-[20px]">
        {t.resolveDialog.title(item.trainNumber)}
      </h2>
      <p className="mb-md mt-sm text-[15px] leading-normal">{t.resolveDialog.body(t.actionsPast[action] ?? action)}</p>
      {resolve.isError ? (
        <p role="alert" className="mb-md mt-0 rounded-md border-[3px] border-warning bg-warning-bg p-[12px] text-label font-semibold text-warning">
          {failureText(resolve.error)}
        </p>
      ) : null}
      <div className="flex justify-end gap-[10px]">
        <Button onClick={onClose} disabled={resolve.isPending}>
          {t.resolveDialog.cancel}
        </Button>
        {resolve.isError ? null : (
          <Button variant="primary" onClick={confirm} disabled={resolve.isPending}>
            {resolve.isPending ? t.resolveDialog.sending : t.resolveDialog.confirm}
          </Button>
        )}
      </div>
    </dialog>
  )
}
