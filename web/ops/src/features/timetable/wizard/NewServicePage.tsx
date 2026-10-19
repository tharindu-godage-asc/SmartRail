import { useEffect, useReducer, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { ApiError } from '../../../core/api/client'
import type { AmendResult, CreateResult, LineOptions, ServiceDetail, ValidationResult } from '../../../core/api/types'
import { Icon } from '../../../core/Icon'
import { t, timetableReason } from '../../../core/strings'
import { nextDay, todayInColombo } from '../../../core/time'
import { Button, Panel } from '../../../core/ui'
import { useAmendService, useCreateService, useServiceDetail, useTimetableOptions, useValidate } from '../queries'
import { DetailsStep } from './DetailsStep'
import { detailsProblems, draftFromDetail, initialDraft, reduce, stopsProblems, toRequest, type Kind } from './draft'
import { formatDate } from '../dates'
import { Stepper } from './parts'
import { ReviewStep } from './ReviewStep'
import { StopsStep } from './StopsStep'

/** The service wizard, for a weekly service or an extra train (spec 15). It loads the line, then hands over to the steps. */
export function NewServicePage({ kind }: { kind: Kind }) {
  return <WizardLoader kind={kind} amendId={undefined} />
}

/** The same wizard filled with the current version of a weekly service, to amend it (spec 15 W4). */
export function AmendServicePage() {
  const { serviceId = '' } = useParams()
  return <WizardLoader kind="Weekly" amendId={serviceId} />
}

function WizardLoader({ kind, amendId }: { kind: Kind; amendId: string | undefined }) {
  const options = useTimetableOptions()
  const detail = useServiceDetail(amendId ?? '', amendId !== undefined)

  return (
    <div className="flex flex-col gap-[14px]">
      <Link to={kind === 'Weekly' ? '/timetable' : '/timetable/extra'} className="inline-flex min-h-touch items-center text-[15px] font-semibold text-ink underline">
        ← {t.timetable.back}
      </Link>
      {options.data && (amendId === undefined || detail.data) ? (
        <Wizard kind={kind} options={options.data} amending={detail.data} />
      ) : (
        <Panel>
          {options.isError || detail.isError ? (
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

function Wizard({ kind, options, amending }: { kind: Kind; options: LineOptions; amending?: ServiceDetail }) {
  const w = t.timetable.wizard
  const navigate = useNavigate()
  const [draft, dispatch] = useReducer(reduce, undefined, () =>
    amending ? draftFromDetail(amending, options, nextDay(todayInColombo())) : initialDraft(kind, options, todayInColombo()),
  )
  const [step, setStep] = useState(0)
  const [problems, setProblems] = useState<string[]>([])
  const [confirmed, setConfirmed] = useState(false)
  // The date in the date box of an extra train that was picked but not yet added with "Add date".
  const [pendingDate, setPendingDate] = useState('')
  // What the server said when Schedule itself was refused: it replaces the check of the Review step until the next check.
  const [refused, setRefused] = useState<ValidationResult | undefined>(undefined)
  const [scheduleError, setScheduleError] = useState<string | null>(null)
  const validate = useValidate()
  const create = useCreateService()
  const amend = useAmendService(amending?.serviceId ?? '')
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
    // A date picked in the box but not added yet is added by Next, so choosing a date is never lost (an extra train, step 1).
    const withPending = step === 0 && draft.kind === 'Extra' && pendingDate !== '' && pendingDate >= todayInColombo()
    const current = withPending ? reduce(draft, { type: 'addDate', date: pendingDate }) : draft
    const found = step === 0 ? detailsProblems(current) : stopsProblems(current)
    setProblems(found)
    if (found.length === 0) {
      if (withPending) {
        dispatch({ type: 'addDate', date: pendingDate })
        setPendingDate('')
      }

      setStep(step + 1)
    }
  }

  const onFailure = (error: unknown) => {
    const body = error instanceof ApiError && typeof error.body === 'object' && error.body !== null ? (error.body as Record<string, unknown>) : {}
    if (error instanceof ApiError && error.reason === 'invalid_service') {
      setRefused({ valid: false, errors: (body.errors as ValidationResult['errors']) ?? [], warnings: (body.warnings as ValidationResult['warnings']) ?? [] })
    } else if (error instanceof ApiError && error.reason === 'warnings_not_acknowledged') {
      // The data changed under the screen: ask again so the advice shown is the current one.
      setScheduleError(t.timetable.reasons.warnings_not_acknowledged)
      validate.mutate(toRequest(draft))
      setConfirmed(false)
    } else if (error instanceof ApiError && error.reason === 'journey_has_events' && typeof body.date === 'string') {
      setScheduleError(t.timetable.amend.blocked(formatDate(body.date)))
    } else if (error instanceof ApiError && timetableReason(error.reason) && amending) {
      setScheduleError(timetableReason(error.reason) ?? t.timetable.amend.failed)
    } else if (error instanceof ApiError && error.reason === 'train_number_in_use') {
      setScheduleError(t.timetable.reasons.train_number_in_use)
    } else {
      setScheduleError(amending ? t.timetable.amend.failed : w.scheduleFailed)
    }
  }

  const onSchedule = () => {
    if (create.isPending || amend.isPending) return
    setScheduleError(null)
    const request = toRequest(draft, confirmed)
    if (amending) {
      amend.mutate(request, {
        onSuccess: (saved: AmendResult) =>
          navigate(`/timetable/${saved.serviceId}`, { state: { message: t.timetable.amend.saved(saved.version, saved.journeysUpdated, saved.journeysCancelled) } }),
        onError: onFailure,
      })
      return
    }

    create.mutate(request, {
      onSuccess: (created: CreateResult) => {
        if (draft.kind === 'Weekly') {
          navigate(`/timetable/${created.serviceIds[0]}`, { state: { message: w.scheduled(created.trainNumber, created.journeysGenerated) } })
        } else {
          navigate('/timetable/extra', { state: { message: w.scheduledExtra(created.serviceIds.length) } })
        }
      },
      onError: onFailure,
    })
  }

  return (
    <div className="flex flex-col gap-md">
      <div>
        <div className="text-label text-muted">
          {t.timetable.title} · {draft.kind === 'Weekly' ? t.timetable.tabs.weekly : t.timetable.tabs.extra}
          {draft.trainNumber ? ` · ${draft.trainNumber}` : ''}
        </div>
        <h1 className="m-0 text-heading">{amending ? t.timetable.amend.newTitle(draft.trainNumber) : draft.kind === 'Weekly' ? w.newTitle : w.newExtraTitle}</h1>
      </div>

      <Stepper step={step} />

      <Panel>
        <h2 ref={heading} tabIndex={-1} className="m-0 text-[18px] outline-none">
          {w.steps[step]}
        </h2>

        {step === 0 ? <DetailsStep draft={draft} dispatch={dispatch} issues={[]} pending={pendingDate} onPending={setPendingDate} amending={amending !== undefined} /> : null}
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
            scheduling={create.isPending || amend.isPending}
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
