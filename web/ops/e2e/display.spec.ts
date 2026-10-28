import { expect, test } from '@playwright/test'

/**
 * Spec 09, section 10.4, against the Docker Compose stack: a platform display and a main board with no sign-in, a delay reported through
 * the API reaching the screen within a poll, and the offline banner. Dev passwords only. Each run reports a delay of "unknown" length
 * and then 240 minutes (the largest), both always accepted after one another, so the test can be repeated without resetting the database.
 */
// The displays run as their own service (docker compose service `display`, port 5174), not on the dev server of the dashboard.
test.use({ baseURL: process.env.DISPLAY_URL ?? 'http://localhost:5174' })

const API = 'http://localhost:8080'
const KEYCLOAK = 'http://localhost:8180/realms/smartrail/protocol/openid-connect/token'

async function nimalToken(): Promise<string> {
  const response = await fetch(KEYCLOAK, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ client_id: 'smartrail-dev-token', grant_type: 'password', username: 'nimal', password: 'nimal-dev-only' }),
  })
  expect(response.ok, 'token for nimal').toBe(true)
  return ((await response.json()) as { access_token: string }).access_token
}

type OperatorJourney = { journeyId: string; trainNumber: string; stops: { stationCode: string; scheduledArrival: string | null }[] }

/** The journey Nimal is assigned to now. It is 8058 on a fresh demo database, but the generated journeys can take its place, so never assume it. */
async function nimalsJourney(token: string): Promise<OperatorJourney> {
  const response = await fetch(`${API}/api/v1/operator/journey`, { headers: { Authorization: `Bearer ${token}` } })
  test.skip(response.status === 404, 'Nimal has no journey now: reset with ./scripts/reset-db.sh --demo (the demo layer assigns him one).')
  expect(response.ok, 'Nimal has a journey now').toBe(true)
  return (await response.json()) as OperatorJourney
}

async function nimalReportsDelay(body: { totalDelayMinutes: number } | { delayUnknown: true }) {
  const token = await nimalToken()
  const journey = await nimalsJourney(token)
  const response = await fetch(`${API}/api/v1/journeys/${journey.journeyId}/events`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      clientEventId: crypto.randomUUID(),
      eventType: 'DelayReported',
      occurredAt: new Date().toISOString(),
      reason: 'Weather',
      ...body,
    }),
  })
  expect([200, 201], `report ${JSON.stringify(body)} answered ${response.status}`).toContain(response.status)
  return journey
}

/** The demo train runs 08:20 to 08:55 Sri Lanka time and can be at most 4 hours late: on a board from 08:00 until about 13:10. */
function demoTrainIsAround(): boolean {
  const hour = Number(new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Colombo', hour: '2-digit', hourCycle: 'h23' }).format(new Date()))
  return hour >= 8 && hour < 13
}

/** Minutes since midnight in Sri Lanka now. */
function colomboMinutesNow(): number {
  const parts = new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Colombo', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(new Date())
  const part = (type: string) => Number(parts.find((p) => p.type === type)?.value)
  return part('hour') * 60 + part('minute')
}

/** The delay that makes a train due at Gampaha in about two minutes (at most 240). At least 10 minutes, so the display says Delayed, not On time. */
function delayThatMakesTheTrainDueSoon(scheduledArrivalMinutes: number): number {
  return Math.min(240, Math.max(10, colomboMinutesNow() - scheduledArrivalMinutes + 2))
}

/** Minutes since midnight in Sri Lanka of an instant. */
function colomboMinutesOf(iso: string): number {
  const parts = new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Colombo', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(new Date(iso))
  const part = (type: string) => Number(parts.find((p) => p.type === type)?.value)
  return part('hour') * 60 + part('minute')
}

const clockTime = (minutes: number) => `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`
const hoursAndMinutes = (minutes: number) => (minutes < 60 ? `${minutes} min` : minutes % 60 === 0 ? `${minutes / 60} h` : `${Math.floor(minutes / 60)} h ${String(minutes % 60).padStart(2, '0')} min`)

test('a delay reported by the operator reaches the platform display within a poll, with no sign-in and no request to Keycloak', async ({ page }) => {
  test.setTimeout(90_000)
  const keycloakRequests: string[] = []
  page.on('request', (request) => {
    if (request.url().includes(':8180')) keycloakRequests.push(request.url())
  })

  await nimalReportsDelay({ delayUnknown: true })
  await page.goto('/display/platform/GPH/1')

  // No sign-in screen, the dark display with the platform and the station in three languages.
  await expect(page.getByText('Platform 1')).toBeVisible()
  await expect(page.getByText('ගම්පහ').first()).toBeVisible()
  await expect(page.getByText('கம்பஹா').first()).toBeVisible()
  // The unknown delay is a status, not an error. 8058 may be the first train or one of the next trains (the demo train 8070 can be ahead of it).
  await expect(page.getByText('Waiting for update').first()).toBeVisible()

  // A delay that puts Nimal's train about two minutes from now, so it is among the six trains the display shows however many other trains
  // the timetable has on platform 1. It is greater than the delay in force (unknown) and at most 240.
  const journey = await nimalsJourney(await nimalToken())
  const scheduled = colomboMinutesOf(journey.stops.find((stop) => stop.stationCode === 'GPH')!.scheduledArrival!)
  // A delay is at most 240 minutes, so a train that was due more than about 4 hours ago cannot be shown as due soon.
  test.skip(colomboMinutesNow() - scheduled > 238, "Nimal's journey passed Gampaha too long ago to be put back on the board by a delay.")
  const delay = delayThatMakesTheTrainDueSoon(scheduled)
  await nimalReportsDelay({ totalDelayMinutes: delay })

  // The scheduled arrival + the delay, within one poll (10 s) and a little more.
  // The first train may show its departure (one minute after the arrival), the trains below show the arrival.
  await expect(page.getByText(new RegExp(`${clockTime(scheduled + delay)}|${clockTime(scheduled + 1 + delay)}`)).first()).toBeVisible({ timeout: 20_000 })
  await expect(page.getByText(`Delayed ${hoursAndMinutes(delay)}`).first()).toBeVisible() // hours and minutes once it is an hour or more (the unit tests cover the 4 h wording)
  await expect(page.getByText('කොළඹ කොටුව').first()).toBeVisible()
  await expect(page.getByText('கொழும்பு கோட்டை').first()).toBeVisible()
  expect(keycloakRequests, 'a display never talks to Keycloak').toEqual([])
})

test('the next trains are listed below the first train, with the following trains among them', async ({ page }) => {
  // What the display should list comes from the board itself (it asks for six trains), not from a fixed train: the timetable has many
  // services on platform 1, so a particular night train is only on screen late in the evening.
  const board = (await (await fetch(`${API}/api/v1/stations/GPH/board?platform=1&count=6`)).json()) as {
    trains: { arrival: { estimated: string | null; scheduled: string | null } | null }[]
  }
  const next = board.trains.slice(1).map((t) => t.arrival?.estimated ?? t.arrival?.scheduled).filter((t): t is string => !!t)
  test.skip(next.length === 0, 'Platform 1 at Gampaha has no later trains today (late in the day, or the demo layer is off): nothing to list below the first train.')

  await page.goto('/display/platform/GPH/1')

  const panel = page.getByRole('region', { name: 'Next trains' })
  await expect(panel).toBeVisible()
  await expect(panel.getByText('அடுத்த ரயில்கள்')).toBeVisible()
  // Three rows are shown at a time and the panel pages every 8 s, so one of the next trains' arrival times appears within a page or two.
  const times = next.map((iso) => new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Colombo', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(new Date(iso)))
  await expect(panel.getByText(new RegExp(times.join('|'))).first()).toBeVisible({ timeout: 20_000 })
  await expect(panel.getByText('Colombo Fort').first()).toBeVisible()
})

test('the main board lists the trains with platform and status, with no sign-in', async ({ page }) => {
  test.skip(!demoTrainIsAround(), 'The demo train is not on a board at this time of day (08:00 to 13:00 Sri Lanka time).')

  await page.goto('/display/board/GPH')

  const row = page.getByRole('listitem').first()
  await expect(row).toBeVisible()
  await expect(row.getByText('Colombo Fort')).toBeVisible()
  await expect(row.getByText('කොළඹ කොටුව')).toBeVisible()
  await expect(row.getByText('1', { exact: true })).toBeVisible() // platform 1
  await expect(row.getByText(/Delayed|Waiting for update|Scheduled|On time/)).toBeVisible()
})

test('platform 2 is empty: it says so, and says what comes next', async ({ page }) => {
  await page.goto('/display/platform/GPH/2')

  // Nothing is planned on platform 2 in the demo, so neither "next" nor "tomorrow" exists.
  await expect(page.getByText('No more services today')).toBeVisible()
  await expect(page.getByText('අද තවත් සේවාවන් නැත')).toBeVisible()
  await expect(page.getByText('இன்று மேலும் சேவைகள் இல்லை')).toBeVisible()
})

test('after 30 seconds without an answer the display says live updates are unavailable, keeps the screen, and recovers by itself', async ({ page }) => {
  test.setTimeout(120_000)
  await page.goto('/display/platform/GPH/1')
  await expect(page.getByText('Platform 1')).toBeVisible()
  await expect(page.getByText(/Updated \d\d:\d\d/)).toBeVisible()
  const shownBefore = await page.locator('main').innerText()

  await page.route('**/api/v1/stations/**', (route) => route.abort())
  const banner = page.getByRole('status').filter({ hasText: 'Live updates unavailable' })
  await expect(banner).toBeVisible({ timeout: 50_000 })
  await expect(banner.getByText('සජීවී යාවත්කාලීන නොමැත')).toBeVisible()
  await expect(banner.getByText('நேரடி புதுப்பிப்புகள் இல்லை')).toBeVisible()
  await expect(banner.getByText(/Last update \d\d:\d\d/)).toBeVisible()
  expect(await page.locator('main').innerText()).toContain((shownBefore.split('\n').find((line) => line.startsWith('Train ')) ?? 'Platform'))

  await page.unroute('**/api/v1/stations/**')
  await expect(banner).toHaveCount(0, { timeout: 30_000 })
})

test('a station or platform that does not exist is not set up, and a malformed address makes no request', async ({ page }) => {
  await page.goto('/display/platform/XXX/1')
  await expect(page.getByText('This display is not set up')).toBeVisible()

  const requests: string[] = []
  page.on('request', (request) => requests.push(request.url()))
  await page.goto('/display/platform/%3Cscript%3E/1')
  await expect(page.getByText('This display is not set up')).toBeVisible()
  expect(requests.filter((url) => url.includes('/api/v1/stations'))).toEqual([])
})
