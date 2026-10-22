import { expect } from '@playwright/test'

/**
 * Shared helpers of the critical end-to-end flows (against the Docker Compose stack, dev passwords only). Each flow creates its own
 * extra train for today with a unique number, so it can be run again and again without resetting the database. Journeys and events are
 * never deleted (the audit trail is append-only), so every run leaves one finished-or-abandoned test train 9xxx behind.
 */
export const API = 'http://localhost:8080'
export const KEYCLOAK = 'http://localhost:8180/realms/smartrail/protocol/openid-connect/token'
export const SMALLBITES = 'http://localhost:8090'
/**
 * The operator the flows use: operator2 ("Second Operator" in the directory), who is never assigned by the demo seeder. the operator is the demo operator
 * of journey 8058 and is often busy (an operator cannot be on two journeys at once), so the flows must not borrow him.
 */
export const OPERATOR_USER = 'operator2'
export const OPERATOR_ID = 'd0d0d0d0-0000-4000-8000-000000000002'
export const OPERATOR_NAME = 'Second Operator'

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

/**
 * Schedules an extra train for today (Veyangoda, Gampaha, Colombo Fort) and returns its unique number. The controller creates it.
 * Test trains are never deleted, so the random number may already exist (a 409 `train_number_in_use`): another one is tried.
 */
export async function createTrainToday(controller: string): Promise<string> {
  for (let attempt = 1; ; attempt++) {
    const trainNumber = `9${Math.floor(100 + Math.random() * 900)}`
    const created = await call<{ reason?: string }>(controller, '/api/v1/timetable/services', {
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
    if (created.status === 409 && created.body?.reason === 'train_number_in_use' && attempt < 8) continue
    expect(created.status, `the extra train was scheduled: ${JSON.stringify(created.body)}`).toBe(201)
    return trainNumber
  }
}

/** The journey of a train today, found through the controller's list of today's journeys. */
export async function findJourneyId(controller: string, trainNumber: string): Promise<string> {
  const list = await call<{ journeyId: string; trainNumber: string }[]>(controller, `/api/v1/assignments/journeys?date=${colomboToday()}&filter=all`)
  const found = list.body.find((j) => j.trainNumber === trainNumber)
  expect(found, `a journey for train ${trainNumber} today`).toBeDefined()
  return found!.journeyId
}

/** Assigns the operator to the journey from now (the default start). Reports he makes afterwards are for this journey. */
export async function assignOperator(controller: string, journeyId: string): Promise<void> {
  const assigned = await call(controller, `/api/v1/journeys/${journeyId}/assignments`, { method: 'POST', body: { operatorUserId: OPERATOR_ID, acknowledgeOverlap: true } })
  expect(assigned.status, `The operator was assigned: ${JSON.stringify(assigned.body)}`).toBe(201)
}

export interface OperatorStop {
  serviceStopId: string
  stationCode: string
}

export async function operatorJourney(operator: string): Promise<{ journeyId: string; trainNumber: string; stops: OperatorStop[] }> {
  const journey = await call<{ journeyId: string; trainNumber: string; stops: OperatorStop[] }>(operator, '/api/v1/operator/journey')
  expect(journey.status).toBe(200)
  return journey.body
}

export async function report(operator: string, journeyId: string, body: Record<string, unknown>) {
  return call<{ eventId: string }>(operator, `/api/v1/journeys/${journeyId}/events`, {
    method: 'POST',
    body: { clientEventId: crypto.randomUUID(), occurredAt: new Date().toISOString(), ...body },
  })
}

/** Ends the operator's assignment so the test train does not stay his current journey. Ignores "already ended". */
export async function releaseOperator(controller: string, journeyId: string): Promise<void> {
  const view = await call<{ assignmentId: string; operatorUserId: string; validTo: string | null; endReason: string | null }[]>(controller, `/api/v1/journeys/${journeyId}/assignments`)
  for (const a of view.body ?? []) {
    if (a.operatorUserId === OPERATOR_ID && a.endReason === null) {
      await call(controller, `/api/v1/journeys/${journeyId}/assignments/${a.assignmentId}/end`, { method: 'POST', body: { reason: 'CrewChange' } })
    }
  }
}

/**
 * Runs the test train to its end (Departed, Arrived, Departed, Arrived at the last stop), so it is Completed and leaves the station boards.
 * Without this every run leaves a train on the boards until its time has passed, and after a few runs the boards (20 trains at most) no
 * longer show the next test train. Replies that are not "recorded" are ignored: this is cleanup, never an assertion.
 */
export async function finishJourney(operator: string, journeyId: string, stops: OperatorStop[]): Promise<void> {
  const steps: [string, number][] = [['Departed', 0], ['Arrived', 1], ['Departed', 1], ['Arrived', stops.length - 1]]
  for (const [eventType, index] of steps) {
    const stop = stops[index]
    if (stop) await report(operator, journeyId, { eventType, serviceStopId: stop.serviceStopId })
  }
}
