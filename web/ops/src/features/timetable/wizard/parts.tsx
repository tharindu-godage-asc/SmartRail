import type { ReactNode } from 'react'
import type { ValidationIssue } from '../../../core/api/types'
import { Icon } from '../../../core/Icon'
import { t } from '../../../core/strings'

/** A labelled field: the label is always visible and tied to its input; a problem sits under the field with an icon and the word "Error". */
export function Field({
  id,
  label,
  hint,
  issues = [],
  children,
}: {
  id: string
  label: string
  hint?: string
  issues?: ValidationIssue[]
  children: ReactNode
}) {
  return (
    <div className="flex flex-col gap-xs">
      <label htmlFor={id} className="text-label font-bold">
        {label}
      </label>
      {children}
      {hint ? (
        <span id={`${id}-hint`} className="text-caption text-muted">
          {hint}
        </span>
      ) : null}
      <IssueList issues={issues} id={`${id}-issues`} />
    </div>
  )
}

/** Errors and advice for one place, each with an icon and a word, so colour is never the only sign (NFR-30). */
export function IssueList({ issues, id }: { issues: ValidationIssue[]; id?: string }) {
  if (issues.length === 0) return null
  return (
    <ul id={id} className="m-0 flex list-none flex-col gap-px p-0">
      {issues.map((issue, i) => (
        <li key={`${issue.code}-${i}`} className="flex items-start gap-xs text-caption font-semibold text-warning">
          <Icon name={issue.code === 'platform_close' ? 'info' : 'warning'} size={14} />
          <span>
            <span className="font-bold">{issue.code === 'platform_close' ? t.timetable.wizard.advice : t.timetable.wizard.error}: </span>
            {issue.message || t.timetable.issues[issue.code] || issue.code}
          </span>
        </li>
      ))}
    </ul>
  )
}

/** The three steps as a list with the current one marked by `aria-current="step"` and by a heavier border, not by colour alone. */
export function Stepper({ step }: { step: number }) {
  return (
    <nav aria-label={t.timetable.wizard.stepper}>
      <ol className="m-0 flex list-none flex-wrap gap-sm p-0">
        {t.timetable.wizard.steps.map((name, i) => (
          <li
            key={name}
            aria-current={i === step ? 'step' : undefined}
            className={`flex min-h-[40px] items-center gap-sm rounded-md px-[12px] text-label ${
              i === step ? 'border-[3px] border-ink font-bold' : 'border border-line text-muted'
            }`}
          >
            <span className="inline-flex size-[22px] items-center justify-center rounded-pill border-[1.5px] border-current text-caption font-bold">
              {i < step ? <Icon name="done" size={14} /> : i + 1}
            </span>
            {name}
          </li>
        ))}
      </ol>
    </nav>
  )
}
