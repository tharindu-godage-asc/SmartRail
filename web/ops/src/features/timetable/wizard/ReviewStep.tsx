import { forwardRef } from 'react'
import type { LineOptions, ValidationIssue, ValidationResult } from '../../../core/api/types'
import { Icon } from '../../../core/Icon'
import { t } from '../../../core/strings'
import { Button, Panel } from '../../../core/ui'
import { formatDate } from '../dates'
import { dayCodes, includedStops, type Draft } from './draft'
import { placeOf } from './issues'

/** Where an issue belongs, in words: the station of a stop, or nothing for the service as a whole. */
function whereOf(issue: ValidationIssue, draft: Draft, options: LineOptions): string {
  const place = placeOf(issue, draft)
  if (place.where !== 'stop') return ''
  return options.stations.find((s) => s.code === place.stationCode)?.names.en ?? place.stationCode
}

function Issues({ issues, draft, options, advice }: { issues: ValidationIssue[]; draft: Draft; options: LineOptions; advice: boolean }) {
  const w = t.timetable.wizard
  return (
    <ul className="m-0 flex list-none flex-col gap-sm p-0">
      {issues.map((issue, i) => {
        const where = whereOf(issue, draft, options)
        return (
          <li key={`${issue.code}-${i}`} className="flex items-start gap-sm text-[15px]">
            <Icon name={advice ? 'info' : 'warning'} size={20} />
            <span>
              <span className="font-bold">{advice ? w.advice : w.error}: </span>
              {where ? <span className="font-semibold">{where}. </span> : null}
              {issue.message || t.timetable.issues[issue.code] || issue.code}
            </span>
          </li>
        )
      })}
    </ul>
  )
}

/**
 * Step 3: what the server found, a summary, and the Schedule button (spec 15 rules 1 to 3). Errors switch Schedule off. Advice needs the
 * box ticked first and then never blocks (FR-57). The screen shows the server's words and decides nothing itself.
 */
export const ReviewStep = forwardRef<
  HTMLDivElement,
  {
    draft: Draft
    options: LineOptions
    checking: boolean
    checkFailed: boolean
    result: ValidationResult | undefined
    confirmed: boolean
    onConfirm: (value: boolean) => void
    scheduling: boolean
    scheduleError: string | null
    onRetryCheck: () => void
    onSchedule: () => void
  }
>(function ReviewStep(props, summaryRef) {
  const { draft, options, checking, checkFailed, result, confirmed, onConfirm, scheduling, scheduleError, onRetryCheck, onSchedule } = props
  const w = t.timetable.wizard
  const errors = result?.errors ?? []
  const warnings = result?.warnings ?? []
  const canSchedule = result !== undefined && errors.length === 0 && (warnings.length === 0 || confirmed) && !scheduling && !checking
  const stops = includedStops(draft)
  const nameOf = (code: string) => options.stations.find((s) => s.code === code)?.names.en ?? code

  return (
    <div className="flex flex-col gap-md">
      <div ref={summaryRef} tabIndex={-1} className="flex flex-col gap-sm outline-none">
        {checking ? (
          <p role="status" className="m-0 text-[15px]">
            {w.checking}
          </p>
        ) : checkFailed ? (
          <div className="flex flex-wrap items-center gap-md">
            <p role="alert" className="m-0 flex items-center gap-sm text-[15px] font-semibold">
              <Icon name="warning" size={20} />
              {w.checkFailed}
            </p>
            <Button onClick={onRetryCheck}>{t.timetable.retry}</Button>
          </div>
        ) : errors.length > 0 ? (
          <div role="alert" className="flex flex-col gap-sm rounded-md border-[3px] border-warning bg-warning-bg p-md">
            <p className="m-0 flex items-center gap-sm text-[16px] font-bold text-warning">
              <Icon name="warning" size={22} />
              {w.errorsCount(errors.length)}
            </p>
            <Issues issues={errors} draft={draft} options={options} advice={false} />
          </div>
        ) : (
          <p role="status" className="m-0 flex items-center gap-sm text-[15px] font-semibold">
            <Icon name="done" size={20} />
            {w.noProblems}
          </p>
        )}
      </div>

      {warnings.length > 0 ? (
        <div className="flex flex-col gap-sm rounded-md border-[1.5px] border-ink p-md">
          <h3 className="m-0 text-[15px] font-bold">{w.adviceTitle}</h3>
          <p className="m-0 text-label text-muted">{w.adviceHelp}</p>
          <Issues issues={warnings} draft={draft} options={options} advice />
          <label className="flex min-h-touch items-center gap-sm text-[15px] font-semibold">
            <input type="checkbox" className="size-[22px]" checked={confirmed} onChange={(e) => onConfirm(e.target.checked)} />
            {w.adviceConfirm}
          </label>
        </div>
      ) : null}

      <Panel title={w.summary}>
        <dl className="m-0 grid grid-cols-1 gap-sm sm:grid-cols-2">
          <div>
            <dt className="text-caption font-bold text-muted">{w.summaryTrain}</dt>
            <dd className="m-0 text-[15px] font-bold">
              {draft.trainNumber} · {t.timetable.categories[draft.category]} · {t.timetable.types[draft.serviceType]}
            </dd>
          </div>
          <div>
            <dt className="text-caption font-bold text-muted">{draft.kind === 'Weekly' ? w.summaryRuns : w.summaryDates}</dt>
            <dd className="m-0 text-[15px]">
              {draft.kind === 'Weekly'
                ? `${dayCodes.filter((d) => draft.days.includes(d)).join(', ')} · ${t.timetable.detail.weeklyFrom(formatDate(draft.effectiveFrom))}`
                : draft.dates.map(formatDate).join(', ')}
            </dd>
          </div>
        </dl>
        <h3 className="m-0 mt-sm text-[13px] font-bold uppercase tracking-[.6px] text-muted">{w.summaryStops}</h3>
        <ol className="m-0 flex list-none flex-col gap-px p-0 text-[15px]">
          {stops.map((s, i) => (
            <li key={s.stationCode} className="tabular-nums">
              {s.time} · {nameOf(s.stationCode)}
              {s.platform ? ` · ${t.timetable.detail.platform} ${s.platform}` : ''}
              {i === 0 ? ` · ${w.firstStop}` : i === stops.length - 1 ? ` · ${w.lastStop}` : ''}
            </li>
          ))}
        </ol>
      </Panel>

      {scheduleError ? (
        <p role="alert" className="m-0 rounded-md border-[3px] border-warning bg-warning-bg p-[12px] text-label font-semibold text-warning">
          {scheduleError}
        </p>
      ) : null}

      <div className="flex flex-wrap items-center gap-sm">
        <Button variant="primary" onClick={onSchedule} disabled={!canSchedule}>
          {scheduling ? w.scheduling : w.schedule}
        </Button>
      </div>
    </div>
  )
})
