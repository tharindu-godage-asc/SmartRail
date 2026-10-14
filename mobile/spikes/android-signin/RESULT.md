# Android sign-in spike: result

Spec: [_specs/android-signin-spike.md](../../../_specs/android-signin-spike.md). Run on 14 Oct 2026, Android emulator `Pixel_6`, Flutter 3.47.2, `flutter_appauth` 12.1.0, Keycloak 26.7.5 in Docker Compose.

## Decision: GO

Sign-in as an operator works on the Android emulator with Authorization Code + PKCE through a Chrome Custom Tab. The signed-in call to the API returned 200, and one refresh worked. The fallback (password grant) was not needed and was not demonstrated.

**One condition for the operator app:** if Android kills the app while the user is on Keycloak's login page, the sign-in result is lost and the app restarts signed out (see finding 5). The operator app must handle that case.

## Time box

| | |
|---|---|
| Start | 09:29 (first command after approval) |
| Stop | 10:25 |
| Elapsed | 56 minutes, inside the 4 hour box |

Where the time went: about 15 minutes on Docker problems unrelated to Android (finding 9), a 5 minute 27 second first Gradle build (36 seconds for later builds), and about 25 minutes finding the root cause of the lost sign-in result (finding 1).

## Acceptance criteria and evidence

| Criterion | Result | Evidence |
|---|---|---|
| App builds and installs on the emulator | Pass | `flutter build apk --debug`, `adb install` |
| Nimal signs in through Keycloak's page with the code flow and PKCE and returns to the app | Pass | [01](screenshots/01-keycloak-login-page.png), [02](screenshots/02-signed-in.png). The run took 14 seconds from tapping Sign in to the signed-in screen |
| App shows subject, role operator, expiry about 15 minutes ahead | Pass | [02](screenshots/02-signed-in.png): `d0d0d0d0-0000-4000-8000-000000000001`, `[operator]`, expires 10:35 after signing in at 10:20 |
| `GET /api/v1/whoami` returns 200 with that subject and role | Pass | [03](screenshots/03-whoami-200-after-refresh.png) |
| One refresh returns a new expiry, and whoami still returns 200 | Pass | Expiry moved from 10:35:00 to 10:35:29, then whoami returned 200 ([03](screenshots/03-whoami-200-after-refresh.png)) |
| Cancelling the login leaves a clear signed-out state | Pass | Closing the Custom Tab gave `Status: Sign-in cancelled` |
| Wrong password stays on Keycloak's page with no detail about which field was wrong | Pass | [05](screenshots/05-wrong-credentials.png): only "Invalid username or password." |
| No token, refresh token or Authorization header in screen text, app log output or committed files | Pass | The screen shows subject, roles, audience and expiry only. Device log has 0 token strings from the app or Flutter. It does contain 12 `eyJ...` strings logged by the system UI for a different app's logout URL (a realm called library, an `id_token_hint`); they are not from SmartRail |
| Cleartext HTTP only to localhost and only in debug builds | Pass | `android/app/src/debug/` holds the network security config. The main manifest has no cleartext setting, so release builds stay strict |
| This `RESULT.md` | Pass | This file |
| Existing tests still pass | Pass | No .NET or realm file was changed. Release build with warnings as errors is clean, 24 unit and 70 integration tests pass, and no package has a known vulnerability (run at 10:27) |
| `CLAUDE.md` describes how to run the spike | Pass | Section "Android sign-in spike" |

## The settings that work

These are the settings the operator app should copy.

1. **Emulator address:** `adb reverse tcp:8180 tcp:8180` and `adb reverse tcp:8080 tcp:8080`. Inside the emulator, `localhost` then reaches the PC, so the issuer stays `http://localhost:8180/realms/smartrail`. No API or realm change. The extra issuer `10.0.2.2` was not needed.
2. **Client:** `smartrail-operator-app` (public, code flow with PKCE). The realm file's redirect URI `com.smartrail.operator:/oauth2redirect` already matched. No realm change.
3. **Redirect scheme:** `manifestPlaceholders["appAuthRedirectScheme"] = "com.smartrail.operator"` in `android/app/build.gradle.kts`.
4. **Remove `android:taskAffinity=""` from `MainActivity`** in `AndroidManifest.xml` (finding 1). This is the one change that makes it work.
5. **Cleartext for debug only:** `android/app/src/debug/AndroidManifest.xml` sets `android:networkSecurityConfig`, and `res/xml/network_security_config.xml` allows cleartext to `localhost` only. Production needs HTTPS (deferred decision D3).
6. **Library call:** `authorizeAndExchangeCode` with `discoveryUrl`, `scopes: ['openid']` and `allowInsecureConnections: true` (the last only because Keycloak is HTTP in Compose). `token` with `refreshToken` for the refresh.

## Findings

1. **Flutter's default manifest breaks AppAuth.** `flutter create` writes `android:taskAffinity=""` on `MainActivity`. AppAuth's own activities use the default task, so the redirect from Keycloak started a new task and AppAuth logged `No stored state - unable to handle response`. The app then reported "Sign-in cancelled" even though the login had succeeded. Removing the line fixed it, and the next run signed in. This is the main reason a sign-in spike was worth doing.
2. **The code flow, PKCE and the Custom Tab all work** against Keycloak on plain HTTP, with the one cleartext setting above.
3. **The issuer plan works.** With `adb reverse`, tokens carry the issuer the API accepts, and `whoami` returned 200 with no change on the server.
4. **`prompt=login` is set by the sign-in call.** When a Keycloak session already exists the page says "Please re-authenticate to continue". That is deliberate for a shared device, but the operator app should decide it on purpose. Refresh does not need it.
5. **Process death during sign-in loses the result.** Android killed the app as a cached background process while a user was on the login page (log: process died 10:02:29, about 75 seconds after Chrome opened). I then reproduced it with `adb shell am kill` while the login page was open. After the user submitted the form, the app restarted with `Status: Signed out`, with no crash ([04](screenshots/04-after-process-kill-signed-out.png)). `flutter_appauth` cannot resume the request in a new process. For the operator app: detect an interrupted sign-in (for example a stored "sign-in started" flag) and tell the user to try again, and keep the login short. This is the condition on the GO decision.
6. **Wrong credentials and cancel behave well.** Keycloak gives one generic message, and closing the tab gives a clean signed-out state in the app.
7. **Tokens are kept in memory only.** The spike does not persist them. Secure storage, the 8 hour session end and offline behaviour remain deferred decision D1.
8. **Testing from adb needs care.** `adb shell input text` can double the first character, the on-screen keyboard shifts the layout so coordinate taps miss, and `uiautomator dump` can silently leave an old file behind. Use Tab to move between fields, pause between keystrokes, and delete the dump file first. These affect only automated testing, not the app.
9. **Docker environment problems that cost time, not Android problems.** A different container (`postgres-db`) held port 5432, which stopped SmartRail's Postgres from starting. Docker then left `postgres-1` attached to no network, so Keycloak and the API could not resolve the name `postgres`. Recreating that container (`docker compose up -d --force-recreate --no-deps postgres`) fixed it and kept the data.

## What was not tested (as agreed in the spec)

A real Android device, iOS, HTTPS for Keycloak, automatic refresh, sign-out at Keycloak, secure token storage, offline sign-in, and the controller or passenger apps.

## Next

- Build `mobile/operator_app` using the settings above. Do not import code from this folder.
- Add the "sign-in interrupted, try again" handling (finding 5) to the operator app's sign-in spec.
- Decide on `prompt=login` (finding 4).
