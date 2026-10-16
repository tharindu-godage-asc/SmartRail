# SmartRail operations web app (controller dashboard)

React + TypeScript + Tailwind CSS. Spec: `_specs/08-controller-web.md`. It uses three controller endpoints of the API
(`GET /api/v1/exceptions`, `POST /api/v1/exceptions/{id}/resolve`, `GET /api/v1/overview`); the API decides who may read anything.

## Run it

1. Start the stack: `docker compose up --build` in `smartrail-platform` (API on 8080, Keycloak on 8180).
2. `npm install`, then `npm run dev`. The app is on http://localhost:5173 (the port is fixed: it is the redirect address of the Keycloak client `smartrail-ops-web`).
3. Sign in as `jayasinghe` (controller, password `jayasinghe-dev-only`). `nimal` (operator) sees "This account cannot open the dashboard".

Settings are optional (`.env.example`): `VITE_API_BASE_URL`, `VITE_OIDC_AUTHORITY`, `VITE_OIDC_CLIENT_ID`. The API must have
`Cors:AllowedOrigins` set to `http://localhost:5173`; Docker Compose does that.

## Commands

| Command | What it does |
|---|---|
| `npm run tokens` | Regenerates `src/styles/tokens.css` from `mobile/operator_app/smartrail-tokens.json` (CSS variables and the Tailwind theme). Run it after the token file changes; CI fails if the committed file is out of date. |
| `npm run lint` / `npm run typecheck` / `npm test` / `npm run build` | The checks the `web` workflow runs. |
| `npm run e2e` | Playwright against the running Compose stack (not in CI). It reports a delay as Nimal and resolves it as Jayasinghe. The overview step runs only 08:20 to 13:00 Sri Lanka time, while the demo train is in progress. |

## Folders

- `src/core`: API client, strings (every word the app shows), time formats, freshness rule, shared UI parts.
- `src/features/auth`: sign in with Authorization Code + PKCE (tokens in memory only), role gate, sign-out screens.
- `src/features/exceptions`, `src/features/overview`: the two screens, their polled reads.
- `src/modes/ops`: the operations layout (safety notice, header, menu). A later `display` mode would be a sibling folder (D-23).
- `e2e`: the Playwright test. `scripts`: the token generator and its test.
