import type { Dispatch } from 'react'
import type { ValidationIssue } from '../../../core/api/types'
import { t } from '../../../core/strings'
import { nextDay, todayInColombo } from '../../../core/time'
import { Button } from '../../../core/ui'
import { formatDate } from '../dates'
import { dayCodes, type Action, type Draft } from './draft'
import { issuesForDetails } from './issues'
import { Field } from './parts'
import { inputClass } from './styles'

/** Step 1: train number, category, type, direction, and the days (weekly) or the dates (extra train). */
export function DetailsStep({
  draft,
  dispatch,
  issues,
  pending,
  onPending,
}: {
  draft: Draft
  dispatch: Dispatch<Action>
  issues: ValidationIssue[]
  /** The date shown in the date box of an extra train that has not been added to the list yet (Next adds it). */
  pending: string
  onPending: (date: string) => void
}) {
  const w = t.timetable.wizard
  const today = todayInColombo()
  const at = (field: Parameters<typeof issuesForDetails>[2]) => issuesForDetails(issues, draft, field)

  return (
    <div className="flex flex-col gap-md">
      <Field id="train-number" label={w.trainNumber} hint={w.trainNumberHint} issues={at('trainNumber')}>
        <input
          id="train-number"
          className={`${inputClass} max-w-[220px]`}
          value={draft.trainNumber}
          maxLength={10}
          aria-describedby="train-number-hint"
          onChange={(e) => dispatch({ type: 'set', field: 'trainNumber', value: e.target.value })}
        />
      </Field>

      <div className="flex flex-wrap gap-md">
        <Field id="category" label={w.category} issues={at('category')}>
          <select
            id="category"
            className={inputClass}
            value={draft.category}
            onChange={(e) => dispatch({ type: 'category', value: e.target.value as Draft['category'] })}
          >
            <option value="Regional">{t.timetable.categories.Regional}</option>
            <option value="LongDistance">{t.timetable.categories.LongDistance}</option>
          </select>
        </Field>
        <Field id="service-type" label={w.type} issues={at('serviceType')}>
          <select
            id="service-type"
            className={inputClass}
            value={draft.serviceType}
            onChange={(e) => dispatch({ type: 'serviceType', value: e.target.value as Draft['serviceType'] })}
          >
            <option value="Slow">{t.timetable.types.Slow}</option>
            <option value="Express">{t.timetable.types.Express}</option>
          </select>
        </Field>
      </div>

      <fieldset className="m-0 flex flex-col gap-xs border-0 p-0">
        <legend className="mb-xs p-0 text-label font-bold">{w.direction}</legend>
        <div className="flex flex-wrap gap-md">
          {(['inbound', 'outbound'] as const).map((d) => (
            <label key={d} className="flex min-h-touch items-center gap-sm text-[15px]">
              <input type="radio" name="direction" checked={draft.direction === d} onChange={() => dispatch({ type: 'direction', value: d })} />
              {d === 'inbound' ? w.directionInbound : w.directionOutbound}
            </label>
          ))}
        </div>
      </fieldset>

      {draft.kind === 'Weekly' ? (
        <>
          <Field id="effective-from" label={w.firstDate} issues={at('effectiveFrom')}>
            <input
              id="effective-from"
              type="date"
              className={`${inputClass} max-w-[220px]`}
              value={draft.effectiveFrom}
              min={today}
              onChange={(e) => dispatch({ type: 'set', field: 'effectiveFrom', value: e.target.value })}
            />
          </Field>
          <fieldset className="m-0 flex flex-col gap-xs border-0 p-0">
            <legend className="mb-xs p-0 text-label font-bold">{w.runsOn}</legend>
            <div className="flex flex-wrap gap-sm">
              {dayCodes.map((d) => (
                <label key={d} className="flex min-h-touch items-center gap-xs rounded-md border-[1.5px] border-ink px-[10px] text-[15px] font-semibold">
                  <input type="checkbox" checked={draft.days.includes(d)} onChange={() => dispatch({ type: 'toggleDay', day: d })} />
                  <span aria-hidden="true">{d}</span>
                  <span className="sr-only">{w.days[d]}</span>
                </label>
              ))}
            </div>
            {at('days').length > 0 ? <p className="m-0 text-caption font-semibold text-warning">{at('days')[0]?.message}</p> : null}
          </fieldset>
        </>
      ) : (
        <fieldset className="m-0 flex flex-col gap-sm border-0 p-0">
          <legend className="mb-xs p-0 text-label font-bold">{w.dates}</legend>
          <div className="flex flex-wrap items-end gap-sm">
            <Field id="extra-date" label={w.dates}>
              <input
                id="extra-date"
                type="date"
                className={inputClass}
                value={pending}
                min={today}
                onChange={(e) => onPending(e.target.value)}
              />
            </Field>
            <Button
              onClick={() => {
                dispatch({ type: 'addDate', date: pending })
                onPending(nextDay(pending || today))
              }}
              disabled={pending === ''}
            >
              {w.addDate}
            </Button>
          </div>
          <p className="m-0 text-caption text-muted">{w.datesHint}</p>
          {draft.dates.length === 0 ? (
            <p className="m-0 text-label text-muted">{w.noDates}</p>
          ) : (
            <ul className="m-0 flex list-none flex-wrap gap-sm p-0">
              {draft.dates.map((date) => (
                <li key={date} className="inline-flex items-center gap-xs rounded-pill border-[1.5px] border-ink pl-[12px] text-label font-semibold">
                  {formatDate(date)}
                  <button
                    type="button"
                    aria-label={w.removeDate(formatDate(date))}
                    onClick={() => dispatch({ type: 'removeDate', date })}
                    className="min-h-[36px] min-w-[36px] rounded-pill text-[16px] font-bold"
                  >
                    ×
                  </button>
                </li>
              ))}
            </ul>
          )}
          {at('dates').length > 0 ? <p className="m-0 text-caption font-semibold text-warning">{at('dates')[0]?.message}</p> : null}
        </fieldset>
      )}
    </div>
  )
}
