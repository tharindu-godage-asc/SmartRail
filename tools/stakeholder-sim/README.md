# SmartRail stakeholder simulation

This TypeScript tool runs discovery-grounded stakeholder discussions, synthesizes
one structured finding, and optionally lets a reviewer accept, edit, or reject it.

## Source of truth

[`context/smartrail-discovery.md`](context/smartrail-discovery.md) is the canonical
source for product facts, decisions, assumptions, priorities, and MVP boundaries.
The panel and smoke test always load this file. Each stakeholder prompt also
includes that role's Markdown persona as a concise perspective; if a persona and
the discovery ever conflict, the discovery takes precedence. Decision categories
are read from the discovery decision log rather than maintained separately.

Personas live in [`personas/`](personas/). The discovery distinguishes actual
MVP actors from design-only perspectives and explicitly excluded roles; the
persona files preserve those distinctions.

## Setup

Requires Node.js 22.12 or newer in a supported release line and npm.

From this directory:

```powershell
npm install
Copy-Item .env.example .env
```

Choose a provider with `LLM_PROVIDER=groq` or `LLM_PROVIDER=gemini` in `.env`,
or per run with `--provider groq`. Add one key or several comma-separated keys
for that provider:

```
GROQ_API_KEYS=key1,key2,key3      # model: GROQ_MODEL (default qwen/qwen3.8-27b)
GEMINI_API_KEYS=key1,key2,key3    # model: GEMINI_MODEL (default gemini-2.5-flash)
```

When a key hits its daily quota the tool switches to the next one. Single-key
`GROQ_API_KEY`, `GEMINI_API_KEY` and `GOOGLE_API_KEY` also work. If your
Gemini key cannot use the default model, set `GEMINI_MODEL=gemini-3.8-flash`.

## Generate transcripts one persona at a time

Persona slugs: `passenger`, `driver`, `station-manager`, `railway-controller`, `customer-service-representative`, `catering-provider`, `security-engineer`, `qa-engineer`, `accessibility-specialist`, `product-owner`.

Free tiers are tight (Gemini allows about 20 requests per day per model; Groq has per-minute and per-day token limits), so
`npm run transcripts` is resumable and spends requests only on work that is not
done yet. Progress is saved in `transcripts/_state.json`; keep the same topic
between runs, because a different topic starts a fresh run.

```powershell
# 1. Start a run: the topic is given once, with the first persona
npm run transcripts -- "Passenger-facing updates during an outage" --provider groq --role passenger

# 2. Then each remaining persona, one at a time (topic is remembered)
npm run transcripts -- --provider groq --role driver
npm run transcripts -- --provider groq --role station-manager
npm run transcripts -- --provider groq --role railway-controller
npm run transcripts -- --provider groq --role customer-service-representative
npm run transcripts -- --provider groq --role catering-provider
npm run transcripts -- --provider groq --role security-engineer
npm run transcripts -- --provider groq --role qa-engineer
npm run transcripts -- --provider groq --role accessibility-specialist
npm run transcripts -- --provider groq --role product-owner

# Or let the script pick the next unfinished personas
npm run transcripts -- --provider groq --next 3
npm run transcripts -- --provider groq          # everything still pending

# Several specific personas in one go
npm run transcripts -- --provider groq --role qa-engineer --role security-engineer

# Switch provider or start from a specific key (1 = first key in the list)
npm run transcripts -- --provider gemini --next 2
npm run transcripts -- --provider groq --next 2 --key 2

# Show progress per persona
npm run transcripts -- --list
```

### One pain point per persona

`--pain` makes a persona respond to a single pain point from `smartrail-pain-points.md` (one request per persona, no panel question and no challenge). It needs exactly one `--role` and does not touch other personas' saved results. Running the same persona again with the same pain point is free (it is skipped); a different pain point replaces that persona's transcript.

| Persona | Pain point ID (discovery refs) | Summary |
|---|---|---|
| `passenger` | PP-PASSEN-01 (D-39, P-02) | Cannot tell whether the train information shown is current |
| `driver` | PP-DRIVER-01 (A-03, D-03) | An app that demands attention while driving is a safety risk |
| `station-manager` | PP-STATIO-02 (D-12) | A platform change must reach passengers quickly |
| `railway-controller` | PP-RAILWA-03 (FR-36, FR-37, section 8) | The controller cannot tell which source is right when operator |
| `customer-service-representative` | PP-CUSTOM-02 (D-10, FR-50) | Cannot tell whether a delay is verified or merely reported when answering passenger enquiries. |
| `catering-provider` | PP-CATERI-02 (D-14, brief section 12) | Food is wasted when trains are late or cancelled because preparation was based on the schedule rather than the live train position. |
| `security-engineer` | PP-SECURI-01 (D-28, D-36) | False operational data such as fake delays or platforms could send passengers to the wrong place. |
| `qa-engineer` | PP-QAENGI-02 (D-09, D-30) | Offline and out-of-order reporting scenarios are hard to reproduce and test reliably. |
| `accessibility-specialist` | PP-ACCESS-02 (D-54) | State changes are not announced to screen readers |
| `product-owner` | PP-PRODUC-07 (D-33) | Must-have scope is about three times the available build capacity |

```powershell
npm run transcripts -- --provider groq --role passenger --pain "Cannot tell whether the train information shown is current; RDMNS showed updates 30+ minutes old and passengers still act on it."
npm run transcripts -- --provider groq --role driver --pain "An app that demands attention while driving is a safety risk, so the driver is not a SmartRail user and reporting moves to the on-train operator."
npm run transcripts -- --provider groq --role station-manager --pain "A platform change must reach passengers quickly, but station staff cannot make it in the system because the controller owns platform changes in the MVP."
npm run transcripts -- --provider groq --role railway-controller --pain "The controller cannot tell which source is right when operator, phone and simulator reports conflict."
npm run transcripts -- --provider groq --role customer-service-representative --pain "Cannot tell whether a delay is verified or merely reported when answering passenger enquiries."
npm run transcripts -- --provider groq --role catering-provider --pain "Food is wasted when trains are late or cancelled because preparation was based on the schedule rather than the live train position."
npm run transcripts -- --provider groq --role security-engineer --pain "False operational data such as fake delays or platforms could send passengers to the wrong place."
npm run transcripts -- --provider groq --role qa-engineer --pain "Offline and out-of-order reporting scenarios are hard to reproduce and test reliably."
npm run transcripts -- --provider groq --role accessibility-specialist --pain "State changes are not announced to screen readers, or every routine update is announced and the screen becomes unusable."
npm run transcripts -- --provider groq --role product-owner --pain "Must-have scope is about three times the available build capacity, so cuts must be decided at the 9 October checkpoint."
```

That is 10 requests in total, one per persona.

Each persona writes `transcripts/<slug>.md` with its response and a challenge
to the previous role in panel order (Passenger, the first role, has no
challenge). A full run is 1 question + 10 responses + 9 challenges = 20
requests. It stops with a clear message once all keys are exhausted; add keys
and rerun to continue.

## Run a panel

From this directory, run one focused topic:

```powershell
npm run panel -- "Passenger-facing updates during an outage"
```

The facilitator formulates one question, each selected role responds, each
challenges the next role, and the synthesizer produces a finding. By default all
ten role perspectives participate. The command writes one Markdown file per
persona, containing its response and challenge, plus `_panel-summary.md` under
`transcripts/`.

## Review and export

```powershell
npm run review -- "Offline delay reporting and journey order" --roles "QA Engineer,Passenger"
```

The review accepts, edits, or rejects the synthesized finding. Accepted and
edited findings are appended to `output/findings.csv`; every completed review
saves a JSON transcript under `output/transcripts/`. Rejected findings are
recorded in the transcript but do not change the CSV.

Use `npm run review -- --help` to see the available roles. A reviewed panel
requires at least two roles.

## Smoke test

```powershell
npm run smoke
npm run smoke -- "What should be verified about stale passenger information?"
```

## Checks

```powershell
npm test
npm run typecheck
```

The tests use injected fake model responses and do not call Gemini or require an
API key.


# start a run for a topic and do just the first persona
npm run transcripts -- "Passenger-facing updates during an outage" --role passenger

# one at a time, by role slug
npm run transcripts -- --role driver
npm run transcripts -- --role station-manager

# or the next N unfinished personas
npm run transcripts -- --next 3

# everything still pending
npm run transcripts

# check progress, or start on key #2
npm run transcripts -- --list
npm run transcripts -- --next 2 --key 2

