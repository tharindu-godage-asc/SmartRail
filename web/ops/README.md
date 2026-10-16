# SmartRail operations web app (controller dashboard)

React + TypeScript + Tailwind CSS. Spec: `_specs/08-controller-web.md`. It uses three controller endpoints of the API
(`GET /api/v1/exceptions`, `POST /api/v1/exceptions/{id}/resolve`, `GET /api/v1/overview`); the API decides who may read anything.

## Run it

1. Start the stack: `docker compose up --build` in `smartrail-platform` (API on 8080, Keycloak on 8180).
2. `npm install`, then `npm run dev`. The app is on http://localhost:5173 (the port is fixed: it is the redirect address of the Keycloak client `smartrail-ops-web`).
3. Sign in as `jayasinghe` (controller, password `jayasinghe-dev-only`). `nimal` (operator) sees "This account cannot open the dashboard".

Settings are optional (`.env.example`): `VITE_API_BASE_URL`, `VITE_OIDC_AUTHORITY`, `VITE_OIDC_CLIENT_ID`. The API must have
`Cors:AllowedOrigins` set to `http://localhost:5173`; Docker Compose does that.

## Station displays (no sign-in)

- They are their own service, so the controller dashboard can be down without touching them. Docker Compose runs it as `display` on **http://localhost:5174**: platform display `/display/platform/GPH/1` (station code, platform), main board `/display/board/GPH`. The same pages also work on the dev server (5173), but that is only for development.
- The display service is `npm run build:display` (a bundle with no sign-in code) served by nginx (`Dockerfile.display`, `nginx.display.conf`). The API address is baked in at build time (`--build-arg VITE_API_BASE_URL`).
- Station codes: `CF`, `MDA`, `RGM`, `GPH`, `VYG`. Platforms `1` and `2`; the demo trains use platform 1.
- They read `GET /api/v1/stations/{code}/board` every 10 seconds (`VITE_DISPLAY_POLL_SECONDS`), never send a token, and say "Live updates unavailable" after 30 seconds without an answer.

## Commands

| Command | What it does |
|---|---|
| `npm run tokens` | Regenerates `src/styles/tokens.css` from `mobile/operator_app/smartrail-tokens.json` (CSS variables and the Tailwind theme). Run it after the token file changes; CI fails if the committed file is out of date. |
| `npm run lint` / `npm run typecheck` / `npm test` / `npm run build` / `npm run build:display` | The checks the `web` workflow runs (it also checks the display bundle has no sign-in code and that the display image builds). |
| `npm run e2e` | Playwright against the running Compose stack (not in CI). It reports a delay as Nimal and resolves it as Jayasinghe (`controller-flow.spec.ts`), and checks the station displays (`display.spec.ts`, against the `display` container on port 5174; `DISPLAY_ONLY=1` skips the dashboard's dev server). The overview step and the display steps that need the demo train run only 08:00 to 13:00 Sri Lanka time, while it is on a board. |

## Folders

- `src/core`: API client, strings (every word the app shows), time formats, freshness rule, shared UI parts.
- `src/features/auth`: sign in with Authorization Code + PKCE (tokens in memory only), role gate, sign-out screens.
- `src/features/exceptions`, `src/features/overview`: the two screens, their polled reads.
- `src/modes/ops`: the operations layout (safety notice, header, menu). A later `display` mode would be a sibling folder (D-23).
- `e2e`: the Playwright test. `scripts`: the token generator and its test.
