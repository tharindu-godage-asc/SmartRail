import { useEffect, useReducer, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { ApiError } from '../../../core/api/client'
import type { CreateResult, LineOptions, ValidationResult } from '../../../core/api/types'
import { Icon } from '../../../core/Icon'
import { t } from '../../../core/strings'
import { todayInColombo } from '../../../core/time'
import { Button, Panel } from '../../../core/ui'
import { useCreateService, useTimetableOptions, useValidate } from '../queries'
import { DetailsStep } from './DetailsStep'
import { detailsProblems, initialDraft, reduce, stopsProblems, toRequest, type Kind } from './draft'
import { Stepper } from './parts'
import { ReviewStep } from './ReviewStep'
import { StopsStep } from './StopsStep'

/** The service wizard, for a weekly service or an extra train (spec 15). It loads the line, then hands over to the steps. */
export function NewServicePage({ kind }: { kind: Kind }) {
  const options = useTimetableOptions()

  return (
    <div className="flex flex-col gap-[14px]">
      <Link to={kind === 'Weekly' ? '/timetable' : '/timetable/extra'} className="inline-flex min-h-touch items-center text-[15px] font-semibold text-ink underline">
        ← {t.timetable.back}
      </Link>
      {options.data ? (
        <Wizard kind={kind} options={options.data} />
      ) : (
        <Panel>
          {options.isError ? (
            <div className="flex flex-wrap items-center gap-md">
              <p role="alert" className="m-0 flex items-center gap-sm text-[15px] font-semibold">
                <Icon name="warning" size={20} />
                {t.timetable.loadFailed}
              </p>
              <Button onClick={() => void options.refetch()}>{t.timetable.retry}</Button>
            </div>
          ) : (
            <p className="m-0 text-label text-muted">{t.timetable.loading}</p>
          )}
        </Panel>
      )}
    </div>
  )
}

function Wizard({ kind, options }: { kind: Kind; options: LineOptions }) {
  const w = t.timetable.wizard
  const navigate = useNavigate()
  const [draft, dispatch] = useReducer(reduce, undefined, () => initialDraft(kind, options, todayInColombo()))
  const [step, setStep] = useState(0)
  const [problems, setProblems] = useState<string[]>([])
  const [confirmed, setConfirmed] = useState(false)
  // What the server said when Schedule itself was refused: it replaces the check of the Review step until the next check.
  const [refused, setRefused] = useState<ValidationResult | undefined>(undefined)
  const [scheduleError, setScheduleError] = useState<string | null>(null)
  const validate = useValidate()
  const create = useCreateService()
  const heading = useRef<HTMLHeadingElement>(null)
  const summary = useRef<HTMLDivElement>(null)

  // Focus moves to the step heading when the step changes (spec 15 section 7).
  useEffect(() => {
    heading.current?.focus()
  }, [step])

  const check = () => {
    setConfirmed(false)
    setRefused(undefined)
    setScheduleError(null)
    validate.mutate(toRequest(draft))
  }

  // Entering the Review step asks the server (spec 15 rule 1). The error summary takes focus when there are errors.
  useEffect(() => {
    if (step === 2) {
      check()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- check once on entering the step, not on every keystroke of the draft
  }, [step])

  const result = refused ?? validate.data
  useEffect(() => {
    if (step === 2 && result && result.errors.length > 0) {
      summary.current?.focus()
    }
  }, [step, result])

  const goNext = () => {
    const found = step === 0 ? detailsProblems(draft) : stopsProblems(draft)
    setProblems(found)
    if (found.length === 0) setStep(step + 1)
  }

  const onSchedule = () => {
    if (create.isPending) return
    setScheduleError(null)
    create.mutate(toRequest(draft, confirmed), {
      onSuccess: (created: CreateResult) => {
        if (draft.kind === 'Weekly') {
          navigate(`/timetable/${created.serviceIds[0]}`, { state: { message: w.scheduled(created.trainNumber, created.journeysGenerated) } })
        } else {
          navigate('/timetable/extra', { state: { message: w.scheduledExtra(created.serviceIds.length) } })
        }
      },
      onError: (error) => {
        const body = error instanceof ApiError && typeof error.body === 'object' && error.body !== null ? (error.body as Record<string, unknown>) : {}
        if (error instanceof ApiError && error.reason === 'invalid_service') {
          setRefused({ valid: false, errors: (body.errors as ValidationResult['errors']) ?? [], warnings: (body.warnings as ValidationResult['warnings']) ?? [] })
        } else if (error instanceof ApiError && error.reason === 'warnings_not_acknowledged') {
          // The data changed under the screen: ask again so the advice shown is the current one.
          setScheduleError(t.timetable.reasons.warnings_not_acknowledged)
          validate.mutate(toRequest(draft))
          setConfirmed(false)
        } else if (error instanceof ApiError && error.reason === 'train_number_in_use') {
          setScheduleError(t.timetable.reasons.train_number_in_use)
        } else {
          setScheduleError(w.scheduleFailed)
        }
      },
    })
  }

  return (
    <div className="flex flex-col gap-md">
      <div>
        <div className="text-label text-muted">
          {t.timetable.title} · {draft.kind === 'Weekly' ? t.timetable.tabs.weekly : t.timetable.tabs.extra}
          {draft.trainNumber ? ` · ${draft.trainNumber}` : ''}
        </div>
        <h1 className="m-0 text-heading">{draft.kind === 'Weekly' ? w.newTitle : w.newExtraTitle}</h1>
      </div>

      <Stepper step={step} />

      <Panel>
        <h2 ref={heading} tabIndex={-1} className="m-0 text-[18px] outline-none">
          {w.steps[step]}
        </h2>

        {step === 0 ? <DetailsStep draft={draft} dispatch={dispatch} issues={[]} /> : null}
        {step === 1 ? <StopsStep draft={draft} options={options} dispatch={dispatch} issues={result?.errors ?? []} /> : null}
        {step === 2 ? (
          <ReviewStep
            ref={summary}
            draft={draft}
            options={options}
            checking={validate.isPending}
            checkFailed={validate.isError}
            result={result}
            confirmed={confirmed}
            onConfirm={setConfirmed}
            scheduling={create.isPending}
            scheduleError={scheduleError}
            onRetryCheck={check}
            onSchedule={onSchedule}
          />
        ) : null}

        {problems.length > 0 && step < 2 ? (
          <ul role="alert" className="m-0 flex list-none flex-col gap-px rounded-md border-[3px] border-warning bg-warning-bg p-[12px] text-label font-semibold text-warning">
            {problems.map((p) => (
              <li key={p} className="flex items-center gap-xs">
                <Icon name="warning" size={16} />
                {w.error}: {p}
              </li>
            ))}
          </ul>
        ) : null}

        <div className="flex flex-wrap justify-between gap-sm">
          <div className="flex gap-sm">
            {step > 0 ? (
              <Button
                onClick={() => {
                  setProblems([])
                  setStep(step - 1)
                }}
              >
                {w.back}
              </Button>
            ) : null}
            <Link to={draft.kind === 'Weekly' ? '/timetable' : '/timetable/extra'} className="inline-flex min-h-touch items-center px-sm text-[15px] font-semibold text-ink underline">
              {w.cancel}
            </Link>
          </div>
          {step < 2 ? (
            <Button variant="primary" onClick={goNext}>
              {step === 1 ? w.nextReview : w.next}
            </Button>
          ) : null}
        </div>
      </Panel>
    </div>
  )
}
