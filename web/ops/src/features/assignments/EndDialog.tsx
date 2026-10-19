import { useEffect, useRef, useState } from 'react'
import { ApiError } from '../../core/api/client'
import type { AssignmentView, EndReason } from '../../core/api/types'
import { Icon } from '../../core/Icon'
import { t } from '../../core/strings'
import { Button } from '../../core/ui'
import { useEndAssignment } from './queries'

const maxNote = 500
const reasons: EndReason[] = ['LostDevice', 'CrewChange', 'Other']

/**
 * End an assignment now (spec 17 A2, `Dash-EndAssignment`): a reason, an optional note, one deliberate confirm. A native modal dialog like
 * the resolve dialog. The words say what will happen: for a lost device, reports are refused even if they claim an earlier time.
 */
export function EndDialog({
  journeyId,
  assignment,
  onClose,
  onDone,
}: {
  journeyId: string
  assignment: AssignmentView
  onClose: () => void
  onDone: () => void
}) {
  const c = t.assignments.crewPage
  const dialog = useRef<HTMLDialogElement>(null)
  const end = useEndAssignment(journeyId)
  const [reason, setReason] = useState<EndReason>('LostDevice')
  const [note, setNote] = useState('')

  useEffect(() => {
    const element = dialog.current
    if (element && !element.open) {
      element.showModal()
    }
  }, [])

  const confirm = () => {
    if (end.isPending) return
    end.mutate({ assignmentId: assignment.assignmentId, reason, note: note.trim() || undefined }, { onSuccess: onDone })
  }

  const failure = !end.isError
    ? null
    : end.error instanceof ApiError && end.error.reason === 'already_ended'
      ? c.alreadyEnded
      : end.error instanceof ApiError && end.error.reason === 'not_started'
        ? c.notStarted
        : c.failed

  return (
    <dialog
      ref={dialog}
      aria-labelledby="end-title"
      onCancel={(event) => {
        event.preventDefault()
        if (!end.isPending) onClose()
      }}
      className="m-auto w-[min(560px,calc(100vw-32px))] rounded-lg border border-line bg-page p-lg text-ink backdrop:bg-black/40"
    >
      <h2 id="end-title" className="m-0 text-[20px]">
        {c.endTitle(assignment.operatorName)}
      </h2>

      <fieldset className="mb-md mt-md flex flex-col gap-xs border-0 p-0">
        <legend className="mb-xs p-0 text-label font-bold">{c.reasonLabel}</legend>
        {reasons.map((value) => (
          <label key={value} className="flex min-h-touch items-center gap-sm text-[15px]">
            <input type="radio" name="end-reason" checked={reason === value} onChange={() => setReason(value)} />
            {c.reasons[value]}
          </label>
        ))}
      </fieldset>

      <p className="mb-md mt-0 flex items-start gap-sm text-label">
        <Icon name="info" size={18} />
        {reason === 'LostDevice' ? c.lostHelp : c.otherHelp}
      </p>

      <div className="mb-md flex flex-col gap-xs">
        <label htmlFor="end-note" className="text-label font-bold">
          {c.note}
        </label>
        <textarea
          id="end-note"
          rows={3}
          maxLength={maxNote}
          className="rounded-md border-[1.5px] border-ink bg-page p-sm text-[15px] text-ink"
          value={note}
          onChange={(e) => setNote(e.target.value)}
        />
        <span className="self-end text-caption tabular-nums text-muted">{c.noteCounter(note.length)}</span>
      </div>

      <p className="mb-md mt-0 text-label text-muted">{c.afterwards}</p>

      {failure ? (
        <p role="alert" className="mb-md mt-0 rounded-md border-[3px] border-warning bg-warning-bg p-[12px] text-label font-semibold text-warning">
          {failure}
        </p>
      ) : null}

      <div className="flex flex-wrap justify-end gap-[10px]">
        <Button onClick={onClose} disabled={end.isPending}>
          {c.keep}
        </Button>
        <Button variant="primary" onClick={confirm} disabled={end.isPending}>
          {end.isPending ? c.ending : c.confirm}
        </Button>
      </div>
    </dialog>
  )
}
