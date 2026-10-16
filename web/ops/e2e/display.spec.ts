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

async function nimalReportsDelay(body: { totalDelayMinutes: number } | { delayUnknown: true }) {
  const token = await nimalToken()
  const journey = (await (await fetch(`${API}/api/v1/operator/journey`, { headers: { Authorization: `Bearer ${token}` } })).json()) as { journeyId: string }
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
}

/** The demo train runs 08:20 to 08:55 Sri Lanka time and can be at most 4 hours late: on a board from 08:00 until about 13:10. */
function demoTrainIsAround(): boolean {
  const hour = Number(new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Colombo', hour: '2-digit', hourCycle: 'h23' }).format(new Date()))
  return hour >= 8 && hour < 13
}

test('a delay reported by the operator reaches the platform display within a poll, with no sign-in and no request to Keycloak', async ({ page }) => {
  test.skip(!demoTrainIsAround(), 'The demo train is not on a board at this time of day (08:00 to 13:00 Sri Lanka time).')
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

  await nimalReportsDelay({ totalDelayMinutes: 240 })

  // 08:30 scheduled + 240 min = 12:30, within one poll (10 s) and a little more. 8058 is the first train, or one of the next trains below it.
  await expect(page.getByText('12:30').first()).toBeVisible({ timeout: 20_000 })
  await expect(page.getByText('Delayed 4 h').first()).toBeVisible() // hours, not 240 minutes
  await expect(page.getByText('කොළඹ කොටුව').first()).toBeVisible()
  await expect(page.getByText('கொழும்பு கோட்டை').first()).toBeVisible()
  expect(keycloakRequests, 'a display never talks to Keycloak').toEqual([])
})

test('the next trains are listed below the first train, with the night train among them', async ({ page }) => {
  await page.goto('/display/platform/GPH/1')

  const panel = page.getByRole('region', { name: 'Next trains' })
  await expect(panel).toBeVisible()
  await expect(panel.getByText('அடுத்த ரயில்கள்')).toBeVisible()
  // The night train 8075 reaches Gampaha at 22:20. It is one of the first six trains until late in the evening.
  await expect(panel.getByText('22:20').first()).toBeVisible({ timeout: 20_000 })
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
