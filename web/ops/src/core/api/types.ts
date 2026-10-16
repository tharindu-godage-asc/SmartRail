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
