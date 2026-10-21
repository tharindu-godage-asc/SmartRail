import { useEffect, useRef, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ApiError } from '../../core/api/client'
import type { DetailStop, PlatformWarning } from '../../core/api/types'
import { Icon } from '../../core/Icon'
import { t } from '../../core/strings'
import { Button } from '../../core/ui'
import { useApi } from '../auth/apiContext'
import { inputClass } from '../timetable/wizard/styles'
import { useTimetableOptions } from '../timetable/queries'

const p = t.journeyDetail.platform

function usePlatformChange(journeyId: string, serviceStopId: string) {
  const { api } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: { platformCode: string | null; acknowledgeWarnings: boolean }) => api.post(`/api/v1/journeys/${journeyId}/stops/${serviceStopId}/platform`, body),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['journeyDetail', journeyId] }),
  })
}

/**
 * Set the platform of one stop (spec 20, FR-57): one of the station's platforms, or "planned" to go back to the timetable's platform.
 * Another train on that platform within a few minutes is advice that has to be confirmed, never a block; trains in opposite directions
 * ask for a second look at the signalling and control channels. It records a decision: it does not control any train.
 */
export function PlatformDialog({ journeyId, stop, onClose, onDone }: { journeyId: string; stop: DetailStop; onClose: () => void; onDone: (message: string) => void }) {
  const dialog = useRef<HTMLDialogElement>(null)
  const options = useTimetableOptions()
  const change = usePlatformChange(journeyId, stop.serviceStopId)
  const [choice, setChoice] = useState(stop.platform ?? '')
  const [confirmed, setConfirmed] = useState(false)
  const platforms = options.data?.stations.find((s) => s.code === stop.stationCode)?.platforms ?? []

  useEffect(() => {
    const element = dialog.current
    if (element && !element.open) element.showModal()
  }, [])

  const warnings: PlatformWarning[] = change.error instanceof ApiError && change.error.reason === 'warnings_not_acknowledged' ? ((change.error.body as { warnings?: PlatformWarning[] }).warnings ?? []) : []
  const opposite = warnings.some((w) => w.oppositeDirection)
  const failure =
    change.error instanceof ApiError && warnings.length === 0
      ? (p.reasons[change.error.reason ?? ''] ?? p.failed)
      : change.isError && warnings.length === 0
        ? p.failed
        : null

  const send = (acknowledge: boolean) => {
    if (change.isPending) return
    change.mutate(
      { platformCode: choice === '' ? null : choice, acknowledgeWarnings: acknowledge },
      { onSuccess: () => onDone(choice === '' ? p.doneReset(stop.stationName) : p.done(stop.stationName, choice)) },
    )
  }

  return (
    <dialog
      ref={dialog}
      aria-labelledby="platform-title"
      onCancel={(e) => {
        e.preventDefault()
        if (!change.isPending) onClose()
      }}
      className="m-auto w-[min(540px,calc(100vw-32px))] rounded-lg border border-line bg-page p-lg text-ink backdrop:bg-black/40"
    >
      <h2 id="platform-title" className="m-0 text-[20px]">
        {p.title(stop.stationName)}
      </h2>
      <p className="mb-md mt-sm text-[15px] leading-normal">{p.help(stop.plannedPlatform ?? p.none)}</p>

      <label className="mb-md flex flex-col gap-xs text-label font-bold">
        {p.label}
        <select className={`${inputClass} max-w-[260px]`} value={choice} onChange={(e) => { setChoice(e.target.value); setConfirmed(false) }}>
          <option value="">{p.planned(stop.plannedPlatform ?? p.none)}</option>
          {platforms.map((code) => (
            <option key={code} value={code}>
              {`${t.timetable.wizard.platform} ${code}`}
            </option>
          ))}
        </select>
      </label>

      {warnings.length > 0 ? (
        <div role="alert" className="mb-md flex flex-col gap-sm rounded-md border-[3px] border-warning bg-warning-bg p-md text-warning">
          <p className="m-0 flex items-center gap-sm text-[16px] font-bold">
            <Icon name="warning" size={22} />
            {opposite ? p.adviceHigh : p.advice}
          </p>
          <ul className="m-0 list-disc pl-[20px] text-label">
            {warnings.map((w) => (
              <li key={w.trainNumber}>{p.warningLine(w.trainNumber, w.minutesApart, w.oppositeDirection)}</li>
            ))}
          </ul>
          {opposite ? <p className="m-0 text-label font-semibold">{p.checkChannels}</p> : null}
          <label className="flex min-h-touch items-center gap-sm text-[15px] font-semibold">
            <input type="checkbox" className="size-[22px]" checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} />
            {opposite ? p.confirmHigh : p.confirm}
          </label>
        </div>
      ) : null}

      {failure ? (
        <p role="alert" className="mb-md mt-0 rounded-md border-[3px] border-warning bg-warning-bg p-[12px] text-label font-semibold text-warning">
          {failure}
        </p>
      ) : null}

      <p className="mb-md mt-0 text-[13px] leading-snug text-muted">{p.records}</p>
      <div className="flex justify-end gap-[10px]">
        <Button onClick={onClose} disabled={change.isPending}>
          {p.cancel}
        </Button>
        <Button variant="primary" onClick={() => send(warnings.length > 0)} disabled={change.isPending || (warnings.length > 0 && !confirmed)}>
          {change.isPending ? p.saving : warnings.length > 0 ? p.changeAnyway : p.save}
        </Button>
      </div>
    </dialog>
  )
}
