# Feature Spec: Catering integration (menu feed, simulated caterer, food menu in the passenger app)

branch: claude/feature/catering-integration
spec: _specs/11-catering-integration.md
owner: tharindu
date: 2026-10-16
status: approved 2026-10-16 (owner: Q1 to Q9 as recommended; branch from passenger-app)

---

## 0) Summary
**One-liner:** The caterer's system sends its menu and item availability to SmartRail over an authenticated, repeat-safe feed. SmartRail stores it and serves a read-only menu list for the stations on a passenger's journey (the destination and long stops with catering). The passenger app shows that list and one button that opens the caterer's own website. A small simulated caterer (its own service in Compose) sends the menu, lets you mark items sold out, and serves a stand-in website page.
**Why now:** The brief requires a catering integration, and this is the only catering direction that D-71 and D-72 keep in the build. It is the last feature spec. It also turns on the two Food-Menu screens that spec 10 left as fixtures.
**Who is this for:** Caterer (an external system, not a person) and Passenger (Kasuni).
**Success looks like:**
- The simulated caterer starts, gets a token from Keycloak, and sends the menu. The API accepts it. The same message sent again changes nothing (AC-E08-08 1, 2).
- In the passenger app, train 8058 from Veyangoda to Colombo Fort shows a "Food menu" entry. It lists the items with price and Available or Sold out, and "Menu from 08:30" (AC-E08-01 1).
- You mark "Fruit cup" sold out on the caterer's admin page. The app shows "Sold out" (text and icon) at its next refresh, no more than 60 seconds later (AC-E08-01 2, AC-E08-08 4).
- The button opens the stand-in page with `?station=CF&train=8058` in the address and nothing else (AC-E08-07 1, 2, 7).
- Stop the caterer. After the freshness limit the list stays and says the menu may be out of date (AC-E08-01 6).

Size: medium, about 1.75 days (backend 0.5, simulated caterer 0.5, app 0.5, tests and the end-to-end run 0.25). Cut order is in Q9.

## 1) Traceability
- Requirements (wording read in `docs/smartrail-refined-requirements.md` v1.13): FR-07 (caterer authenticates with client credentials), FR-14 (item names in en, si, ta), FR-90 (read-only list, price, Available or Sold out, last updated, out-of-date note), FR-91 (menus only for the destination and approved long stops of 5+ minutes with catering), FR-100 (simulated caterer: feed, a way to change availability, stand-in page; no orders, payments or train events), FR-124 (one button to the caterer's website), NFR-20 and NFR-21 (no passenger identity to the caterer), NFR-30 (status never by colour alone), NFR-32 (screen reader), NFR-07 (never silent), NFR-43 (runs in Compose).
- Acceptance criteria: AC-E08-01 (1 to 7, with item 1 only partly, see Q1), AC-E08-07 (1 to 5 and 7; 6 is a fixture), AC-E08-08 (1 to 4). AC-E08-05 (train events) and AC-E08-02, 03, 04, 06 (ordering) are **not built** (D-71, D-72).
- Edge cases (looked up in `docs/smartrail-edge-cases.md`, section 6): EC-107, EC-180, EC-181, EC-182, EC-183, EC-184, EC-185 (fixture), EC-186, EC-100 (applied to menu versions). EC-96, EC-98, EC-99, EC-108 belong to train events and stay deferred.
- Decisions and assumptions: D-13 (caterer owns menu, payment, refunds), D-15 and A-07d (5-minute stop rule), D-71, D-72, D-73 (disrupted state; address foodforu.com for now), A-35 (caterer website and link parameters), A-07j (menu freshness limit, proposed 60 minutes, configurable), ADR-12 (message ID, version), ADR-13 (hybrid integration).
- Design: Food-Menu, Food-Menu-Offline, Food-Menu-Disrupted (passenger boards 32 to 34 in spec 10).

## 2) Goals and Non-Goals
### Goals
- `PUT /api/v1/caterer/menus/{stationCode}`: the feed from the caterer, caller must have the `caterer` role.
- Three tables for the menu plus a table of processed message IDs, with one migration.
- `GET /api/v1/trains/{trainNumber}/menu`: the public, read-only list for the passenger app.
- A simulated caterer service: menu feed, admin page with availability toggles, stand-in website page.
- The Food menu screen in `mobile/passenger_app`, its offline note, and the link button (FR-124).
- Seed: Colombo Fort marked as having catering, a menu for it (placeholder data).
- Tests at unit, integration and end-to-end level.
### Non-Goals (all designed, not built)
- **Train events to the caterer (FR-95, NFR-08, AC-E08-05):** no outbox, no retries toward the caterer, no SmartRail client token. SmartRail sends the caterer nothing (D-72). This is a risk the owner accepted, and the brief (section 11) says the caterer may need railway information. The report must say so.
- **Ordering and everything after it (FR-92 to FR-94, FR-96 to FR-99, FR-101, FR-102, AC-E08-02, 03, 04, 06):** no orders, order references, payment, tokens or refunds. The `OrderReference` table is not created (D-71, A-35).
- **The disrupted food screen (AC-E08-07 6, EC-185):** cancelled and terminated services do not exist in the backend yet. The screen is a fixture in the gallery (spec 10, board 34). The list endpoint has no disruption field now. When overrides are built, a `serviceState` field is added.
- Menu management by SmartRail staff, a SmartRail admin screen for the menu, caterer-side authentication of SmartRail calls (nothing is sent that way), per-journey menus, nutrition or allergen data, images, prices in other currencies, caterer opening hours, cutoff times.
- Rate limiting of the feed (NFR-13 is deferred everywhere). Keycloak token lifetime and rotation of the caterer secret are not changed.

## 3) User Workflow
### Primary workflow (Kasuni, with the simulated caterer)
1. The caterer service starts, gets a token (`smartrail-caterer`, client credentials) and sends the menu for Colombo Fort. SmartRail stores it.
2. Kasuni opens train 8058 (Veyangoda to Colombo Fort) in the passenger app. The live screen asks for the menu and shows a "Food menu" row because the destination has catering and a menu.
3. She opens it: "Menu from 08:30", items with price and Available or Sold out, the note that ordering, payment and collection happen on the caterer's website and that SmartRail does not see the order.
4. The person running the demo marks "Fruit cup" sold out on the caterer's admin page. The caterer sends a new menu version. At the next refresh the list shows "Sold out".
5. Kasuni taps "Order on the caterer's website". The phone's browser opens the stand-in page.
### Secondary workflows
- **Phone offline:** the last list stays with "Menu from 07:12. It may have changed." The button explains she is offline and opens nothing (EC-181).
- **Feed silent:** no update for longer than the freshness limit: the list stays with the same note (EC-180).
- **No food on this journey:** no Food menu row is shown (AC-E08-01 7). For example, any journey that ends at Veyangoda, because no catering is set there.
- **Caterer sends a bad or old message:** rejected or ignored, the stored menu is unchanged (section 6).

## 4) API Surface
### 4.1 `PUT /api/v1/caterer/menus/{stationCode}` (feed from the caterer)
- Who: policy `Caterer` (role `caterer`, the `smartrail-caterer` service account). No token: 401. Other roles: 403. Passengers, operators and controllers cannot call it.
- Request body (replaces the station's whole menu; availability updates use the same call):
```json
{
  "messageId": "8d2c5e0e-5f0a-4b64-9d3a-6c7c8c1b2a11",
  "version": 4,
  "sentAt": "2026-10-16T08:30:12Z",
  "items": [
    { "itemId": "egg-sandwich", "names": { "en": "Egg sandwich", "si": "...", "ta": "..." }, "priceLkr": 250.00, "available": true },
    { "itemId": "fruit-cup",    "names": { "en": "Fruit cup" },                              "priceLkr": 180.00, "available": false }
  ]
}
```
- Validation (all failures: 400/422 ProblemDetails, nothing stored, outcome `rejected` logged and counted): body at most 64 KB; `messageId` a GUID; `version` an integer from 1; `sentAt` a UTC instant not more than 5 minutes in the future (server time); 1 to 100 items; `itemId` 1 to 40 characters from `a-z 0-9 -`, unique in the message; `names.en` required, 1 to 100 characters; only language codes that exist in `language` (`en`, `si`, `ta`); `priceLkr` from 0 to 100000 with at most 2 decimals; `available` a boolean. Station code unknown: 404 `station_not_found`. Station exists but has no catering facility: 422 `station_has_no_catering`.
- Response `200`:
```json
{ "status": "applied", "version": 4 }
```
`status` is one of:
  - `applied`: stored, replaces the previous menu of that station.
  - `duplicate`: this `messageId` was processed before. Nothing changes. The answer repeats the first outcome (200).
  - `stale`: `version` is not greater than the stored version. Nothing changes (AC-E08-08 3). It is 200, not an error, so a retrying caterer stops retrying.
- Example call (development token): `curl -X PUT http://localhost:8080/api/v1/caterer/menus/CF -H "Authorization: Bearer $CATERER_TOKEN" -H "Content-Type: application/json" -d @menu.json`.

### 4.2 `GET /api/v1/trains/{trainNumber}/menu?from={code}&to={code}` (passenger app)
- Who: anonymous (like the other passenger reads). No account, no identifier.
- `from` and `to` are optional station codes: `from` is the boarding station (default: first stop), `to` the destination (default: last stop). Both must be stops of the service, `from` before `to`, otherwise 404 `station_not_found` / 422 `invalid_stop_order`. Unknown train number: 404 `train_not_found`. The list does not need a journey to exist today: eligibility depends on the timetable only.
- Response `200` (an empty `stations` array means no Food menu entry):
```json
{
  "serverTime": "2026-10-16T08:31:02Z",
  "trainNumber": "8058",
  "orderUrl": "http://10.0.2.2:8090/?station=CF&train=8058",
  "staleAfterMinutes": 60,
  "stations": [
    {
      "code": "CF",
      "names": { "en": "Colombo Fort", "si": "කොළඹ කොටුව", "ta": "கொழும்பு கோட்டை" },
      "reason": "Destination",
      "menuVersion": 4,
      "menuUpdatedAt": "2026-10-16T08:30:12Z",
      "receivedAt": "2026-10-16T08:30:13Z",
      "stale": false,
      "items": [
        { "itemId": "egg-sandwich", "names": { "en": "Egg sandwich", "si": "...", "ta": "..." }, "priceLkr": 250.00, "available": true }
      ]
    }
  ]
}
```
- `reason` is `Destination` or `LongStop`. `stale` is true when `serverTime - receivedAt` is more than `staleAfterMinutes`. `menuUpdatedAt` is the caterer's `sentAt` (what the screen shows as "Menu from"); `receivedAt` is when SmartRail got it.
- `orderUrl` is built by the server from `Catering:WebsiteUrl` (one setting, FR-124) plus `station` and `train` for the **first** eligible station only (the destination if present). It is `null` when the setting is empty, and then the app shows no button. It never carries an account, device, token or journey ID (NFR-21, EC-186).
- Headers: `Cache-Control: public, max-age=15`. Errors use ProblemDetails like spec 04.

## 5) Data Contract
New migration `AddCateringMenu` (EF Core, snake_case names as in the other tables; the default privileges already give `smartrail_app` DML on new tables, rule R9):

| Table | Columns | Notes |
|---|---|---|
| `station_menu` | `station_id uuid pk fk station`, `version bigint not null check (version >= 1)`, `caterer_sent_at timestamptz not null`, `received_at timestamptz not null`, `source_client varchar(100) not null` | One row per station with a menu; the highest version applied |
| `menu_item` | `id uuid pk default gen_random_uuid()`, `station_id uuid fk`, `caterer_item_id varchar(40) not null`, `price_lkr numeric(10,2) not null check (price_lkr >= 0)`, `available bool not null`, `sort_order int not null` | `unique (station_id, caterer_item_id)`; the order in the message is the display order |
| `menu_item_translation` | `menu_item_id uuid fk on delete cascade`, `language_code varchar(5) fk language`, `name varchar(100) not null` | PK `(menu_item_id, language_code)`; matches `MenuItemTranslation` in the data model; `en` always present |
| `menu_update_message` | `message_id uuid pk`, `station_id uuid fk`, `version bigint`, `outcome varchar(10) check in ('applied','stale','rejected')`, `received_at timestamptz` | Append-only record of every feed call that reached validation; this is how a repeated `messageId` is recognised |

- Seed (reference data, migration): `station.has_catering_facility = true` for Colombo Fort (`CF`) only. Existing `HasData` rows change, so this needs the migration (see `ReferenceData.cs` note).
- Seed (demo data, in the simulated caterer, **not** in SmartRail's database): menu for CF with five placeholder items, including "Egg sandwich Rs 250" and "Fruit cup" as in the design board. All prices are placeholders. Sinhala and Tamil names are written during the build and are **unverified** until a native speaker checks them.
- No journey data changes. No outbox table (D-72).

## 6) Business Rules
1. **Authentication (FR-07, AC-E08-08 1).** Only a valid token with role `caterer` may call the feed. The token is validated by the existing `Caterer` policy.
2. **Repeat-safe (AC-E08-08 2, ADR-12).** The first write for a `messageId` wins. A second call with the same ID returns `duplicate` and changes nothing, even if its body differs.
3. **Newest version only (AC-E08-08 3, EC-100).** Per station, a message is applied only if its `version` is greater than the stored one. Done as one atomic statement (`update station_menu set ... where station_id = @s and version < @v`) in the same transaction as replacing the items, so two messages arriving together cannot apply out of order.
4. **Whole-menu replacement.** An item missing from a newer message disappears from the list. Availability changes are sent as the full menu again with `available` changed. This avoids a second endpoint and the question of what an update to a missing item means (Q2).
5. **Eligible stations (FR-91, D-15, A-07d, AC-E08-01 4, EC-107).** For a passenger travelling `from` to `to` on a service, a stop is listed when it comes after `from` and not after `to`, its station has `has_catering_facility`, and either it is the destination or its scheduled stop (departure minus arrival, both present) is at least 5 minutes (`Catering:MinLongStopMinutes`, configurable, A-07d). The boarding station is not listed. A station with the facility but no menu yet is left out (so no empty section).
6. **Read-only (AC-E08-01 3).** The API has no order, basket or quantity concept anywhere.
7. **Freshness (FR-90, AC-E08-01 6, EC-180).** `stale` is calculated on the server with the controllable clock (`TimeProvider`), so a demo can age the menu without waiting. The limit is `Catering:MenuFreshnessMinutes`, default 60 (A-07j; D-73 had parked it, this spec builds it because it is a few lines; Q4). The app also shows the same note when the phone is offline.
8. **Names (FR-14, EC-184).** `en` is required. If `si` or `ta` is missing for an item, the API returns only what it has and the app shows the English name. A test in the simulated caterer fails if any demo item lacks `si` or `ta`.
9. **The link (FR-124, NFR-21, EC-186).** The server builds it from the setting. Parameters are `station` and `train`, URL-encoded and checked against the known station and train, so a request cannot inject anything. The foodforu.com address (D-73) is the real value; the Compose value points at the simulated caterer's page. Whether foodforu.com accepts the parameters is unconfirmed (A-35).
10. **No passenger data.** The menu read has no cookies, no identifiers and logs no IP address beyond what the existing request logging records. The feed contains no passenger data in either direction.
11. **Late, wrong or silent caterer.** Invalid messages are rejected and logged; they never alter the stored menu. A silent caterer leaves the last menu in place, marked stale after the limit. SmartRail never blocks on the caterer: no operational path calls it.
12. **Errors never become silence (NFR-07).** If the app cannot load the menu and has none stored, the Food menu row is not shown, and the live status screen is unaffected.

## 7) Architecture (follow CLAUDE.md and ADR-15)
- **Domain:** `MenuItem`, `StationMenu` (with the "apply only if newer" rule as a method), `MenuUpdateOutcome`; the long-stop rule as a pure function on stops (`FoodStopRule.Eligible(stops, fromSeq, toSeq, minMinutes)`).
- **Application:** `ApplyMenuUpdate` (command, validator, handler: record message, version check, replace items) and `GetTrainMenu` (query: stops, stations, menus, freshness, link). Interfaces: `IMenuRepository`.
- **Infrastructure:** EF configuration and migration `AddCateringMenu`; repository with the atomic version update; `Catering` options (`WebsiteUrl`, `MenuFreshnessMinutes`, `MinLongStopMinutes`).
- **Api:** `Features/Catering/CateringEndpoints.cs` with the two routes; the feed uses `RequireAuthorization(Policies.Caterer)`; the read uses `AllowAnonymous`.
- **Simulated caterer:** new project `src/SmartRail.SimulatedCaterer` (.NET minimal API, a separate Compose service on port 8090). It holds its menu in memory, loaded from a JSON seed file at start (it resets on restart; this is a stand-in, not a database, Q5). Routes: `GET /` (the stand-in website page: shows the menu, says it is a demonstration and takes no orders or payments, shows the `station` and `train` it was opened with), `GET /admin` (a plain page: list of items with an Available toggle and a "Send full menu now" button), `POST /admin/items/{id}/available` (changes the item, raises the version, sends). It fetches a client-credentials token from Keycloak (`smartrail-caterer`, development secret from Compose environment), sends the menu at start with a retry until SmartRail answers (3 tries, 2 s apart, then the admin page shows the failure), and shows the last result (applied, duplicate, stale, rejected) on the admin page. It has no outbox and receives nothing from SmartRail (D-72).
- **Compose:** new `caterer` service; `Catering__WebsiteUrl: http://10.0.2.2:8090/` on the API (reachable from the Android emulator); the caterer reaches the API at `http://api:8080` and Keycloak at `http://keycloak:8080/realms/smartrail`. The token's issuer is `http://localhost:8180/...` as for every other client, so the caterer requests the token from the Keycloak container but the API's existing issuer setting keeps accepting it (to be verified on the first run; if the issuer differs, the caterer sets the `Host` header like the dev token helper).
- **Passenger app:** new `lib/features/food`: repository interface and Dio implementation for the menu read, Riverpod provider with the cached last result per train (`shared_preferences`, as the other caches), `FoodMenuScreen`, the offline state, the link button via `url_launcher` (new dependency), entry row on the live status screen shown only when `stations` is not empty, strings in all three languages (en, si, ta keys; the existing key-completeness test applies). The refresh runs on open, on pull-to-refresh, and every 60 seconds while the screen is open (no overlapping requests, as the live screen).
- **Telemetry:** counter `smartrail.menu.updates` with attribute `result` (`applied`, `duplicate`, `stale`, `rejected`), counter `smartrail.menu.reads`; log fields `stationCode`, `messageId`, `version`, `clientId`, `outcome`. Grafana can show updates by result.

## 8) Edge Cases
| EC | Case | Handling |
|---|---|---|
| EC-100 | Older menu version arrives after a newer one | `stale`: ignored, 200 (rule 3) |
| EC-107 | Long-distance train with no stop long enough | Only the destination is listed (rule 5) |
| EC-180 | Feed silent, or phone offline | List stays, "Menu from [time]" and the may-have-changed note (rules 7, 12) |
| EC-181 | Offline when the button is tapped | The app says she is offline, no page opens, the list stays |
| EC-182 | Caterer's website down or address wrong | The phone's browser shows its own error; accepted limitation; the address is a setting and the demonstration checks it opens |
| EC-183 | Item shown Available but sold out on the website | Accepted limitation; the list shows when it was updated |
| EC-184 | Item has no si or ta name | English shown; a test checks the demo menu is complete |
| EC-185 | Journey cancelled or terminated before the food station | **Fixture only** (overrides not built); no button on that fixture screen |
| EC-186 | Link carries passenger data | Only `station` and `train`; tested |
| (new) | Same message twice | `duplicate` (rule 2) |
| (new) | Two messages at once | Atomic version update; one `applied`, one `applied` or `stale` by version, never out of order (rule 3) |
| (new) | Message for a station without catering | 422, nothing stored |
| (new) | Station has catering but no menu yet | Omitted from the list; if none left, no Food menu row |
| (new) | Item removed from the menu | Disappears at the next refresh (rule 4) |
| (new) | Menu or price changes while the screen is open | Updates at the next refresh; a changed availability is announced to a screen reader, a price change is not |

## 9) Acceptance Criteria
- [ ] A call to the feed without a token returns 401, with a controller or operator token returns 403, with the caterer token and a valid body returns 200 `applied` and the menu is stored (AC-E08-08 1).
- [ ] The same `messageId` twice returns `duplicate` and the stored menu is unchanged (AC-E08-08 2).
- [ ] A message with a version not greater than the stored one returns `stale` and changes nothing (AC-E08-08 3).
- [ ] An invalid message (bad item, unknown language, price out of range, unknown station, station without catering, oversized body) is rejected and changes nothing.
- [ ] After the caterer marks an item unavailable, the next menu read shows `available: false` and the app shows "Sold out" with text and an icon, not colour alone (AC-E08-01 2, AC-E08-08 4).
- [ ] For train 8058 from Veyangoda to Colombo Fort the menu read lists Colombo Fort as `Destination` with its items, price and availability (AC-E08-01 1, in the form the seed allows, see Q1).
- [ ] A service with an intermediate stop of 4 minutes lists no menu for it; a stop of 5 minutes with catering lists one (unit and integration test; AC-E08-01 4).
- [ ] A journey whose destination has no catering returns an empty `stations` array, and the app shows no Food menu row (AC-E08-01 7).
- [ ] Item names follow the chosen language and fall back to English (AC-E08-01 5).
- [ ] The list has no quantity control, basket or Order button; the only action is the link (AC-E08-01 3).
- [ ] After no update for longer than the freshness limit (tested with the controllable clock), `stale` is true and the app shows "Menu from [time]" with the may-have-changed note; with a current menu it shows the time without the note (AC-E08-01 6).
- [ ] The button opens the simulated caterer's stand-in page; the address carries only `station` and `train`; the screen says ordering, payment and collection happen there and that SmartRail does not see the order (AC-E08-07 1, 2, 3, 7).
- [ ] Offline, the button explains she is offline and no page opens; the list stays (AC-E08-07 4).
- [ ] A screen reader announces the button as a link that opens outside SmartRail (AC-E08-07 5).
- [ ] The simulated caterer takes no orders or payments and receives nothing from SmartRail (FR-100, D-72).
- [ ] `dotnet test`, `flutter analyze --fatal-infos` and `flutter test` are green; the Compose stack starts with the new service.
- [ ] The report states plainly that FR-95 and NFR-08 (train events to the caterer) and all ordering requirements are not built, and that the disrupted food screen is a fixture.

## 10) Test Plan
### 10.1 Seed and fixture data
- Reference seed: CF with catering; services 8058, 8061, 8075 end at CF. The simulated caterer's JSON menu for CF with five items, one sold out.
- Test-only: a fabricated service with stops of 4 and 5 minutes at stations with catering, to test the long-stop rule (the real seed has stops of 1 minute only).
- Tokens: the existing test token issuer for roles `caterer`, `controller`, `operator`; no token.
### 10.2 Unit tests
- `FoodStopRule`: destination only; 5-minute stop yes; 4-minute stop no; stop with a null arrival or departure no; boarding station not listed; stops after `to` not listed; facility false.
- `StationMenu.Apply`: newer version applies; equal and older do not; version 1 on empty.
- Validator: each rule in 4.1, boundaries (100 items, 101 items, price 100000 and 100000.01, name 100 and 101 characters, `sentAt` +5 and +6 minutes).
- Link builder: normal; unknown station; characters that need encoding; empty setting gives `null`.
- Flutter: menu model parsing; language fallback; stale and offline notes; Food menu row hidden for empty `stations`; sold-out chip has text and icon; offline button does not call the launcher.
- Simulated caterer: every demo item has `si` and `ta` names (EC-184); toggling raises the version.
### 10.3 Integration tests (Postgres in a container, as the existing tests)
- Feed: 401, 403, 200 applied, duplicate, stale, rejected cases, station without catering, concurrent messages with different versions (final stored version is the highest).
- Read: destination listed, availability change visible, empty list, unknown train, `from` after `to`, freshness with the fake clock, link has only the two parameters, anonymous access works.
- Migration: tables exist, the uniqueness and check constraints fail on bad inserts, CF has the facility flag.
### 10.4 End to end (Compose plus emulator, recorded with screenshots)
Caterer starts and sends; the app shows the list; toggle Fruit cup; the app shows Sold out within 60 seconds; tap the button and the stand-in page opens; stop the caterer service, move the clock past the limit (or set a 1-minute limit for the run), the note appears; airplane mode shows the offline state and the button explanation. This is the "menu feed silent" half of the fifth end-to-end flow. The termination half is a fixture (Q8).

## 11) References
- `docs/smartrail-discovery.md` sections 18.4 and 18.5 (data model, integration), ADR-12, ADR-13, D-71, D-72, D-73; `docs/smartrail-refined-requirements.md` section 2.9; `docs/smartrail-acceptance-criteria.md` E08; `docs/smartrail-edge-cases.md` section 6; A-07d, A-07j, A-35.
- Specs 02 (Keycloak clients, the `caterer` role), 04 (public read style, ProblemDetails), 07 (migration and test style), 10 (screens 32 to 34, passenger app structure).
- Existing code reused: `Policies.Caterer`, the `smartrail-caterer` client in `realm-smartrail.json`, `TestTokenIssuer`, `TimeProvider`, EF configuration pattern from `ReferenceConfigurations.cs`, the passenger app's repository, cache and l10n patterns.

## 12) Open Questions (recommendation first)
- **Q1. Demo data does not match AC-E08-01 1.** The criterion uses Colombo Fort to Badulla with food at Badulla. The seed has five stations on one line, no Badulla and no long-distance service, and no station has catering. Recommend: use what exists. Mark Colombo Fort as the catering station, so every inbound train (8058, 8061, 8075 and the demo trains from spec 10) shows food at its destination, and journeys that end elsewhere show none. The long-stop rule is proven by tests with a fabricated service. Alternative: add Badulla and a long-distance service (new stations, a second line or an extended line, a seed migration, changes to the station list and search). That is probably a day and touches finished specs. I would only do it if you want the literal demo; tell me, and the report will say AC-E08-01 1 is shown on Colombo Fort instead.
- **Q2. One feed endpoint or two.** Recommend one: the caterer always sends the full menu, with availability as a field. Alternative: add `PATCH .../availability` for small changes. Simpler code and tests with one; the cost is that a toggle resends the menu (tiny here).
- **Q3. Where the order link is built.** Recommend the server builds `orderUrl` from one setting, so the app has no address and the "carries nothing else" rule is enforced in one place and tested. Alternative: a setting in the app.
- **Q4. Menu freshness limit (A-07j, D-73 parked it).** Recommend building it: 60 minutes, configurable, server-side with the controllable clock. It is a few lines and AC-E08-01 6 and EC-180 need it. If you decided to leave it out, the "silent feed" half of the demo goes with it.
- **Q5. Simulated caterer storage.** Recommend in-memory, loaded from a JSON file, resets on restart. Discovery 18 says "separate system with its own database", but it takes no orders, so a database would only hold five items. Alternative: SQLite file in a volume (about an hour more).
- **Q6. Entry points in the app.** Recommend one: a "Food menu" row on the live status screen, shown only when the train has a menu. Spec 10 had also hidden the Home tile; I would leave it hidden. Alternative: show the Home tile when the followed train has a menu (a second request on Home).
- **Q7. Placeholder data and translations.** Menu items, prices and Sinhala and Tamil names are placeholders and unverified. I will not state any of it as real. Do you have someone to check the translations (same question as spec 10 Q3)?
- **Q8. Disrupted screen and the fifth end-to-end flow.** Overrides do not exist, so the disrupted food screen cannot be reached. Recommend leaving it a fixture and saying in the test report that the termination half of flow 5 is not demonstrated. Alternative: add a controller override first (a separate, larger slice).
- **Q9. Time and cut order.** About 1.75 days, on top of spec 10's 2.5. Cut in this order: the "Send full menu now" button, the stand-in page's extras (keep a plain page), the 60-second auto-refresh (keep refresh on open and pull), the concurrency test. Never cut: the authenticated feed, repeat-safe and newest-version rules, the list screen, the offline note, the link. If you are short of time, tell me what the real budget is before 20 October.

## 13) Owner decisions of 16 Oct 2026
- Q1 to Q9 as recommended. Spec 11 starts from `claude/feature/passenger-app` (spec 10 is not merged yet), so its pull request is merged after spec 10's.

## 14) Change of approach (owner, 16 Oct 2026): SmallBites is its own system and SmartRail pulls its menu
The caterer is a separate company, **SmallBites**, with its own service, API and data. SmartRail does not host any caterer code or tables. This **replaces** sections 4.1, 5, and the feed parts of 2, 3, 6, 7, 9 and 10; everything about the passenger read, eligibility, freshness, the link and the app stays.

**SmallBites** (folder `smallbites/` at the repo root, its own .NET project, Dockerfile and Compose service on port 8090; not part of `smartrail-platform/src`):
- `GET /api/menus/{stationCode}`: `{ station, version, updatedAt, items: [{ id, names: { en, si, ta }, priceLkr, available }] }`; 404 when the station has no menu. Public, read-only (a real caterer would require a key; not built).
- `GET /` its customer website page (menu list, says it is a demonstration, takes no orders or payments, shows the `station` and `train` it was opened with); `GET /admin` plain page with an Available toggle per item; `POST /admin/items/{id}/available` changes it and raises `version`. Menu for Colombo Fort held in memory, loaded from a JSON file (placeholder data, `si` and `ta` names unverified); resets on restart.
- It receives nothing from SmartRail and has no train data (D-72).

**SmartRail** (no new tables, **one migration only**: `station.has_catering_facility = true` for Colombo Fort):
- `IMenuSource` (Application) and `SmallBitesMenuSource` (Infrastructure, `HttpClient`, `Catering:SmallBitesUrl`, 3 s timeout). It keeps the last good menu per station in memory with the time it was fetched, and fetches again at most every 15 s per station. If SmallBites does not answer, the last good menu is returned.
- `GET /api/v1/trains/{trainNumber}/menu?from=&to=` as in 4.2, with `menuUpdatedAt` (SmallBites `updatedAt`), `fetchedAt` (when SmartRail last got it) and `stale` = `now - fetchedAt` over `Catering:MenuFreshnessMinutes` (60). A station whose menu was never fetched is left out. No version or messageId in the response.
- **Removed:** `PUT /api/v1/caterer/menus/{stationCode}`, the `caterer` policy use, message IDs, version rules, the three menu tables and the message table.

**Requirements affected, stated plainly in the report:** FR-07 (caterer authenticates to SmartRail with client credentials) and AC-E08-08 (1 to 3: authenticated, repeat-safe, newest version) are **not built**, because SmartRail pulls and SmallBites sends nothing. AC-E08-08 4 (a changed availability reaches the screen within 60 s) is kept: SmartRail refetches at most every 15 s and the app every 60 s. Keycloak's `smartrail-caterer` client stays unused.

**Acceptance criteria (replace the first four of section 9):**
- [ ] SmallBites runs as its own container, serves the menu and the admin page, and shares no code or database with SmartRail.
- [ ] The menu read for 8058 Veyangoda to Colombo Fort lists Colombo Fort as `Destination` with SmallBites items, price and availability; marking an item sold out in the admin page shows `available: false` within about 20 s and "Sold out" in the app within 60 s.
- [ ] With SmallBites stopped, the read still returns the last menu with `stale` true after the freshness limit (controllable clock in the tests); with no menu ever fetched the station is left out.
- [ ] Unit tests: the stop rule, the link builder, the staleness rule; integration tests: the read with a fake `IMenuSource`; SmallBites tests: toggle raises the version, every demo item has `si` and `ta` names.
