import { expect } from '@playwright/test'

/**
 * Shared helpers of the critical end-to-end flows (against the Docker Compose stack, dev passwords only). Each flow creates its own
 * extra train for today with a unique number, so it can be run again and again without resetting the database. Journeys and events are
 * never deleted (the audit trail is append-only), so every run leaves one finished-or-abandoned test train 9xxx behind.
 */
export const API = 'http://localhost:8080'
export const KEYCLOAK = 'http://localhost:8180/realms/smartrail/protocol/openid-connect/token'
export const SMALLBITES = 'http://localhost:8090'
export const NIMAL_ID = 'd0d0d0d0-0000-4000-8000-000000000001'

export async function tokenFor(user: string): Promise<string> {
  const response = await fetch(KEYCLOAK, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ client_id: 'smartrail-dev-token', grant_type: 'password', username: user, password: `${user}-dev-only` }),
  })
  expect(response.ok, `token for ${user}`).toBe(true)
  return ((await response.json()) as { access_token: string }).access_token
}

export async function call<T = unknown>(token: string | null, path: string, init: { method?: string; body?: unknown } = {}): Promise<{ status: number; body: T }> {
  const response = await fetch(`${API}${path}`, {
    method: init.method ?? 'GET',
    headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(init.body ? { 'Content-Type': 'application/json' } : {}) },
    body: init.body ? JSON.stringify(init.body) : undefined,
  })
  const text = await response.text()
  return { status: response.status, body: (text ? JSON.parse(text) : null) as T }
}

/** The time in Sri Lanka, `plusMinutes` from now, as HH:mm (services that pass midnight are not supported, so flows skip late at night). */
export function colomboTime(plusMinutes: number): string {
  const at = new Date(Date.now() + plusMinutes * 60_000)
  return new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Colombo', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(at)
}

export function colomboToday(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Colombo' }).format(new Date())
}

export const tooLateToday = (): boolean => Number(colomboTime(0).slice(0, 2)) >= 23 || Number(colomboTime(0).slice(0, 2)) < 1

export interface TestTrain {
  trainNumber: string
  journeyId: string
}

/** Schedules an extra train for today (Veyangoda, Gampaha, Colombo Fort) and returns its unique number. The controller creates it. */
export async function createTrainToday(controller: string): Promise<string> {
  const trainNumber = `9${Math.floor(100 + Math.random() * 900)}`
  const created = await call(controller, '/api/v1/timetable/services', {
    method: 'POST',
    body: {
      kind: 'Extra',
      trainNumber,
      lineCode: 'MAIN',
      category: 'Regional',
      serviceType: 'Slow',
      dates: [colomboToday()],
      stops: [
        { stationCode: 'VYG', arrival: null, departure: colomboTime(10), platform: null },
        { stationCode: 'GPH', arrival: colomboTime(20), departure: colomboTime(21), platform: null },
        { stationCode: 'CF', arrival: colomboTime(30), departure: null, platform: null },
      ],
      acknowledgeWarnings: true,
    },
  })
  expect(created.status, `the extra train was scheduled (a 409 means the random number is taken: run again): ${JSON.stringify(created.body)}`).toBe(201)
  return trainNumber
}

/** The journey of a train today, found through the controller's list of today's journeys. */
export async function findJourneyId(controller: string, trainNumber: string): Promise<string> {
  const list = await call<{ journeyId: string; trainNumber: string }[]>(controller, `/api/v1/assignments/journeys?date=${colomboToday()}&filter=all`)
  const found = list.body.find((j) => j.trainNumber === trainNumber)
  expect(found, `a journey for train ${trainNumber} today`).toBeDefined()
  return found!.journeyId
}

/** Assigns Nimal to the journey from now (the default start). Reports he makes afterwards are for this journey. */
export async function assignNimal(controller: string, journeyId: string): Promise<void> {
  const assigned = await call(controller, `/api/v1/journeys/${journeyId}/assignments`, { method: 'POST', body: { operatorUserId: NIMAL_ID, acknowledgeOverlap: true } })
  expect(assigned.status, `Nimal was assigned: ${JSON.stringify(assigned.body)}`).toBe(201)
}

export interface OperatorStop {
  serviceStopId: string
  stationCode: string
}

export async function nimalJourney(nimal: string): Promise<{ journeyId: string; trainNumber: string; stops: OperatorStop[] }> {
  const journey = await call<{ journeyId: string; trainNumber: string; stops: OperatorStop[] }>(nimal, '/api/v1/operator/journey')
  expect(journey.status).toBe(200)
  return journey.body
}

export async function report(nimal: string, journeyId: string, body: Record<string, unknown>) {
  return call<{ eventId: string }>(nimal, `/api/v1/journeys/${journeyId}/events`, {
    method: 'POST',
    body: { clientEventId: crypto.randomUUID(), occurredAt: new Date().toISOString(), ...body },
  })
}

/** Ends Nimal's assignment so the test train does not stay his current journey. Ignores "already ended". */
export async function releaseNimal(controller: string, journeyId: string): Promise<void> {
  const view = await call<{ assignmentId: string; operatorUserId: string; validTo: string | null; endReason: string | null }[]>(controller, `/api/v1/journeys/${journeyId}/assignments`)
  for (const a of view.body ?? []) {
    if (a.operatorUserId === NIMAL_ID && a.endReason === null) {
      await call(controller, `/api/v1/journeys/${journeyId}/assignments/${a.assignmentId}/end`, { method: 'POST', body: { reason: 'CrewChange' } })
    }
  }
}
