/** Settings from the environment (Vite `VITE_*`), with the Docker Compose addresses as defaults. No secrets: this is a public client. */
export const config = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080',
  oidcAuthority: import.meta.env.VITE_OIDC_AUTHORITY ?? 'http://localhost:8180/realms/smartrail',
  oidcClientId: import.meta.env.VITE_OIDC_CLIENT_ID ?? 'smartrail-ops-web',
  /** How often both panels are refreshed while the tab is visible (spec 08 rule 7). */
  pollMs: 10_000,
  /** After this long without a successful response the "Not up to date" banner appears (NFR-04). */
  staleAfterMs: 30_000,
} as const
