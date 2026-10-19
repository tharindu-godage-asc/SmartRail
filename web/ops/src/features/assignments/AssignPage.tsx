import { useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router'
import { ApiError } from '../../core/api/client'
import type { AssignRequest, BusyOn } from '../../core/api/types'
import { Icon } from '../../core/Icon'
import { assignmentReason, t } from '../../core/strings'
import { formatClockShort, todayInColombo } from '../../core/time'
import { Button, Panel } from '../../core/ui'
import { formatDate } from '../timetable/dates'
import { inputClass } from '../timetable/wizard/styles'
import { useAssign, useAssignmentJourneys, useOperators } from './queries'

const timePattern = /^([01]\d|2[0-3]):[0-5]\d$/

/** A time of the journey's day as an instant: the railway runs on Sri Lanka time (UTC+05:30, no daylight saving). */
const instantOf = (date: string, time: string): string => `${date}T${time}:00+05:30`

/**
 * Assign an operator to one journey (spec 17 A2, `Dash-Assign`): choose an operator (available, or busy on another train), a start and an
 * end. An operator on another journey in that range is advice, confirmed with a box, never a block (rule 5). A start while another operator
 * is assigned is a crew change: that assignment ends there (rule 4).
 */
export function AssignPage() {
  const { journeyId = '' } = useParams()
  const [params] = useSearchParams()
  const date = params.get('date') ?? todayInColombo()
  const navigate = useNavigate()
  const journeys = useAssignmentJourneys(date, 'all')
  const journey = journeys.data?.find((j) => j.journeyId === journeyId)
  const operators = useOperators(journeyId)
  const assign = useAssign(journeyId)
  const [chosen, setChosen] = useState('')
  const [fromMode, setFromMode] = useState<'default' | 'time'>('default')
  const [toMode, setToMode] = useState<'end' | 'time'>('end')
  const [fromTime, setFromTime] = useState('')
  const [toTime, setToTime] = useState('')
  const [problem, setProblem] = useState<string | null>(null)
  const [advice, setAdvice] = useState<BusyOn | null>(null)
  const [confirmed, setConfirmed] = useState(false)
  const a = t.assignments
  const p = a.page

  const submit = () => {
    if (assign.isPending) return
    if (chosen === '') return setProblem(p.needOperator)
    if ((fromMode === 'time' && !timePattern.test(fromTime)) || (toMode === 'time' && !timePattern.test(toTime))) return setProblem(p.needTime)
    setProblem(null)
    const request: AssignRequest = {
      operatorUserId: chosen,
      ...(fromMode === 'time' ? { validFrom: instantOf(date, fromTime) } : {}),
      ...(toMode === 'time' ? { validTo: instantOf(date, toTime) } : {}),
      ...(confirmed ? { acknowledgeOverlap: true } : {}),
    }

    assign.mutate(request, {
      onSuccess: (done) => navigate(`/journeys?date=${date}`, { state: { message: a.assigned(journey?.trainNumber ?? '', done.name) } }),
      onError: (error) => {
        if (error instanceof ApiError && error.reason === 'operator_overlaps_another_journey') {
          const body = (error.body ?? {}) as Record<string, unknown>
          setAdvice({ journeyId: String(body.journeyId ?? ''), trainNumber: String(body.trainNumber ?? ''), until: typeof body.until === 'string' ? body.until : null })
          setConfirmed(false)
        } else {
          setProblem(error instanceof ApiError ? (assignmentReason(error.reason) ?? p.directoryFailed) : p.directoryFailed)
        }
      },
    })
  }

  const chosenName = operators.data?.find((o) => o.userId === chosen)?.name ?? ''

  return (
    <div className="flex flex-col gap-[14px]">
      <Link to={`/journeys?date=${date}`} className="inline-flex min-h-touch items-center text-[15px] font-semibold text-ink underline">
        ← {a.back}
      </Link>
      <div>
        <div className="text-label text-muted">
          {journey ? `${t.timetable.to(journey.from, journey.to)} · ${formatClockShort(journey.firstDeparture)} · ${formatDate(journey.operatingDate)}` : t.nav.mainLine}
        </div>
        <h1 className="m-0 text-heading">{p.title(journey?.trainNumber ?? '')}</h1>
      </div>

      <Panel>
        <fieldset className="m-0 flex flex-col gap-xs border-0 p-0">
          <legend className="mb-xs p-0 text-label font-bold">{p.operator}</legend>
          {operators.isError ? (
            <div className="flex flex-wrap items-center gap-md">
              <p role="alert" className="m-0 flex items-center gap-sm text-[15px] font-semibold">
                <Icon name="warning" size={20} />
                {p.directoryFailed}
              </p>
              <Button onClick={() => void operators.refetch()}>{a.retry}</Button>
            </div>
          ) : operators.isPending ? (
            <p className="m-0 text-label text-muted">{a.loading}</p>
          ) : operators.data.length === 0 ? (
            <p className="m-0 text-[15px]">{p.noOperators}</p>
          ) : (
            operators.data.map((o) => (
              <label key={o.userId} className="flex min-h-touch flex-wrap items-center gap-sm rounded-md border border-line px-[12px] text-[15px]">
                <input type="radio" name="operator" checked={chosen === o.userId} disabled={!o.enabled} onChange={() => setChosen(o.userId)} />
                <span className="font-bold">{o.name}</span>
                {!o.enabled ? (
                  <span className="text-muted">{p.disabled}</span>
                ) : o.available ? (
                  <span className="inline-flex items-center gap-xs text-muted">
                    <Icon name="done" size={16} />
                    {p.available}
                  </span>
                ) : (
                  <span className="inline-flex items-center gap-xs font-semibold text-warning">
                    <Icon name="warning" size={16} />
                    {p.busy(o.busyOn?.trainNumber ?? '', o.busyOn?.until ? formatClockShort(o.busyOn.until) : '')}
                  </span>
                )}
              </label>
            ))
          )}
        </fieldset>

        <div className="flex flex-wrap gap-lg">
          <fieldset className="m-0 flex flex-col gap-xs border-0 p-0">
            <legend className="mb-xs p-0 text-label font-bold">{p.from}</legend>
            <label className="flex min-h-touch items-center gap-sm text-[15px]">
              <input type="radio" name="from" checked={fromMode === 'default'} onChange={() => setFromMode('default')} />
              {p.fromDefault(30)}
            </label>
            <label className="flex min-h-touch items-center gap-sm text-[15px]">
              <input type="radio" name="from" checked={fromMode === 'time'} onChange={() => setFromMode('time')} />
              {p.fromTime}
            </label>
            {fromMode === 'time' ? (
              <input aria-label={`${p.from}: ${p.time}`} className={`${inputClass} w-[110px] tabular-nums`} value={fromTime} placeholder="HH:mm" maxLength={5} onChange={(e) => setFromTime(e.target.value)} />
            ) : null}
          </fieldset>
          <fieldset className="m-0 flex flex-col gap-xs border-0 p-0">
            <legend className="mb-xs p-0 text-label font-bold">{p.to}</legend>
            <label className="flex min-h-touch items-center gap-sm text-[15px]">
              <input type="radio" name="to" checked={toMode === 'end'} onChange={() => setToMode('end')} />
              {p.toEnd}
            </label>
            <label className="flex min-h-touch items-center gap-sm text-[15px]">
              <input type="radio" name="to" checked={toMode === 'time'} onChange={() => setToMode('time')} />
              {p.toTime}
            </label>
            {toMode === 'time' ? (
              <input aria-label={`${p.to}: ${p.time}`} className={`${inputClass} w-[110px] tabular-nums`} value={toTime} placeholder="HH:mm" maxLength={5} onChange={(e) => setToTime(e.target.value)} />
            ) : null}
          </fieldset>
        </div>
        <p className="m-0 flex items-start gap-sm text-label text-muted">
          <Icon name="info" size={18} />
          {p.crewNote}
        </p>

        {advice ? (
          <div className="flex flex-col gap-sm rounded-md border-[1.5px] border-ink p-md">
            <h2 className="m-0 text-[15px] font-bold">{p.adviceTitle}</h2>
            <p className="m-0 flex items-start gap-sm text-[15px]">
              <Icon name="info" size={18} />
              {p.advice(chosenName, advice.trainNumber, advice.until ? formatClockShort(advice.until) : '')}
            </p>
            <label className="flex min-h-touch items-center gap-sm text-[15px] font-semibold">
              <input type="checkbox" className="size-[22px]" checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} />
              {p.adviceConfirm}
            </label>
          </div>
        ) : null}

        {problem ? (
          <p role="alert" className="m-0 rounded-md border-[3px] border-warning bg-warning-bg p-[12px] text-label font-semibold text-warning">
            {problem}
          </p>
        ) : null}

        <div className="flex flex-wrap gap-sm">
          <Button variant="primary" onClick={submit} disabled={assign.isPending || (advice !== null && !confirmed)}>
            {assign.isPending ? p.submitting : p.submit}
          </Button>
          <Link to={`/journeys?date=${date}`} className="inline-flex min-h-touch items-center px-sm text-[15px] font-semibold text-ink underline">
            {p.cancel}
          </Link>
        </div>
      </Panel>
    </div>
  )
}
