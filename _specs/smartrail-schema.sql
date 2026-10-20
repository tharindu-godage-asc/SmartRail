-- SmartRail MVP schema (draft) - matches smartrail-er-diagram.md
-- PostgreSQL 13+ (gen_random_uuid() is built in from PG 13)
-- Purpose: review and visualise the data model in pgAdmin (ERD tool).
-- The application schema will later be created by EF Core migrations;
-- treat this file as a review artefact, not a second source of truth.
--
-- UPDATED 19 October 2026 to match the EF Core migrations (InitialCreate ... AddAssignmentEnd, model snapshot).
-- BUILT tables carry no marker. Tables marked "DESIGN ONLY, NOT BUILT" are not created by any migration.
-- Differences from the first draft, all in the built tables:
--   train_service: no longer one row per train number. kind, version, effective_from/to and superseded_service_id added (specs 13, 14).
--   journey.status: 'Cancelled' added (spec 14).
--   operator_assignment: ended_by, end_reason, end_note added (spec 17).
--   journey_exception: type limited to what is raised today; station_id and resolved_by_name added; no related_journey_id,
--     platform_id or owner_user_id yet.
--   new tables service_cancellation and service_change_log (specs 13, 14).
--   train_event: the append-only trigger (UPDATE, DELETE, TRUNCATE refused) and the role grants live in the InitialCreate
--     migration, not in this file.
-- 12 October 2026 (D-71): food ordering is designed but not built. The table
-- order_reference below is kept as part of the design and is NOT created by the
-- MVP's migrations. menu_item and its translations are built (read-only list).

CREATE SCHEMA IF NOT EXISTS smartrail;
SET search_path TO smartrail;

-- ---------------------------------------------------------------
-- Languages and translations
-- Each entity carries a default-language (English) `name`. Translation
-- tables hold the OTHER languages, so adding a language is a data change
-- (a new LANGUAGE row plus translations), not a schema change. Interface text is NOT stored here: it lives in
-- the shared localisation files (D-52).
-- ---------------------------------------------------------------

CREATE TABLE language (
    code        varchar(5)  PRIMARY KEY,         -- 'en', 'si', 'ta'
    name        varchar(50) NOT NULL,
    is_default  boolean     NOT NULL DEFAULT false
);
-- Exactly one default (fallback) language
CREATE UNIQUE INDEX ux_language_default ON language (is_default) WHERE is_default;

-- ---------------------------------------------------------------
-- Reference data (seed data)
-- ---------------------------------------------------------------

CREATE TABLE line (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code             varchar(20) NOT NULL UNIQUE,
    name             varchar(100) NOT NULL,  -- default language (English)
    operations_phone varchar(30)            -- line desk number for phone fallback
);

CREATE TABLE line_translation (
    line_id        uuid       NOT NULL REFERENCES line(id) ON DELETE CASCADE,
    language_code  varchar(5) NOT NULL REFERENCES language(code),
    name           varchar(100) NOT NULL,
    PRIMARY KEY (line_id, language_code)
);

CREATE TABLE station (
    id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code                  varchar(10) NOT NULL UNIQUE,
    name                  varchar(100) NOT NULL,  -- default language (English)
    contact_phone         varchar(30),
    has_catering_facility boolean NOT NULL DEFAULT false
);

CREATE TABLE platform (                          -- platforms at a station (FR-10, FR-57)
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    station_id  uuid NOT NULL REFERENCES station(id) ON DELETE CASCADE,
    code        varchar(10) NOT NULL,                -- e.g. '1', '2A'
    UNIQUE (station_id, code),
    UNIQUE (id, station_id)                          -- target for the composite key on service_stop
);

CREATE TABLE station_translation (
    station_id     uuid       NOT NULL REFERENCES station(id) ON DELETE CASCADE,
    language_code  varchar(5) NOT NULL REFERENCES language(code),
    name           varchar(100) NOT NULL,
    PRIMARY KEY (station_id, language_code)
);

CREATE TABLE line_station (                      -- stations on a line, with distance along it
    line_id      uuid NOT NULL REFERENCES line(id) ON DELETE CASCADE,
    station_id   uuid NOT NULL REFERENCES station(id),
    sequence     int  NOT NULL CHECK (sequence > 0),
    distance_km  numeric(6,1) NOT NULL CHECK (distance_km >= 0),  -- from the line origin
    PRIMARY KEY (line_id, station_id),
    UNIQUE (line_id, sequence)
);

CREATE TABLE train_service (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    train_number  varchar(10) NOT NULL,      -- no longer unique alone: extra dates and later versions share a number
    line_id       uuid        NOT NULL REFERENCES line(id),
    service_type  varchar(20) NOT NULL CHECK (service_type IN ('Slow', 'Express')),
    service_category varchar(20) NOT NULL DEFAULT 'Regional'
                  CHECK (service_category IN ('Regional', 'LongDistance')),  -- push threshold 5 / 10 min (A-07c)
    direction     varchar(10) NOT NULL CHECK (direction IN ('Outbound', 'Inbound')),  -- away from / toward the line's first station
    runs_on       varchar(50) NOT NULL,      -- service calendar, e.g. 'Weekdays'
    unreserved_capacity int NOT NULL DEFAULT 0 CHECK (unreserved_capacity >= 0),  -- D-67: tickets sold without a seat (long-distance)
    kind          varchar(10) NOT NULL DEFAULT 'Weekly' CHECK (kind IN ('Weekly', 'Extra')),   -- spec 13
    version       int  NOT NULL DEFAULT 1 CHECK (version >= 1),                                  -- spec 14: amend inserts version N+1
    effective_from date NOT NULL DEFAULT DATE '2000-01-01',
    effective_to  date,                                   -- null = still in force; an amend ends the old version the day before
    superseded_service_id uuid REFERENCES train_service(id),   -- the version this one replaced
    UNIQUE (train_number, version, effective_from)
);
CREATE INDEX ix_train_service_line_kind_effective ON train_service (line_id, kind, effective_from);
CREATE INDEX ix_train_service_superseded ON train_service (superseded_service_id);

CREATE TABLE service_cancellation (               -- spec 14: a train number cancelled on a line for a date range (nothing is deleted)
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    service_id    uuid NOT NULL REFERENCES train_service(id),
    line_id       uuid NOT NULL REFERENCES line(id),
    train_number  varchar(10) NOT NULL,
    from_date     date NOT NULL,
    to_date       date,                               -- null = onward
    reason        varchar(200) NOT NULL,              -- for controllers only, never shown to passengers
    created_at    timestamptz NOT NULL,
    created_by    varchar(64) NOT NULL,
    CHECK (to_date IS NULL OR to_date >= from_date)
);
CREATE INDEX ix_service_cancellation_service ON service_cancellation (service_id);
CREATE INDEX ix_service_cancellation_line_train_from ON service_cancellation (line_id, train_number, from_date);

CREATE TABLE service_change_log (                 -- specs 13, 14: audit of create, amend and cancel
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    service_id  uuid NOT NULL REFERENCES train_service(id),
    action      varchar(12) NOT NULL CHECK (action IN ('Created', 'Amended', 'Cancelled')),
    actor       varchar(64) NOT NULL,
    at          timestamptz NOT NULL,
    details     jsonb                                  -- for Amended: the changes that feed the version summary
);
CREATE INDEX ix_service_change_log_service ON service_change_log (service_id);

CREATE TABLE service_stop (
    id                       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    service_id               uuid NOT NULL REFERENCES train_service(id) ON DELETE CASCADE,
    station_id               uuid NOT NULL REFERENCES station(id),
    sequence                 int  NOT NULL CHECK (sequence > 0),
    scheduled_arrival        time,              -- null at origin
    scheduled_departure      time,              -- null at destination
    planned_platform_id      uuid,              -- optional; must be a platform of this stop's station
    arrival_report_required  boolean NOT NULL DEFAULT false,  -- D-08
    collection_eligible      boolean NOT NULL DEFAULT false,  -- D-15
    UNIQUE (service_id, sequence),
    UNIQUE (service_id, station_id),
    FOREIGN KEY (planned_platform_id, station_id) REFERENCES platform (id, station_id)
);

-- ---------------------------------------------------------------
-- Operational data (owned by journeys, ADR-11)
-- ---------------------------------------------------------------

CREATE TABLE journey (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    service_id              uuid NOT NULL REFERENCES train_service(id),
    operating_date          date NOT NULL,
    status                  varchar(20) NOT NULL DEFAULT 'Unassigned'
                            CHECK (status IN ('Unassigned', 'Scheduled', 'Running', 'Completed', 'Cancelled')),  -- Cancelled: spec 14
    version                 int  NOT NULL DEFAULT 0 CHECK (version >= 0),   -- ADR-09
    current_delay_minutes   int  NOT NULL DEFAULT 0,
    expected_next_event_by  timestamptz,                                    -- overdue detection
    is_overdue              boolean NOT NULL DEFAULT false,
    UNIQUE (service_id, operating_date)
);
CREATE INDEX ix_journey_operating_date ON journey (operating_date);

CREATE TABLE operator_assignment (               -- MP-19: time-bounded
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    journey_id        uuid NOT NULL REFERENCES journey(id) ON DELETE CASCADE,
    operator_user_id  varchar(64) NOT NULL,      -- Keycloak subject
    valid_from        timestamptz NOT NULL,
    valid_to          timestamptz,               -- null = until journey end
    assigned_by       varchar(64) NOT NULL,
    ended_by          varchar(64),               -- spec 17: who ended it early
    end_reason        varchar(15) CHECK (end_reason IN ('LostDevice', 'CrewChange', 'Other')),
    end_note          varchar(200),
    CHECK (valid_to IS NULL OR valid_to > valid_from),
    CHECK (end_reason IS NULL OR ended_by IS NOT NULL)
);
CREATE INDEX ix_assignment_journey ON operator_assignment (journey_id);
CREATE INDEX ix_assignment_operator ON operator_assignment (operator_user_id);

-- DESIGN ONLY, NOT BUILT (FR-125, D-74)
CREATE TABLE controller_assignment (             -- FR-125, D-74 (FR-126, D-75 senior-controller reassign is future work, D-76): the controller in charge (assigned_by is the actor, a senior controller when reassigned) of a journey, time-bounded like operator_assignment
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    journey_id          uuid NOT NULL REFERENCES journey(id) ON DELETE CASCADE,
    controller_user_id  varchar(64) NOT NULL,    -- Keycloak subject
    valid_from          timestamptz NOT NULL,
    valid_to            timestamptz,             -- null = until handed over or the journey ends
    assigned_by         varchar(64) NOT NULL,    -- the controller who took charge, or handed over
    handover_note       varchar(500),            -- optional (FR-58)
    CHECK (valid_to IS NULL OR valid_to > valid_from)
);
-- At most one controller in charge of a journey at a time
CREATE UNIQUE INDEX ux_controller_in_charge ON controller_assignment (journey_id) WHERE valid_to IS NULL;
CREATE INDEX ix_controller_assignment_user ON controller_assignment (controller_user_id);

CREATE TABLE journey_stop_state (
    journey_id           uuid NOT NULL REFERENCES journey(id) ON DELETE CASCADE,
    service_stop_id      uuid NOT NULL REFERENCES service_stop(id),
    estimated_arrival    timestamptz,
    estimated_departure  timestamptz,
    actual_arrival       timestamptz,
    actual_departure     timestamptz,
    platform_id          uuid REFERENCES platform(id),  -- current; null = planned platform applies. Same-station rule is enforced in the application (tested)
    confirmation         varchar(10) NOT NULL DEFAULT 'Pending'
                         CHECK (confirmation IN ('Pending', 'Reported', 'Inferred')),
    PRIMARY KEY (journey_id, service_stop_id)
);

CREATE TABLE train_event (                       -- append-only audit trail (D-36)
    id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    journey_id             uuid NOT NULL REFERENCES journey(id),
    client_event_id        uuid NOT NULL,                       -- D-35
    event_type             varchar(30) NOT NULL
                           CHECK (event_type IN ('Departed', 'Arrived', 'DelayReported', 'ReasonVerified',
                                                 'OverrideSet', 'OverrideCleared', 'PlatformChanged',
                                                 'ReportFlagged')),
    service_stop_id        uuid REFERENCES service_stop(id),
    occurred_at            timestamptz NOT NULL,                -- device, server-corrected
    received_at            timestamptz NOT NULL DEFAULT now(),  -- server
    source                 varchar(15) NOT NULL CHECK (source IN ('Operator', 'Controller', 'Simulator')),
    created_by             varchar(64) NOT NULL,                -- Keycloak subject
    payload                jsonb,
    journey_version_after  int NOT NULL,
    UNIQUE (journey_id, client_event_id)
);
CREATE INDEX ix_event_journey_occurred ON train_event (journey_id, occurred_at);

-- DESIGN ONLY, NOT BUILT (spec 16 F3)
CREATE TABLE journey_override (                  -- ADR-05: active until cleared
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    journey_id     uuid NOT NULL REFERENCES journey(id),
    type           varchar(15) NOT NULL CHECK (type IN ('Suspended', 'Cancelled', 'Terminated')),
    at_station_id  uuid REFERENCES station(id),
    reason_code    varchar(30) NOT NULL,
    set_at         timestamptz NOT NULL DEFAULT now(),
    set_by         varchar(64) NOT NULL,
    cleared_at     timestamptz,
    cleared_by     varchar(64)
);
-- At most one active override per journey
CREATE UNIQUE INDEX ux_override_active ON journey_override (journey_id) WHERE cleared_at IS NULL;

CREATE TABLE journey_exception (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    journey_id        uuid NOT NULL REFERENCES journey(id),
    related_event_id  uuid REFERENCES train_event(id),
    station_id        uuid REFERENCES station(id),   -- where the train was when raised (spec 07); null if nothing reported yet
    station_id        uuid REFERENCES station(id),   -- where the train was last reported (null before any report)
    -- related_journey_id and platform_id (platform conflict, FR-57): DESIGN ONLY, NOT BUILT (spec 16 F4)
    type              varchar(40) NOT NULL
                      CHECK (type IN ('ReasonAwaitingVerification', 'EarlyDeparture')),   -- built; Overdue, PlatformConflict ... will widen this check
    severity          varchar(10) NOT NULL CHECK (severity IN ('Critical', 'High')),   -- D-44
    created_at        timestamptz NOT NULL DEFAULT now(),
    -- owner_user_id (MP-13, Should): NOT BUILT
    resolved_at       timestamptz,
    resolved_by       varchar(64),               -- Keycloak subject; the audit key
    resolved_by_name  varchar(64),               -- user name at resolve time, display only
    resolution        varchar(200)               -- Acknowledged, NoActionNeeded, Escalated, ReasonVerified, ReasonChanged, Rejected
);
-- One report raises at most one exception of a type
CREATE UNIQUE INDEX ux_exception_event_type ON journey_exception (related_event_id, type);
-- Open exceptions queue: severity, then age
CREATE INDEX ix_exception_open ON journey_exception (severity, created_at) WHERE resolved_at IS NULL;
-- One report, one exception of a type (spec 07): a replayed report cannot raise it twice
CREATE UNIQUE INDEX ux_exception_event_type ON journey_exception (related_event_id, type);

-- DESIGN ONLY, NOT BUILT (spec 16 F5, F6)
CREATE TABLE disruption_message (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    journey_id    uuid REFERENCES journey(id),
    station_id    uuid REFERENCES station(id),
    line_id       uuid REFERENCES line(id),      -- optional: a message about a whole line (FR-107)
    template_id   varchar(50) NOT NULL,          -- translated per language
    parameters    jsonb,
    published_at  timestamptz NOT NULL DEFAULT now(),
    published_by  varchar(64) NOT NULL,
    withdrawn_at  timestamptz
);

-- ---------------------------------------------------------------
-- Integration (ADR-12, ADR-13)
-- ---------------------------------------------------------------

-- D-72 (12 Oct 2026): the outbox is designed, not built in the MVP (no train events go to the caterer).
-- DESIGN ONLY, NOT BUILT
CREATE TABLE outbox_message (                    -- transactional outbox
    message_id       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    journey_id       uuid REFERENCES journey(id),
    message_type     varchar(40) NOT NULL,
    payload          jsonb NOT NULL,
    journey_version  int,
    attempt_count    int NOT NULL DEFAULT 0,
    next_attempt_at  timestamptz NOT NULL DEFAULT now(),
    status           varchar(10) NOT NULL DEFAULT 'Pending'
                     CHECK (status IN ('Pending', 'Delivered', 'Failed')),
    created_at       timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_outbox_pending ON outbox_message (next_attempt_at) WHERE status = 'Pending';

-- DESIGN ONLY, NOT BUILT (SmartRail pulls the menu from SmallBites; there is no feed, so no inbox)
CREATE TABLE inbox_message (                     -- caterer messages already processed (MVP: menu messages; order status messages are deferred, D-71)
    message_id   uuid PRIMARY KEY,
    received_at  timestamptz NOT NULL DEFAULT now()
);

-- DEFERRED (D-71): designed, not built. Not created in the MVP; kept for a later phase.
CREATE TABLE order_reference (
    order_id                varchar(64) PRIMARY KEY,          -- caterer's order ID
    journey_id              uuid NOT NULL REFERENCES journey(id),
    collection_stop_id      uuid NOT NULL REFERENCES service_stop(id),
    device_installation_id  varchar(64) NOT NULL,             -- anonymous (review question 2)
    status                  varchar(20) NOT NULL
                            CHECK (status IN ('Pending', 'AwaitingPayment', 'Expired', 'Rejected',
                                              'Confirmed', 'Ready', 'Collected', 'NotCollected', 'Refunded')),
    rejection_reason        varchar(200),
    order_version           int NOT NULL DEFAULT 0,           -- INT-09
    updated_at              timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_order_device ON order_reference (device_installation_id);
CREATE INDEX ix_order_journey ON order_reference (journey_id);

-- DESIGN ONLY, NOT BUILT (push, FR-75/76)
CREATE TABLE train_subscription (                -- D-31: deleted after the journey
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    journey_id  uuid NOT NULL REFERENCES journey(id) ON DELETE CASCADE,
    push_token  varchar(512) NOT NULL,
    created_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (journey_id, push_token)
);

-- DESIGN ONLY, NOT BUILT (the menu is read live from SmallBites and kept in memory, spec 11 section 14)
CREATE TABLE menu_item (                         -- cached caterer menu (source of truth: caterer); shown as a read-only list (FR-90), updated_at gives 'Menu from [time]'
    item_id     varchar(64) PRIMARY KEY,
    station_id  uuid NOT NULL REFERENCES station(id),
    name        varchar(100) NOT NULL,                       -- default language (English)
    price       numeric(10,2) NOT NULL CHECK (price >= 0),   -- display only
    available   boolean NOT NULL DEFAULT true,
    updated_at  timestamptz NOT NULL DEFAULT now()
);

-- DESIGN ONLY, NOT BUILT
CREATE TABLE menu_item_translation (              -- names supplied by the caterer per language
    item_id        varchar(64) NOT NULL REFERENCES menu_item(item_id) ON DELETE CASCADE,
    language_code  varchar(5)  NOT NULL REFERENCES language(code),
    name           varchar(100) NOT NULL,
    description    varchar(300),
    PRIMARY KEY (item_id, language_code)
);

-- ---------------------------------------------------------------
-- Ticketing and passenger accounts (Should, D-60, ADR-14)
-- Built only if the Must slices are done. Cards never appear here:
-- the payment provider holds them; SmartRail keeps a token reference
-- and display details only (D-63, NFR-48). Names and email stay in the
-- identity provider (NFR-49).
-- ---------------------------------------------------------------

-- DESIGN ONLY, NOT BUILT: everything from here to the end (ticketing, Should, D-60) has no migration.
CREATE EXTENSION IF NOT EXISTS btree_gist;       -- for the seat exclusion constraint below

CREATE TABLE fare_rate (                         -- A-26: seeded rate card
    service_category  varchar(12) PRIMARY KEY
                      CHECK (service_category IN ('Regional', 'LongDistance')),
    price_per_km      numeric(8,2) NOT NULL CHECK (price_per_km >= 0),
    minimum_fare      numeric(8,2) NOT NULL CHECK (minimum_fare >= 0)
);

CREATE TABLE passenger_account (                 -- only the identity provider's account ID
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    identity_subject  varchar(64) NOT NULL UNIQUE,
    created_at        timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE saved_card (                        -- optional; token reference and display details only
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id      uuid NOT NULL REFERENCES passenger_account(id) ON DELETE CASCADE,
    provider_token  varchar(100) NOT NULL,
    brand           varchar(20) NOT NULL,
    last4           char(4) NOT NULL CHECK (last4 ~ '^[0-9]{4}$'),
    exp_month       smallint NOT NULL CHECK (exp_month BETWEEN 1 AND 12),
    exp_year        smallint NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (account_id, provider_token)
);

CREATE TABLE coach (                             -- seat layout of a long-distance service (seed data)
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    service_id    uuid NOT NULL REFERENCES train_service(id) ON DELETE CASCADE,
    coach_number  int NOT NULL CHECK (coach_number > 0),
    UNIQUE (service_id, coach_number)
);

CREATE TABLE seat (
    id        uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    coach_id  uuid NOT NULL REFERENCES coach(id) ON DELETE CASCADE,
    label     varchar(4) NOT NULL,               -- for example 12A
    channel   varchar(7) NOT NULL DEFAULT 'App'  -- D-68: 'Counter' seats are never offered in the app
              CHECK (channel IN ('App', 'Counter')),
    UNIQUE (coach_id, label)
);

CREATE TABLE purchase (                          -- one payment, one or more tickets
    id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id           uuid REFERENCES passenger_account(id) ON DELETE SET NULL,  -- link removed on deletion or after retention (NFR-49)
    status               varchar(16) NOT NULL
                         CHECK (status IN ('AwaitingPayment', 'Paid', 'Declined', 'Expired')),
    total_amount         numeric(10,2) NOT NULL CHECK (total_amount >= 0),
    provider_payment_ref varchar(64) UNIQUE,
    payment_version      int NOT NULL DEFAULT 0,  -- NFR-51: ignore older provider messages
    saved_card_id        uuid REFERENCES saved_card(id) ON DELETE SET NULL,
    created_at           timestamptz NOT NULL DEFAULT now(),
    updated_at           timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE ticket (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    purchase_id             uuid NOT NULL REFERENCES purchase(id) ON DELETE CASCADE,
    ticket_type             varchar(12) NOT NULL
                            CHECK (ticket_type IN ('Regional', 'LongDistance')),   -- passengers see "Metro" for Regional
    journey_id              uuid REFERENCES journey(id),                          -- long-distance only
    service_date            date NOT NULL,
    origin_station_id       uuid NOT NULL REFERENCES station(id),
    destination_station_id  uuid NOT NULL REFERENCES station(id),
    fare_amount             numeric(10,2) NOT NULL CHECK (fare_amount >= 0),       -- the quoted fare (AC-E11-01)
    status                  varchar(20) NOT NULL
                            CHECK (status IN ('Held', 'Paid', 'Expired', 'CancelledByRailway',
                                              'Cancelled', 'Refunded')),
    ticket_code             varchar(32) NOT NULL UNIQUE,                           -- shown offline (FR-116)
    hold_expires_at         timestamptz,
    refund_ref              varchar(64),
    created_at              timestamptz NOT NULL DEFAULT now(),
    updated_at              timestamptz NOT NULL DEFAULT now(),
    CHECK (origin_station_id <> destination_station_id),
    CHECK ((ticket_type = 'LongDistance') = (journey_id IS NOT NULL))
);

CREATE TABLE seat_reservation (                  -- a seat for a stop range of one journey
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id      uuid NOT NULL REFERENCES ticket(id) ON DELETE CASCADE,
    journey_id     uuid NOT NULL REFERENCES journey(id),
    seat_id        uuid NOT NULL REFERENCES seat(id),
    from_sequence  int NOT NULL CHECK (from_sequence > 0),   -- boarding stop (service_stop.sequence)
    to_sequence    int NOT NULL,                              -- alighting stop
    active         boolean NOT NULL DEFAULT true,             -- false when expired, cancelled or refunded
    CHECK (to_sequence > from_sequence),
    -- D-64, NFR-47: the database refuses overlapping sales of one seat on one journey.
    -- [from, to) ranges, so a seat sold 1 to 3 can be sold again 3 to 5.
    EXCLUDE USING gist (journey_id WITH =, seat_id WITH =,
                        int4range(from_sequence, to_sequence) WITH &&) WHERE (active)
);

CREATE INDEX ix_purchase_account ON purchase (account_id, created_at DESC);   -- purchase history (FR-115)
CREATE INDEX ix_ticket_purchase  ON ticket (purchase_id);
CREATE INDEX ix_ticket_journey   ON ticket (journey_id) WHERE journey_id IS NOT NULL;   -- refund on cancellation (FR-117)
