import { useState } from 'react'
import { ApiError } from '../../core/api/client'
import type { ExceptionItem } from '../../core/api/types'
import { Icon } from '../../core/Icon'
import { t } from '../../core/strings'
import { Button } from '../../core/ui'
import { useVerify } from './useVerify'

const approved = ['SignalIssue', 'TechnicalFault', 'Obstruction', 'Weather']

function problemOf(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 409) return t.verify.alreadyResolved
    if (error.reason === 'reason_not_approved') return t.verify.notApproved
  }
  return t.verify.failed
}

/**
 * Verify the delay reason of one exception (spec 16 F2, FR-52): publish it as reported, publish another approved reason, or reject
 * the report. Verifying is what makes the reason public, so the preview says what passengers will see.
 */
export function VerifyPanel({ item }: { item: ExceptionItem }) {
  const { one } = useVerify()
  const [editing, setEditing] = useState(false)
  const [choice, setChoice] = useState('')
  const reported = item.reportedReason ?? ''
  const label = (code: string) => t.verify.reasons[code] ?? code

  return (
    <div className="flex flex-col gap-sm">
      <h3 className="m-0 text-[15px] font-bold">{t.verify.heading}</h3>
      <p className="m-0 text-[15px]">{t.verify.reported(label(reported))}</p>
      {item.canVerify ? (
        <p className="m-0 flex items-start gap-sm text-label text-muted">
          <Icon name="info" size={18} />
          {t.verify.preview(label(reported))}
        </p>
      ) : (
        <p className="m-0 flex items-start gap-sm text-label font-semibold">
          <Icon name="warning" size={18} />
          {t.verify.locked}
        </p>
      )}

      {editing ? (
        <div className="flex flex-wrap items-center gap-sm">
          <label className="flex items-center gap-sm text-[15px]">
            {t.verify.editLabel}
            <select value={choice} onChange={(e) => setChoice(e.target.value)} className="min-h-touch rounded-md border border-line bg-page px-sm text-[15px]">
              <option value="">{t.verify.choose}</option>
              {approved.map((code) => (
                <option key={code} value={code}>
                  {label(code)}
                </option>
              ))}
            </select>
          </label>
          <Button variant="primary" disabled={choice === '' || one.isPending} onClick={() => one.mutate({ id: item.id, action: 'change', reasonCode: choice })}>
            {one.isPending ? t.verify.sending : t.verify.publish}
          </Button>
        </div>
      ) : (
        <div className="flex flex-wrap gap-[10px]">
          <Button variant="primary" disabled={!item.canVerify || one.isPending} onClick={() => one.mutate({ id: item.id, action: 'verify' })}>
            {one.isPending ? t.verify.sending : t.verify.verify}
          </Button>
          <Button onClick={() => setEditing(true)} disabled={one.isPending}>
            {t.verify.edit}
          </Button>
          <Button onClick={() => one.mutate({ id: item.id, action: 'reject' })} disabled={one.isPending}>
            {t.verify.reject}
          </Button>
        </div>
      )}
      <p className="m-0 text-[13px] leading-snug text-muted">{t.verify.rejectNote}</p>
      {one.isError ? (
        <p role="alert" className="m-0 rounded-md border-[3px] border-warning bg-warning-bg p-[12px] text-label font-semibold text-warning">
          {problemOf(one.error)}
        </p>
      ) : null}
    </div>
  )
}
