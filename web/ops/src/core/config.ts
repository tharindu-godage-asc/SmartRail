/**
 * Settings shared by both builds, from the environment (Vite `VITE_*`), with the Docker Compose addresses as defaults. No secrets.
 * The sign-in settings are not here but in features/auth/oidc.ts, so the display-only build carries none of them.
 */
export const config = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080',
  /** How often both panels are refreshed while the tab is visible (spec 08 rule 7). */
  pollMs: 10_000,
  /** How often a station display refreshes (spec 09 rule 10; `VITE_DISPLAY_POLL_SECONDS`, default 10). */
  displayPollMs: Math.max(2, Number(import.meta.env.VITE_DISPLAY_POLL_SECONDS ?? 10) || 10) * 1000,
  /** After this long without a successful response the "Not up to date" banner appears (NFR-04). */
  staleAfterMs: 30_000,
} as const
