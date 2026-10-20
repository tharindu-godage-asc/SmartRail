import { expect, test, type Page } from '@playwright/test'

/**
 * Spec 16 F2: Nimal reports a delay of unknown length with the reason "Unknown" through the API; passengers see no reason. Jayasinghe
 * opens the exception, sees that "Unknown" is locked, edits it to "Signal issue" and publishes it; the public status then carries the
 * reason and the time. Run by hand against the Compose stack (dev passwords only), like controller-flow.spec.ts. A delay of unknown
 * length is always accepted, so the test can be run again and again.
 */
const API = 'http://localhost:8080'
const KEYCLOAK = 'http://localhost:8180/realms/smartrail/protocol/openid-connect/token'

async function tokenFor(user: string): Promise<string> {
  const response = await fetch(KEYCLOAK, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ client_id: 'smartrail-dev-token', grant_type: 'password', username: user, password: `${user}-dev-only` }),
  })
  expect(response.ok, `token for ${user}`).toBe(true)
  return ((await response.json()) as { access_token: string }).access_token
}

async function getJson<T>(token: string | null, path: string): Promise<T> {
  const response = await fetch(`${API}${path}`, { headers: token ? { Authorization: `Bearer ${token}` } : {} })
  expect(response.status).toBe(200)
  return (await response.json()) as T
}

async function signIn(page: Page) {
  await page.goto('/')
  await page.getByRole('button', { name: 'Sign in with staff account' }).click()
  await page.locator('#username').fill('jayasinghe')
  await page.locator('#password').fill('jayasinghe-dev-only')
  await page.locator('#kc-login').click()
}

test.skip(true, 'Superseded by flow 2 in critical-flows.spec.ts (this one needs the demo journey 8058 to be in progress).')

test('a controller edits an unapproved reason and publishes it; passengers then see it with the time', async ({ page }) => {
  const nimal = await tokenFor('nimal')
  const journey = await getJson<{ journeyId: string }>(nimal, '/api/v1/operator/journey')
  const report = await fetch(`${API}/api/v1/journeys/${journey.journeyId}/events`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${nimal}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({ clientEventId: crypto.randomUUID(), eventType: 'DelayReported', occurredAt: new Date().toISOString(), reason: 'Unknown', delayUnknown: true }),
  })
  expect(report.status).toBe(201)
  const eventId = ((await report.json()) as { eventId: string }).eventId

  // Before verification the public status has no reason at all.
  const before = await getJson<Record<string, unknown>>(null, `/api/v1/journeys/${journey.journeyId}/status`)
  expect(before.reason).toBeUndefined()

  const jayasinghe = await tokenFor('jayasinghe')
  const queue = await getJson<{ items: { id: string; relatedEventId: string }[] }>(jayasinghe, '/api/v1/exceptions?status=open')
  const created = queue.items.find((item) => item.relatedEventId === eventId)
  expect(created, 'the report raised an exception').toBeDefined()

  await signIn(page)
  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: /^Exceptions/ }).click()
  const row = page.locator(`tr[data-exception-id="${created!.id}"]`)
  await row.getByRole('button', { name: /Delay reason waiting for verification/ }).click()

  // "Unknown" cannot be published as it is: the button is locked and the reason is explained.
  await expect(page.getByText('Reported by the operator: Unknown')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Verify and publish reason' })).toBeDisabled()
  await expect(page.getByText(/cannot be published as reported/)).toBeVisible()

  await page.getByRole('button', { name: 'Edit reason' }).click()
  await page.getByLabel('Approved reason').selectOption('SignalIssue')
  await page.getByRole('button', { name: 'Publish this reason' }).click()
  await expect(row).toHaveCount(0)

  const after = await getJson<{ reason?: { code: string; verifiedAt: string } }>(null, `/api/v1/journeys/${journey.journeyId}/status`)
  expect(after.reason?.code).toBe('SignalIssue')
  expect(Date.parse(after.reason?.verifiedAt ?? '')).not.toBeNaN()
})
