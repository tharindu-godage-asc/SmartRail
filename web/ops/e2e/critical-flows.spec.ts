import { expect, test, type Page } from '@playwright/test'
import {
  API,
  assignOperator,
  call,
  createTrainToday,
  findJourneyId,
  finishJourney,
  OPERATOR_NAME,
  OPERATOR_USER,
  operatorJourney,
  releaseOperator,
  report,
  SMALLBITES,
  tokenFor,
  tooLateToday,
} from './flow-helpers.js'

/**
 * The five critical end-to-end flows of the assessment (QA section of the brief), run by hand against the Compose stack:
 *   1. Live chain: an operator's reports reach the public status and a station board (NFR-04).
 *   2. Verify a delay reason: the controller verifies in the web app and the passenger status then carries the reason (FR-52, D-10).
 *   3. Assignment and lost device: the controller ends an assignment in the web app and the operator is refused (FR-12, FR-56).
 *   4. Controller resolves a fault report: a Technical fault delay report reaches the exceptions queue and the controller resolves it
 *      (controller-flow.spec.ts, spec 08 and spec 07). The timetable flow (timetable.spec.ts, spec 15) is an extra check, no longer one of the five.
 *   5. Catering: an item sold out at SmallBites shows as sold out in SmartRail's menu read (FR-90, D-72) and the caterer cannot read operations data.
 * Each flow makes its own train for today, so the tests repeat without a database reset. Dev passwords only.
 */
test.skip(tooLateToday(), 'A train made for today cannot pass midnight: run these between 01:00 and 23:00 Sri Lanka time.')

async function signInAsController(page: Page) {
  await page.goto('/')
  await page.getByRole('button', { name: 'Sign in with staff account' }).click()
  await page.locator('#username').fill('jayasinghe')
  await page.locator('#password').fill('jayasinghe-dev-only')
  await page.locator('#kc-login').click()
}

/** A new train with the operator assigned. Returns what the flow needs and releases the operator at the end. */
async function trainWithOperator() {
  const controller = await tokenFor('jayasinghe')
  const operator = await tokenFor(OPERATOR_USER)
  const trainNumber = await createTrainToday(controller)
  const journeyId = await findJourneyId(controller, trainNumber)
  await assignOperator(controller, journeyId)
  const journey = await operatorJourney(operator)
  expect(journey.journeyId, 'the newest assignment is this train').toBe(journeyId)
  return { controller, operator, trainNumber, journeyId, stops: journey.stops }
}

test('flow 1: operator reports reach the public status and a station board', async () => {
  const { controller, operator, trainNumber, journeyId, stops } = await trainWithOperator()
  try {
    // Before any report the public status shows no delay.
    const before = await call<{ delayMinutes: number }>(null, `/api/v1/journeys/${journeyId}/status`)
    expect(before.status).toBe(200)
    expect(before.body.delayMinutes).toBe(0)

    expect((await report(operator, journeyId, { eventType: 'Departed', serviceStopId: stops[0]!.serviceStopId })).status).toBe(201)
    expect((await report(operator, journeyId, { eventType: 'DelayReported', reason: 'SignalIssue', totalDelayMinutes: 12 })).status).toBe(201)

    // The passenger's status, the train lookup and the board of the next station all show it, with no delay reason (D-10).
    const status = await call<{ delayMinutes: number; statusKey: string; lastKnownPosition: { stationCode: string; state: string } | null }>(null, `/api/v1/journeys/${journeyId}/status`)
    expect(status.body.delayMinutes).toBe(12)
    expect(status.body.statusKey).toBe('Delayed')
    expect(status.body.lastKnownPosition).toMatchObject({ stationCode: 'VYG', state: 'Departed' })
    expect(JSON.stringify(status.body)).not.toContain('SignalIssue')

    // The board of Gampaha lists at most 20 trains and earlier runs leave trains on it, so the test train is put on platform 2 (empty in the
    // reference data) and the board is read for that platform: it also proves a controller's platform change reaches the board.
    const platform = await call(controller, `/api/v1/journeys/${journeyId}/stops/${stops[1]!.serviceStopId}/platform`, { method: 'POST', body: { platformCode: '2', acknowledgeWarnings: true } })
    expect(platform.status, JSON.stringify(platform.body)).toBe(200)
    const board = await call<{ trains: { trainNumber: string; platform: string | null; delayMinutes: number | null }[] }>(null, '/api/v1/stations/GPH/board?platform=2&count=20')
    const onBoard = board.body.trains.find((t) => t.trainNumber === trainNumber)
    expect(onBoard?.platform).toBe('2')
    expect(onBoard?.delayMinutes).toBe(12)

    // An arrival at Gampaha (an in-between stop since spec 19) is accepted and moves the train there.
    expect((await report(operator, journeyId, { eventType: 'Arrived', serviceStopId: stops[1]!.serviceStopId })).status).toBe(201)
    const arrived = await call<{ lastKnownPosition: { stationCode: string; state: string } }>(null, `/api/v1/journeys/${journeyId}/status`)
    expect(arrived.body.lastKnownPosition).toMatchObject({ stationCode: 'GPH', state: 'Arrived' })
  } finally {
    await finishJourney(operator, journeyId, stops)
    await releaseOperator(controller, journeyId)
  }
})

test('flow 2: a controller verifies a delay reason and passengers then see it', async ({ page }) => {
  const { controller, operator, journeyId, stops } = await trainWithOperator()
  try {
    await report(operator, journeyId, { eventType: 'Departed', serviceStopId: stops[0]!.serviceStopId })
    const delay = await report(operator, journeyId, { eventType: 'DelayReported', reason: 'Weather', totalDelayMinutes: 9 })
    expect(delay.status).toBe(201)

    const before = await call<Record<string, unknown>>(null, `/api/v1/journeys/${journeyId}/status`)
    expect(before.body.reason).toBeUndefined() // not public until verified

    const queue = await call<{ items: { id: string; relatedEventId: string }[] }>(controller, '/api/v1/exceptions?status=open')
    const exception = queue.body.items.find((i: { id: string; relatedEventId: string }) => i.relatedEventId === delay.body.eventId)
    expect(exception, 'the delay raised an exception').toBeDefined()

    await signInAsController(page)
    await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: /^Exceptions/ }).click()
    const row = page.locator(`tr[data-exception-id="${exception!.id}"]`)
    await row.getByRole('button', { name: /Delay reason waiting for verification/ }).click()
    await expect(page.getByText('Reported by the operator: Weather')).toBeVisible()
    await page.getByRole('button', { name: 'Verify and publish reason' }).click()
    await expect(row).toHaveCount(0)

    const after = await call<{ reason?: { code: string; verifiedAt: string } }>(null, `/api/v1/journeys/${journeyId}/status`)
    expect(after.body.reason?.code).toBe('Weather')
    expect(Date.parse(after.body.reason?.verifiedAt ?? '')).not.toBeNaN()
  } finally {
    await finishJourney(operator, journeyId, stops)
    await releaseOperator(controller, journeyId)
  }
})

test('flow 3: ending an assignment as a lost device refuses the operator', async ({ page }) => {
  const { controller, operator, trainNumber, journeyId, stops } = await trainWithOperator()
  try {
    expect((await report(operator, journeyId, { eventType: 'Departed', serviceStopId: stops[0]!.serviceStopId })).status).toBe(201)
    const firstReportAt = Date.now()

    await signInAsController(page)
    await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Journeys' }).click()
    await page.getByRole('link', { name: /^All journeys/ }).click()
    const row = page.getByRole('row', { name: new RegExp(trainNumber) })
    await expect(row).toContainText(OPERATOR_NAME)
    await row.getByRole('link', { name: 'Crew and control' }).click()

    await page.getByRole('button', { name: /^End assignment/ }).click()
    const dialog = page.getByRole('dialog')
    await dialog.getByRole('radio', { name: 'Lost or stolen device' }).check()
    await dialog.getByRole('button', { name: 'End assignment' }).click()
    await expect(page.getByText('Assignment ended.')).toBeVisible()

    // From now a report from the operator for this journey is refused, even one that claims an earlier time (FR-56).
    const late = await report(operator, journeyId, { eventType: 'Departed', serviceStopId: stops[1]!.serviceStopId, occurredAt: new Date(firstReportAt + 1_000).toISOString() }) // inside the assignment, received after it ended
    expect(late.status).toBe(403)
    expect((late.body as unknown as { reason: string }).reason).toBe('assignment_ended')
    // The report he made before stays in the record.
    const detail = await call<{ events: { type: string }[] }>(controller, `/api/v1/controller/journeys/${journeyId}`)
    expect(detail.body.events.some((e: { type: string }) => e.type === 'Departed')).toBe(true)
  } finally {
    await releaseOperator(controller, journeyId)
  }
})

test('flow 5: an item sold out at the caterer shows as sold out in SmartRail, and the caterer cannot read operations', async () => {
  const controller = await tokenFor('jayasinghe')
  const trainNumber = await createTrainToday(controller)
  const menuPath = `/api/v1/trains/${trainNumber}/menu?from=VYG&to=CF`
  const itemOf = (body: { stations: { items: { itemId: string; available: boolean }[] }[] }) => body.stations[0]!.items.find((i) => i.itemId === 'egg-sandwich')!

  const first = await call<{ stations: { items: { itemId: string; available: boolean }[] }[] }>(null, menuPath)
  expect(first.status).toBe(200)
  expect(itemOf(first.body).available).toBe(true)

  try {
    // The caterer marks the item sold out on its own system.
    const toggled = await fetch(`${SMALLBITES}/admin/items/egg-sandwich/available?value=false`, { method: 'POST', redirect: 'manual' })
    expect(toggled.status).toBeLessThan(400)

    // SmartRail pulls the menu again within its refetch interval (15 s).
    await expect
      .poll(async () => itemOf((await call<{ stations: { items: { itemId: string; available: boolean }[] }[] }>(null, menuPath)).body).available, { timeout: 45_000, intervals: [3_000] })
      .toBe(false)
  } finally {
    await fetch(`${SMALLBITES}/admin/items/egg-sandwich/available?value=true`, { method: 'POST', redirect: 'manual' })
  }

  // The caterer's own token cannot read operational data (D-72: SmartRail sends it nothing).
  const caterer = await fetch('http://localhost:8180/realms/smartrail/protocol/openid-connect/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ client_id: 'smartrail-caterer', client_secret: 'smartrail-caterer-dev-only', grant_type: 'client_credentials' }),
  })
  if (caterer.ok) {
    const token = ((await caterer.json()) as { access_token: string }).access_token
    expect((await call(token, '/api/v1/exceptions?status=open')).status).toBe(403)
    expect((await call(token, '/api/v1/overview')).status).toBe(403)
  }
  expect((await call(null, '/api/v1/exceptions?status=open')).status).toBe(401)
  void API
})
