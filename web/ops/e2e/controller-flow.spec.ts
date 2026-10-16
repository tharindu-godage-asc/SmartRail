import { expect, test, type Page } from '@playwright/test'

/**
 * Spec 08, section 9: Nimal reports a delay through the API, Jayasinghe signs in on the web app, sees the Critical row, resolves it,
 * and the row is gone. Also: Nimal on the web app sees "No dashboard access" and the API refuses his token.
 * Dev passwords only (docker/keycloak/realm-smartrail.json). The demo journey 8058 is used; each run reports a delay of "unknown"
 * length, which is always accepted, so the test can be run again and again without resetting the database.
 */
const API = 'http://localhost:8080'
const KEYCLOAK = 'http://localhost:8180/realms/smartrail/protocol/openid-connect/token'
const password = (user: string) => `${user}-dev-only`

async function tokenFor(user: string): Promise<string> {
  const response = await fetch(KEYCLOAK, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ client_id: 'smartrail-dev-token', grant_type: 'password', username: user, password: password(user) }),
  })
  expect(response.ok, `token for ${user}`).toBe(true)
  return ((await response.json()) as { access_token: string }).access_token
}

async function api<T>(token: string, path: string, init: { method?: string; body?: unknown } = {}): Promise<{ status: number; body: T }> {
  const response = await fetch(`${API}${path}`, {
    method: init.method ?? 'GET',
    headers: { Authorization: `Bearer ${token}`, ...(init.body ? { 'Content-Type': 'application/json' } : {}) },
    body: init.body ? JSON.stringify(init.body) : undefined,
  })
  const text = await response.text()
  return { status: response.status, body: (text ? JSON.parse(text) : null) as T }
}

async function signInOnKeycloak(page: Page, user: string) {
  await page.goto('/')
  await page.getByRole('button', { name: 'Sign in with staff account' }).click()
  await page.locator('#username').fill(user)
  await page.locator('#password').fill(password(user))
  await page.locator('#kc-login').click()
}

/** Nimal reports "delay, technical fault, length unknown" on his journey now. Returns the event ID. */
async function nimalReportsATechnicalFault(): Promise<string> {
  const nimal = await tokenFor('nimal')
  const journey = await api<{ journeyId: string }>(nimal, '/api/v1/operator/journey')
  expect(journey.status).toBe(200)

  const report = await api<{ eventId: string }>(nimal, `/api/v1/journeys/${journey.body.journeyId}/events`, {
    method: 'POST',
    body: {
      clientEventId: crypto.randomUUID(),
      eventType: 'DelayReported',
      occurredAt: new Date().toISOString(),
      reason: 'TechnicalFault',
      delayUnknown: true,
    },
  })
  expect(report.status).toBe(201)
  return report.body.eventId
}

test('a Technical fault report reaches the controller, who resolves it', async ({ page }) => {
  const eventId = await nimalReportsATechnicalFault()
  const jayasinghe = await tokenFor('jayasinghe')
  const queue = await api<{ items: { id: string; relatedEventId: string }[] }>(jayasinghe, '/api/v1/exceptions?status=open')
  const created = queue.body.items.find((item) => item.relatedEventId === eventId)
  expect(created, 'the API raised an exception for the report').toBeDefined()

  await signInOnKeycloak(page, 'jayasinghe')

  // The safety notice is on the screen, and the controller's name and role are in the header.
  await expect(page.getByText(/information system, not a safety system/)).toBeVisible()
  await expect(page.getByText(/jayasinghe · Controller/)).toBeVisible()

  // The Critical row (icon and word) is in the queue.
  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: /^Exceptions/ }).click()
  const row = page.locator(`tr[data-exception-id="${created!.id}"]`)
  await expect(row).toBeVisible()
  await expect(row.getByText('Critical')).toBeVisible()
  await expect(row.getByText('Delay reason waiting for verification')).toBeVisible()
  await expect(row.getByText(/^8058/)).toBeVisible()

  // Resolve with the three-step flow: select, choose, confirm. One deliberate confirm.
  await row.getByRole('button', { name: /Delay reason waiting for verification/ }).click()
  await page.getByRole('button', { name: 'Acknowledge', exact: true }).click()
  const dialog = page.getByRole('dialog')
  await expect(dialog).toContainText('Acknowledged')
  await dialog.getByRole('button', { name: 'Confirm' }).click()

  await expect(row).toHaveCount(0)
  const after = await api<{ items: { id: string }[] }>(jayasinghe, '/api/v1/exceptions?status=open')
  expect(after.body.items.some((item) => item.id === created!.id)).toBe(false)

  // It is now on the Closed tab, as a card with the status, the action, who closed it and when (spec 08 section 14).
  await page.getByRole('navigation', { name: 'Exceptions views' }).getByRole('link', { name: 'Closed' }).click()
  const card = page.locator(`article[data-exception-id="${created!.id}"]`)
  await expect(card).toBeVisible()
  await expect(card.getByText('Closed · Acknowledged')).toBeVisible()
  await expect(card.getByText('Critical')).toBeVisible()
  await expect(card.getByText(/8058/)).toBeVisible()
  await expect(card.getByText('jayasinghe', { exact: true })).toBeVisible() // the name stored at resolve time, not a UUID
  await expect(card.getByText(/after it was raised/)).toBeVisible()

  // Nothing in the browser storage holds a token (spec 08 rule 12).
  const stored = await page.evaluate(() => JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage }))
  expect(stored).not.toMatch(/eyJ[A-Za-z0-9_-]{10,}\./)
})

test('the overview lists train 8058 with the delay shown as unknown (only while the demo train is in progress)', async ({ page }) => {
  // The demo journey runs 08:20 to 08:55 Sri Lanka time and can be at most 4 hours late, so it is "in progress" only until about 13:10.
  const hour = Number(new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Colombo', hour: '2-digit', hourCycle: 'h23' }).format(new Date()))
  test.skip(hour < 8 || hour >= 13, 'The demo train is not in progress at this time of day; the overview is covered by the API tests.')

  await nimalReportsATechnicalFault()
  await signInOnKeycloak(page, 'jayasinghe')

  const row = page.locator('tr', { hasText: 'Veyangoda → Colombo Fort' })
  await expect(row).toBeVisible()
  await expect(row.getByText('Delay unknown')).toBeVisible()
  await expect(row.getByText('Awaiting verification')).toBeVisible()
  await expect(row.getByText(/Operator report/)).toBeVisible()
})

test('Nimal, an operator, sees "No dashboard access" and the API refuses his token', async ({ page }) => {
  await signInOnKeycloak(page, 'nimal')

  await expect(page.getByRole('heading', { name: 'This account cannot open the dashboard' })).toBeVisible()
  await expect(page.getByText('nimal', { exact: true })).toBeVisible()
  await expect(page.getByText('Open exceptions')).toHaveCount(0)

  const nimal = await tokenFor('nimal')
  expect((await api(nimal, '/api/v1/exceptions?status=open')).status).toBe(403)
  expect((await api(nimal, '/api/v1/overview')).status).toBe(403)
})

test('Sign out ends the session and returns to the sign-in screen', async ({ page }) => {
  await signInOnKeycloak(page, 'jayasinghe')
  await expect(page.getByText(/jayasinghe · Controller/)).toBeVisible()

  await page.getByRole('button', { name: 'Sign out' }).click()

  await expect(page.getByRole('button', { name: 'Sign in with staff account' })).toBeVisible()
})
