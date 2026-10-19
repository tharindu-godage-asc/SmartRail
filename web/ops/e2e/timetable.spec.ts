import { expect, test, type Page } from '@playwright/test'

/**
 * Spec 15, section 9 (Playwright criterion): Jayasinghe schedules a new weekly service, finds it in the list, cancels one date and finds
 * it under Cancelled. Run by hand against the Compose stack, like controller-flow.spec.ts (dev passwords only). The service is created
 * through the API with a random train number, so the test can be run again and again without resetting the database; everything after
 * that goes through the screens.
 */
const API = 'http://localhost:8080'
const KEYCLOAK = 'http://localhost:8180/realms/smartrail/protocol/openid-connect/token'

async function controllerToken(): Promise<string> {
  const response = await fetch(KEYCLOAK, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ client_id: 'smartrail-dev-token', grant_type: 'password', username: 'jayasinghe', password: 'jayasinghe-dev-only' }),
  })
  expect(response.ok, 'token for jayasinghe').toBe(true)
  return ((await response.json()) as { access_token: string }).access_token
}

/** A date in Sri Lanka, `plus` days from today, as YYYY-MM-DD. */
function colomboDay(plus: number): string {
  const day = new Date(Date.now() + plus * 86_400_000)
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Colombo' }).format(day)
}

async function signIn(page: Page) {
  await page.goto('/')
  await page.getByRole('button', { name: 'Sign in with staff account' }).click()
  await page.locator('#username').fill('jayasinghe')
  await page.locator('#password').fill('jayasinghe-dev-only')
  await page.locator('#kc-login').click()
}

test('a controller schedules a service, finds it in the list, cancels one date and finds it under Cancelled', async ({ page }) => {
  const trainNumber = `9${Math.floor(100 + Math.random() * 900)}`
  const token = await controllerToken()
  const created = await fetch(`${API}/api/v1/timetable/services`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      kind: 'Weekly',
      trainNumber,
      lineCode: 'MAIN',
      category: 'Regional',
      serviceType: 'Slow',
      days: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
      effectiveFrom: colomboDay(2),
      stops: [
        { stationCode: 'VYG', arrival: null, departure: '21:10', platform: null },
        { stationCode: 'GPH', arrival: '21:30', departure: '21:31', platform: null },
        { stationCode: 'CF', arrival: '21:50', departure: null, platform: null },
      ],
    }),
  })
  expect(created.status, 'the service was created (a 409 means the random train number is in use: run again)').toBe(201)

  await signIn(page)
  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: /^Timetable/ }).click()
  await expect(page.getByRole('heading', { name: 'Timetable' })).toBeVisible()

  // It is in the weekly list, and its detail has the Cancel button.
  const row = page.getByRole('row', { name: new RegExp(trainNumber) })
  await expect(row).toBeVisible()
  await row.getByRole('link', { name: new RegExp(trainNumber) }).click()
  await expect(page.getByText('A service is never deleted. A change creates a new version.')).toBeVisible()
  await page.screenshot({ path: 'test-results/timetable-1-service-detail.png', fullPage: true })
  await page.getByRole('button', { name: 'Cancel service' }).click()

  // Cancel one date, with a reason. The dialog says plainly that passengers are not messaged.
  const dialog = page.getByRole('dialog')
  await expect(dialog).toContainText('Messages to passengers are not sent yet')
  await dialog.getByRole('radio', { name: 'This date only' }).check()
  await dialog.locator('#cancel-date').fill(colomboDay(4))
  await dialog.getByRole('textbox', { name: 'Reason' }).fill('Playwright test: no crew')
  await page.screenshot({ path: 'test-results/timetable-2-cancel-dialog.png' })
  await dialog.getByRole('button', { name: 'Confirm cancellation' }).click()
  await expect(dialog).toBeHidden() // success closes the dialog; the detail then shows the result
  await expect(page.getByText(/^Cancelled \d+ journeys?\.|^Cancelled\. No journey existed/)).toBeVisible()

  // It is under Cancelled with the reason; the service itself is still in the weekly list (nothing is deleted).
  await page.getByRole('link', { name: 'Back to the timetable' }).click()
  await page.getByRole('navigation', { name: 'Timetable sections' }).getByRole('link', { name: /^Cancelled/ }).click()
  const cancelled = page.getByRole('row', { name: new RegExp(trainNumber) })
  await expect(cancelled).toBeVisible()
  await expect(cancelled).toContainText('Playwright test: no crew')
  await page.screenshot({ path: 'test-results/timetable-3-cancelled-tab.png', fullPage: true })

  await page.getByRole('navigation', { name: 'Timetable sections' }).getByRole('link', { name: /^Weekly schedule/ }).click()
  await expect(page.getByRole('row', { name: new RegExp(trainNumber) })).toBeVisible()
})
