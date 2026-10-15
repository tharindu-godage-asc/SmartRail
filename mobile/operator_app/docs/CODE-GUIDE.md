# Operator app: code guide

A map of every file in `mobile/operator_app`, what it is for, and its main functions. Read this first when you need to find
something or explain how the app works. The requirements are in `_specs/05-operator-app.md`; this guide describes the code.

## 1. The idea in five sentences

The operator signs in with Keycloak and sees the journey they are assigned to. When the train leaves a station they tap
**Departed <stop>**. The tap is saved in a small database on the phone first, with the time it happened. A sender then
delivers it to the API, retrying with growing waits if the network or server fails, and never throws a report away. The screen
works out "what is next" from the server's status plus the reports still waiting on the phone, so it moves on even offline.

## 2. How the code is organised

```text
lib/
  main.dart            starts the app
  app/                 wiring: providers, router, the app widget
  core/                shared pieces: config, text, time, theme, widgets
  features/
    auth/              signing in and out
    journey/           the assigned journey and its screens
    reports/           the offline queue, sending, delivery status
```

Every feature has three folders, and the rule is that dependencies point inward:

| Folder | What lives there | May use |
|---|---|---|
| `domain/` | Plain Dart rules and interfaces. No Flutter, no network, no database. Tested without a phone. | nothing outside itself |
| `data/` | The real things: HTTP, SQLite, the sign-in library, secure storage. Each implements a domain interface. | `domain/` |
| `presentation/` | Riverpod controllers and screens. | `domain/`, `data/` (through providers) |

This is why the tests can replace the network, the database and Keycloak with fakes: the rest of the app only knows the
interfaces.

**Riverpod in one paragraph.** A *provider* is a named, shared value (a service, or a piece of state). A *Notifier* /
*AsyncNotifier* is a provider with state and methods. Widgets that extend `ConsumerWidget` read providers with `ref.watch`
(rebuild when it changes) or `ref.read` (one-off, for actions). `app/providers.dart` lists the services, and tests swap them
with `overrideWithValue`.

## 3. The life of one tap

1. **Tap.** `journey_screen.dart` calls `ReportActions.departed` (in `report_dispatcher.dart`).
2. **Stamp.** The time is `ClockOffset.now()`: the phone's clock corrected by how far it differed from the server's at the
   last successful read, cut to whole seconds. A wrong phone clock does not make a wrong report.
3. **Save.** `DriftReportQueue.enqueue` writes the report (with a new `clientEventId`) to SQLite. If a report for that stop is
   already pending or delivered, it returns that one instead, so a double tap cannot make two.
4. **Screen moves on.** `journeyProgressProvider` re-runs `deriveProgress`, which counts the saved report as done, so the
   next action is already the next stop. The chip says **Not sent yet**.
5. **Send.** `ReportDispatcher.kick` runs `ReportSender.pump`. `SmartRailApi.send` posts to
   `/api/v1/journeys/{id}/events` and turns the answer into a `SendResult`.
6. **Outcome.** `Delivered` (201 or 200) marks it delivered. `RetryLater` (no network, 5xx, 409 version conflict) schedules the
   next try with `Backoff` and holds the queue. `Rejected` (403, 404, other 409, 422) marks it rejected and moves on.
   `Unauthorized` pauses the queue and shows the session-expired screen.
7. **Refresh.** After a delivery the journey is re-read, so the stop list matches the server.

The same `clientEventId` is sent on every retry, so the server treats repeats as one event.

## 4. File by file

### Entry and wiring (`lib/app`, `lib/main.dart`)

| File | What it does |
|---|---|
| `main.dart` | `runApp` inside a `ProviderScope` (the Riverpod container). |
| `app/app.dart` | `OperatorApp`: builds `MaterialApp.router` with the theme and the router. Keeps `reportDispatcherProvider` alive for the whole run (it sends reports). On `AppLifecycleState.resumed` it refreshes the journey and kicks the sender. Shows a spinner while the saved session is being read, so a signed-in operator never sees a flash of the sign-in screen. |
| `app/router.dart` | `routerProvider`: a `GoRouter` with routes `/sign-in`, `/` (the signed-in shell), `/delay/reason`, `/delay/estimate`. Its `redirect` sends signed-out users to `/sign-in` and signed-in users away from it. `_Shell` is the frame: header, the Journey/Help tab (`IndexedStack`) and the bottom bar. |
| `app/providers.dart` | Every outside dependency as a provider: `appConfigProvider`, `databaseProvider`, `reportQueueProvider`, `localCacheProvider`, `authRepositoryProvider`, `dioProvider`, `smartRailApiProvider`, `journeyApiProvider`, `reportApiProvider`, and `isOnlineProvider` (a stream from `connectivity_plus`). Tests override these. |

### Core (`lib/core`)

| File | What it does |
|---|---|
| `config.dart` | `AppConfig`: API address, Keycloak issuer and desk phone from `--dart-define` (defaults suit the emulator with `adb reverse`). Also the client id `smartrail-operator-app`, the redirect URL and `allowInsecure` (true only for `http://` issuers, which exist only in Compose). |
| `strings.dart` | `Strings`: every word on screen in one place, so Sinhala and Tamil can be added later. Functions such as `trainTitle`, `signOutWaiting(count)`. |
| `time.dart` | `colomboOffset` (UTC+5:30, no daylight saving), `inColombo`, `formatTime` ("08:28"), `formatDay` ("Tue 6 Oct"). `ClockOffset`: `measure(serverTime, receivedAt)` stores the difference between server and phone, `now()` returns corrected time. |
| `theme/tokens.g.dart` | **Generated** from `smartrail-tokens.json`. `SmartRailColors` (light and dark values), and constants `Space`, `Radii`, `TouchTarget`, `TypeSize`, `LineHeight`, `ChipOutline`, `TokenIcons`. Never edit by hand. |
| `theme/app_theme.dart` | `buildAppTheme()` builds the light `ThemeData` from the tokens. `context.colors` and `context.text` are shortcuts for widgets. |
| `widgets/widgets.dart` | Shared widgets from the design: `AppHeader` (logo, "Operator", name), `SectionCard` (white card with a small upper-case title), `PrimaryButton` (56 px filled) and `SecondaryButton` (48 px outline), `InfoLine` (icon and small text), `DashedPill` (pill with a solid or dashed outline, used by chips), `AppBottomBar` (Journey and Help tabs). |

### Auth (`lib/features/auth`)

| File | What it does |
|---|---|
| `domain/auth.dart` | `TokenSet` (access, refresh, id token, expiry). `AuthStatus` (signedOut, signedIn, sessionExpired, signInInterrupted, signInCancelled, signInFailed) and `AuthState`. Exceptions `RefreshRejected` (session over), `RefreshUnavailable` (probably offline), `SignInCancelled`. `displayNameFromToken` makes "Nimal O." from the token's given and family name. |
| `domain/auth_ports.dart` | Two interfaces. `AuthGateway`: `authorize`, `refresh`, `endSession`. `TokenStore`: read, write, clear tokens, plus the "sign-in started" flag. |
| `domain/auth_repository.dart` | The rules. `restore()`: a saved session is signed in; a "started" flag with no tokens means Android killed the app during sign-in, so report `signInInterrupted` once. `signIn()`. `accessToken()`: returns the token, refreshing silently when it has less than 30 seconds left. `refreshNow()` after a 401. `signOut()`: ends the Keycloak session, clears tokens. A refused refresh clears tokens; a refresh that fails for another reason (offline) keeps the session. |
| `data/app_auth_gateway.dart` | `AppAuthGateway` implements `AuthGateway` with `flutter_appauth` (Authorization Code with PKCE, `prompt=login`). Maps "Keycloak said `invalid_grant`" to `RefreshRejected` and any other refresh failure to `RefreshUnavailable`. Logs only the error code on failure, never a token. |
| `data/secure_token_store.dart` | `SecureTokenStore` implements `TokenStore` with `flutter_secure_storage` (Android keystore). |
| `presentation/auth_controller.dart` | `AuthController` (an `AsyncNotifier<AuthState>`): `build` restores the session; `signIn`, `signOut` (also clears the cached journey so a shared phone does not show the last operator's train), and `markSessionExpired` (keeps waiting reports). `authControllerProvider`. |
| `presentation/sign_in_screen.dart` | One screen for every signed-out state: first start (`Nimal-Login`), a notice above it for interrupted, cancelled or failed sign-in, and `_SessionExpired` which lists the reports still waiting on the phone (`Nimal-SessionExpired`). |

### Journey (`lib/features/journey`)

| File | What it does |
|---|---|
| `domain/models.dart` | Plain data classes matching the API's JSON: `OperatorJourney` and `OperatorStop` (from `GET operator/journey`), `TrainStatus` and `StopProgress` (from `GET trains/{n}/status`). `StopProgress.hasDeparted` is true for `Reported` and `Inferred` (a later stop was reported, so this one was passed). |
| `domain/journey_api.dart` | The `JourneyApi` interface (`fetchAssignedJourney`, `fetchTrainStatus`) and the three ways a call can fail: `SessionExpiredException`, `NetworkException`, `ServerException`. |
| `domain/progress.dart` | `deriveProgress(journey, reports, status)`: the key rule. A stop is done if the server says it departed **or** a non-rejected report for it is on the phone. The first later stop is "current". If that is the final stop the result is `NoMoreReports` (slice 1 has no departure from the last stop), otherwise `DepartFrom(stop)`. Returns a mark (done, current, upcoming) per stop. |
| `domain/local_cache.dart` | `LocalCache` interface: the last journey with when it was fetched, and the clock offset. |
| `data/smartrail_api.dart` | `SmartRailApi` implements both `JourneyApi` and `ReportApi` with `dio`. `_call` adds the bearer token and, on a 401, refreshes once and asks again; a second 401 means `SessionExpiredException`. `fetchTrainStatus` is a public read with no token. `send` posts the report body and returns a `SendResult` (never throws for network trouble). |
| `data/drift_local_cache.dart` | `DriftLocalCache` implements `LocalCache` on the key-value table. A cache that cannot be read is dropped, not a crash. |
| `presentation/journey_controller.dart` | `JourneyController`: loads the journey and status, polls every 30 seconds, falls back to the saved copy when offline (`offline: true`, with `fetchedAt` for "Last updated"), ignores a status with an older version than the one shown, and measures the clock offset on each status read. `JourneyViewState` is what the screen shows. `ClockOffsetController` keeps the offset (saved between runs). `reportsProvider(journeyId)` streams the phone's reports. `journeyProgressProvider` combines everything through `deriveProgress`. |
| `presentation/journey_screen.dart` | The Journey tab. States: the journey (heading, next-action card, "Report delay", "Last report" card, stop list), the offline banner, "No journey assigned" with Refresh, and "Could not load". `_depart` guards against double taps. Pull to refresh. |
| `presentation/help_screen.dart` | The Help tab: desk phone and **Call the desk** (`PhoneDeskButton`, opens the dialer), the account card, and **Sign out** with a confirmation that says how many reports are still waiting. |
| `presentation/delay_screens.dart` | The two delay steps (reason, total delay). Built as UI only: **Send report** is disabled until the server has a delay endpoint. `offeredDelays(running)` hides options at or below the current delay. |

### Reports (`lib/features/reports`)

| File | What it does |
|---|---|
| `domain/report.dart` | `QueuedReport`: id (`clientEventId`), journey, stop, station name, `occurredAt`, `state` (`pending`, `delivered`, `rejected`), attempts, next try time, last reason. `copyWith`. |
| `domain/send_result.dart` | `SendResult.fromResponse(status, reason)`: 200 and 201 → `Delivered`; 401 → `Unauthorized`; 409 `version_conflict`, 5xx, 408, 429 → `RetryLater`; everything else → `Rejected` with the server's `reason`. |
| `domain/backoff.dart` | `Backoff.after(attempts)`: 5 s, doubling, capped at 5 minutes, with ±20 % random jitter. |
| `domain/report_queue.dart` | The `ReportQueue` interface (`enqueue`, `pending`, `update`, `watch`, `pendingCount`) and the `ReportApi` interface (`send`). |
| `domain/report_sender.dart` | `ReportSender.pump(now)`: sends pending reports oldest first, one at a time. A retrying report **holds** the ones behind it (the server must see them in order); a rejected one does not. Returns `QueueEmpty`, `WaitUntil(time)` or `NeedsSignIn`. |
| `data/app_database.dart` | The drift database: table `Reports` (the queue; rows stay after delivery for the "Last report" card) and `KeyValues`. `app_database.g.dart` is generated by `build_runner`. |
| `data/drift_report_queue.dart` | `DriftReportQueue` implements `ReportQueue` on SQLite. `enqueue` enforces one pending-or-delivered report per stop. `watch` is a live stream for the screen. |
| `presentation/delivery_chip.dart` | `DeliveryChip`: Delivered (solid outline, check icon), Not sent yet (dashed outline, clock icon), Rejected (thick warning outline, error icon). Status is never colour alone. |
| `presentation/report_dispatcher.dart` | `ReportDispatcher.kick()` runs a sender pass, schedules the next one at the `WaitUntil` time, pauses on `NeedsSignIn` (and marks the session expired), and refreshes the journey after anything was sent. It kicks itself when the network returns or sign-in succeeds. `ReportActions.departed` is what the button calls. |

## 5. Tests (`test/`)

| File | What it proves |
|---|---|
| `journey/progress_test.dart` | Which stop is next, with and without queued reports, skipped stops, the end state. |
| `reports/sender_test.dart` | Backoff numbers, `SendResult` mapping, sending order, a retrying report holds the queue, a rejected one does not, nothing lost when the session expires. |
| `reports/drift_queue_test.dart` | The real SQLite file: the queue survives close and reopen, one report per stop, the journey cache survives. |
| `auth/auth_repository_test.dart` | Sign-in, silent refresh, a refused refresh clears the session, an offline refresh keeps it, process-death detection, sign-out. |
| `journey/api_test.dart` | The HTTP client against a scripted network: bearer token, 404, offline, 401 then refresh, exact POST body, reason codes. |
| `journey/journey_screen_test.dart` | Screen states: normal, departed, double tap, offline, rejected, clock hint, no journey, 200 % text size, 44 px target, icon plus text. |
| `auth/auth_screens_test.dart`, `journey/help_and_delay_test.dart` | Sign-in variants, session expired with waiting reports, sign-out dialog, the delay screens. |
| `core/time_test.dart`, `core/tokens_test.dart` | Colombo time, clock offset, and that `tokens.g.dart` matches `smartrail-tokens.json`. |
| `support/fakes.dart`, `support/harness.dart` | The fakes (in-memory queue, scripted API, fake token store) and `TestApp`, which runs the whole app against them. The harness loads Roboto so text widths are realistic. |

## 6. Tools, config and CI

| File | What it does |
|---|---|
| `smartrail-tokens.json` | The design values (colours light and dark, type sizes, spacing, radii, icons) in DTCG format. The single source for the theme. |
| `tool/generate_tokens.dart`, `tool/tokens_generator.dart` | Turn the JSON into `tokens.g.dart`. Run `dart run tool/generate_tokens.dart`. |
| `pubspec.yaml` | Dependencies: `flutter_riverpod`, `dio`, `flutter_appauth`, `flutter_secure_storage`, `connectivity_plus`, `go_router`, `drift`, `url_launcher`, `clock`, `uuid`, `intl`. |
| `analysis_options.yaml` | `flutter_lints`. |
| `android/app/build.gradle.kts` | Sets the sign-in redirect scheme `com.smartrail.operator`. |
| `android/app/src/main/AndroidManifest.xml` | Internet permission, the dialer query, and **no** `taskAffinity` (it breaks the sign-in redirect; spike finding 1). |
| `android/app/src/debug/...` | Allows plain HTTP to `localhost` in debug builds only. Release builds stay strict. |
| `screenshots/` | Pictures from the emulator run, for the pull request. |
| `../../.github/workflows/mobile.yml` | CI: regenerate the generated files and fail if they differ, then `flutter analyze` and `flutter test`. Runs only when this folder changes. |

## 7. Where to change things

| You want to... | Change |
|---|---|
| Change a word on screen | `core/strings.dart` |
| Change a colour, size or icon | `smartrail-tokens.json`, then `dart run tool/generate_tokens.dart` |
| Change the retry timing | `reports/domain/backoff.dart` (and its test) |
| Change what counts as Delivered or Rejected | `reports/domain/send_result.dart` |
| Change which stop is offered next | `journey/domain/progress.dart` |
| Change the poll interval | `JourneyController.pollEvery` |
| Add a database column | `reports/data/app_database.dart`, bump `schemaVersion`, write a migration, run `dart run build_runner build` |
| Point the app at another server | `--dart-define=API_BASE_URL=...` and `KEYCLOAK_ISSUER=...` |

## 8. What is deliberately not here yet

Arrival reports, sending a delay, "flag my last report", the suspended state and the Reports tab need server work first (see
the spec's Non-Goals). The Noto Sans font file is not bundled, so the platform font is used.
