/** The shapes of the three controller endpoints (spec 07). The API sends codes, the client words them (src/core/strings.ts). */

export type ResolveAction = 'Acknowledged' | 'NoActionNeeded' | 'Escalated'

export interface ExceptionItem {
  id: string
  journeyId: string
  trainNumber: string
  stationName: string | null
  /** `ReasonAwaitingVerification` or `EarlyDeparture` today; an unknown code is shown as it is. */
  type: string
  /** `Critical` or `High` today. */
  severity: string
  createdAt: string
  ageSeconds: number
  relatedEventId: string | null
}

export interface ClosedException {
  id: string
  journeyId: string
  trainNumber: string
  stationName: string | null
  type: string
  severity: string
  createdAt: string
  resolvedAt: string
  /** The controller's user name, or null when none was stored (older rows, or a token without one). */
  resolvedByName: string | null
  /** `Acknowledged`, `NoActionNeeded` or `Escalated`. */
  resolution: string
  secondsToResolve: number
}

export interface ClosedExceptions {
  items: ClosedException[]
  /** How many closed exceptions exist in all; `items` holds the latest `limit`. */
  total: number
  limit: number
}

export interface OpenExceptions {
  items: ExceptionItem[]
  openCount: number
}

export interface OverviewLastEvent {
  type: string
  stationName: string | null
  occurredAt: string
  source: string
}

export interface OverviewJourney {
  journeyId: string
  trainNumber: string
  from: string
  to: string
  statusGroup: string
  statusKey: string
  delayMinutes: number
  delayKnown: boolean
  lastEvent: OverviewLastEvent | null
  nextStop: { stationName: string; expectedAt: string } | null
  secondsSinceUpdate: number | null
  /** `awaiting` or `none` today. */
  verification: string
}

export interface Overview {
  openExceptionCount: number
  journeys: OverviewJourney[]
  serverTime: string
}

// ---- Timetable (specs 13 and 14). Dates are YYYY-MM-DD, times HH:mm. ----

export interface OptionsStation {
  code: string
  sequence: number
  names: Record<string, string>
  platforms: string[]
}

export interface LineOptions {
  lineCode: string
  stations: OptionsStation[]
}

export interface DateRange {
  from: string
  to: string | null
}

export interface ServiceRow {
  serviceId: string
  trainNumber: string
  category: string
  serviceType: string
  fromName: string
  toName: string
  /** Day codes `Mon` to `Sun`; null for an extra train. */
  runsOn: string[] | null
  /** The date of an extra train; null for a weekly service. */
  date: string | null
  firstDeparture: string
  stopCount: number
  stopsLabel: string
  version: number
  amended: boolean
  effectiveFrom: string
  cancelledRanges: DateRange[]
}

export interface CancellationView {
  cancellationId: string
  serviceId: string
  trainNumber: string
  fromName: string
  toName: string
  from: string
  to: string | null
  reason: string
  cancelledBy: string
  cancelledAt: string
}

export interface ServiceStopView {
  sequence: number
  stationCode: string
  names: Record<string, string>
  arrival: string | null
  departure: string | null
  platform: string | null
  isFirst: boolean
  isLast: boolean
}

export interface ServiceVersionView {
  version: number
  serviceId: string
  effectiveFrom: string
  effectiveTo: string | null
  createdAt: string | null
  createdBy: string | null
  summary: string | null
}

export interface ServiceDetail {
  serviceId: string
  trainNumber: string
  kind: string
  category: string
  serviceType: string
  direction: string
  runsOn: string[] | null
  effectiveFrom: string
  effectiveTo: string | null
  version: number
  stops: ServiceStopView[]
  versions: ServiceVersionView[]
  cancellations: CancellationView[]
}

export interface ValidationIssue {
  code: string
  field: string
  message: string
}

export interface ValidationResult {
  valid: boolean
  errors: ValidationIssue[]
  warnings: ValidationIssue[]
}

export interface StopRequest {
  stationCode: string
  arrival: string | null
  departure: string | null
  platform: string | null
}

export interface ServiceRequest {
  kind: 'Weekly' | 'Extra'
  trainNumber: string
  lineCode: string
  category: string
  serviceType: string
  days?: string[]
  dates?: string[]
  effectiveFrom?: string
  stops: StopRequest[]
  acknowledgeWarnings?: boolean
}

export interface CreateResult {
  serviceIds: string[]
  trainNumber: string
  version: number
  journeysGenerated: number
  warnings: ValidationIssue[]
}
