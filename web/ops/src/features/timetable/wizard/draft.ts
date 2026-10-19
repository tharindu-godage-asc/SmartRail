import type { LineOptions, ServiceRequest, StopRequest } from '../../../core/api/types'
import { t } from '../../../core/strings'

/**
 * The draft of a new service while the controller moves between the three steps (spec 15 section 5). It lives in memory only and is
 * turned into the request body in one place, `toRequest`. The screen never re-implements the rules of FR-11D: the server decides.
 */

export type Kind = 'Weekly' | 'Extra'
export type Direction = 'inbound' | 'outbound'

export const dayCodes = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'] as const

export interface StopDraft {
  stationCode: string
  included: boolean
  /** HH:mm. First stop: the departure. Last stop: the arrival. Any other included stop: the arrival (the departure is sent 1 minute later). */
  time: string
  /** A platform code of the station, or null for "Not set". */
  platform: string | null
}

export interface Draft {
  kind: Kind
  trainNumber: string
  category: 'Regional' | 'LongDistance'
  serviceType: 'Slow' | 'Express'
  direction: Direction
  days: string[]
  dates: string[]
  effectiveFrom: string
  stopMode: 'every' | 'choose'
  /** Every station of the line in line order (the first one is the line's first station), whether the train stops there or not. */
  stops: StopDraft[]
}

export function initialDraft(kind: Kind, options: LineOptions, today: string): Draft {
  const ordered = [...options.stations].sort((a, b) => a.sequence - b.sequence)
  return {
    kind,
    trainNumber: '',
    category: 'Regional',
    serviceType: 'Slow',
    direction: 'inbound',
    days: [],
    dates: [],
    effectiveFrom: kind === 'Weekly' ? today : '',
    stopMode: 'every',
    stops: ordered.map((s) => ({ stationCode: s.code, included: true, time: '', platform: null })),
  }
}

/** The stops in the order the train runs: inbound goes toward the line's first station, so it is the line order reversed. */
export const inRunningOrder = (draft: Draft): StopDraft[] => (draft.direction === 'inbound' ? [...draft.stops].reverse() : [...draft.stops])

/** The first and last station of the run are always included (spec 15 rule 4). */
export function isLocked(draft: Draft, stationCode: string): boolean {
  const run = inRunningOrder(draft)
  return run[0]?.stationCode === stationCode || run[run.length - 1]?.stationCode === stationCode
}

export type Action =
  | { type: 'set'; field: 'trainNumber' | 'effectiveFrom'; value: string }
  | { type: 'category'; value: Draft['category'] }
  | { type: 'serviceType'; value: Draft['serviceType'] }
  | { type: 'direction'; value: Direction }
  | { type: 'toggleDay'; day: string }
  | { type: 'addDate'; date: string }
  | { type: 'removeDate'; date: string }
  | { type: 'stopMode'; mode: Draft['stopMode'] }
  | { type: 'toggleStop'; stationCode: string }
  | { type: 'time'; stationCode: string; value: string }
  | { type: 'platform'; stationCode: string; value: string | null }

export function reduce(draft: Draft, action: Action): Draft {
  switch (action.type) {
    case 'set':
      return { ...draft, [action.field]: action.value }
    case 'category':
      return { ...draft, category: action.value }
    case 'serviceType':
      return { ...draft, serviceType: action.value }
    case 'direction':
      return { ...draft, direction: action.value }
    case 'toggleDay':
      return { ...draft, days: draft.days.includes(action.day) ? draft.days.filter((d) => d !== action.day) : [...draft.days, action.day] }
    case 'addDate':
      return draft.dates.includes(action.date) || !action.date ? draft : { ...draft, dates: [...draft.dates, action.date].sort() }
    case 'removeDate':
      return { ...draft, dates: draft.dates.filter((d) => d !== action.date) }
    case 'stopMode':
      // "Every stop" includes the whole line again; "Choose stops" keeps what is included now.
      return { ...draft, stopMode: action.mode, stops: action.mode === 'every' ? draft.stops.map((s) => ({ ...s, included: true })) : draft.stops }
    case 'toggleStop':
      return isLocked(draft, action.stationCode)
        ? draft
        : { ...draft, stops: draft.stops.map((s) => (s.stationCode === action.stationCode ? { ...s, included: !s.included } : s)) }
    case 'time':
      return { ...draft, stops: draft.stops.map((s) => (s.stationCode === action.stationCode ? { ...s, time: action.value } : s)) }
    case 'platform':
      return { ...draft, stops: draft.stops.map((s) => (s.stationCode === action.stationCode ? { ...s, platform: action.value } : s)) }
  }
}

const timePattern = /^([01]\d|2[0-3]):[0-5]\d$/
export const isTime = (value: string): boolean => timePattern.test(value)

/** "08:59" + 1 minute is "09:00". A time past midnight wraps to "00:00", which the server then refuses as not increasing. */
export function addMinute(time: string): string {
  const [h, m] = time.split(':').map(Number)
  const total = ((h ?? 0) * 60 + (m ?? 0) + 1) % (24 * 60)
  return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`
}

/** The stops that are included, in the order the train runs. */
export const includedStops = (draft: Draft): StopDraft[] => inRunningOrder(draft).filter((s) => s.included)

/** What the screen cannot send (spec 15 rule 1). Everything else is for the server to decide. */
export function detailsProblems(draft: Draft): string[] {
  const w = t.timetable.wizard
  const problems: string[] = []
  if (draft.trainNumber.trim() === '') problems.push(w.needNumber)
  if (draft.kind === 'Weekly') {
    if (draft.days.length === 0) problems.push(w.needDays)
    if (draft.effectiveFrom === '') problems.push(w.needDate)
  } else if (draft.dates.length === 0) {
    problems.push(w.needDates)
  }

  return problems
}

export function stopsProblems(draft: Draft): string[] {
  const w = t.timetable.wizard
  const included = includedStops(draft)
  const problems: string[] = []
  if (included.length < 2) problems.push(w.needTwoStops)
  if (included.some((s) => !isTime(s.time))) problems.push(w.needTimes)
  return problems
}

/** The request body of spec 13 section 4.5. A middle stop sends its arrival and leaves 1 minute later (spec 15 Q2). */
export function toRequest(draft: Draft, acknowledgeWarnings = false): ServiceRequest {
  const included = includedStops(draft)
  const stops: StopRequest[] = included.map((s, i) => {
    const first = i === 0
    const last = i === included.length - 1
    return {
      stationCode: s.stationCode,
      arrival: first ? null : s.time,
      departure: first ? s.time : last ? null : addMinute(s.time),
      platform: s.platform,
    }
  })

  const base = {
    trainNumber: draft.trainNumber.trim(),
    lineCode: 'MAIN',
    category: draft.category,
    serviceType: draft.serviceType,
    stops,
    ...(acknowledgeWarnings ? { acknowledgeWarnings: true } : {}),
  }

  return draft.kind === 'Weekly'
    ? { kind: 'Weekly', ...base, days: dayCodes.filter((d) => draft.days.includes(d)), effectiveFrom: draft.effectiveFrom }
    : { kind: 'Extra', ...base, dates: [...draft.dates].sort() }
}
