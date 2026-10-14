# SmartRail – Container Diagram (MVP)

Reflects ADR-06 to ADR-13. Dashed lines are designed but not built in the MVP. Updated 12 October 2026 (D-71, D-72): food ordering is designed but not built; the passenger app lists the menu and links to the caterer's website.

```mermaid
flowchart TB
    subgraph People
        K["Kasuni<br/>Passenger"]
        N["Nimal<br/>Train Operator"]
        J["Jayasinghe<br/>Operations Controller"]
        SP["Passengers at stations"]
        H["Caterer handler"]
    end

    subgraph Clients["SmartRail client applications"]
        PA["Passenger App<br/>Flutter, anonymous"]
        OA["Operator App<br/>Flutter, offline queue"]
        OW["Operations Web App<br/>React"]
        SD["Station Displays<br/>React, main board and platform modes"]
        SIM["Simulator<br/>.NET scenario runner"]
    end

    subgraph SR["SmartRail API - ASP.NET Core, one process"]
        REST["REST API<br/>snapshots, events, menu"]
        HUBS["SignalR hubs<br/>Public, Operator, Operations"]
        CORE["Validation and ETA engine"]
        BG["Hosted services<br/>Overdue detection, Journey generator,<br/>Push sender"]
    end

    subgraph DATA["PostgreSQL server"]
        SRDB[("SmartRail DB<br/>Services, ServiceStops, Journeys,<br/>JourneyStopState, TrainEvents,<br/>Overrides, Exceptions, Outbox,<br/>MenuItems, Subscriptions")]
        KCDB[("Keycloak DB")]
    end

    KC["Keycloak<br/>Identity provider and admin console"]
    FCM["Firebase Cloud Messaging<br/>external service"]

    subgraph CAT["Simulated Catering Provider - separate system"]
        CAPI["Caterer API<br/>.NET"]
        CUI["Caterer UI<br/>Stand-in website, menu toggles"]
        CDB[("Caterer DB<br/>Menu")]
    end

    %% People to applications
    K --> PA
    N --> OA
    J --> OW
    SP --> SD
    H --> CUI

    %% Passenger app
    PA -->|"REST: snapshots, menu"| REST
    HUBS -->|"live updates, public hub"| PA
    PA -->|"button opens the caterer website"| CUI
    FCM -->|"push notifications"| PA
    PA -.->|"Should: optional account"| KC

    %% Station displays
    SD -->|"REST: snapshots"| REST
    HUBS -->|"live updates, public hub"| SD

    %% Operator app and simulator
    OA -->|"sign in, PKCE"| KC
    OA -->|"REST: report events, client event ID"| REST
    HUBS -->|"assigned journey updates, operator hub"| OA
    SIM -->|"sign in as seeded operator"| KC
    SIM -->|"REST: same API as operator app"| REST

    %% Operations web app
    OW -->|"sign in, PKCE"| KC
    OW -->|"REST: verify, override, publish, enter event"| REST
    HUBS -->|"exceptions and updates, operations hub"| OW

    %% Inside SmartRail API
    REST -->|"validate tokens"| KC
    REST --> CORE
    CORE -->|"event + state, one transaction (outbox designed, not built)"| SRDB
    CORE -->|"broadcast after commit"| HUBS
    BG --> SRDB
    BG -->|"push requests"| FCM

    %% Catering integration
    REST -.->|"designed, not built: forward order with journey and station"| CAPI
    BG -.->|"designed, not built (D-72): train events, outbox with retries"| CAPI
    CAPI -->|"webhooks: menu availability"| REST
    CAPI -.->|"designed, not built: order status"| REST
    CAPI -->|"client credentials token"| KC
    BG -.->|"designed, not built (D-72): token to call caterer"| KC
    CUI --> CAPI
    CAPI --> CDB

    %% Identity storage
    KC --> KCDB
```

## Notes

- **Containers vs components:** validation, the ETA engine, the SignalR hubs, and the hosted services run inside the SmartRail API process (MVP simplification). They could be split into a separate worker later.
- **Only direct passenger-to-caterer link:** the button that opens the caterer's website (D-71). SmartRail never handles orders or payment data. Before D-71 this was the hosted payment page (ADR-13); that design is kept, not built.
- **Every webhook message** carries a message ID (idempotent receivers). In the MVP only the caterer's menu messages exist; train events carry the journey version (out-of-order protection) when built (D-72).
- **System-to-system authentication (ADR-12):** both directions use Keycloak client credentials. SmartRail and the caterer are each confidential clients; each obtains a token to call the other, and each validates the other's tokens against Keycloak. One identity provider, one mechanism.
- **Seed data and journeys:** the journey generator uses the controllable clock's date (ADR-06).
