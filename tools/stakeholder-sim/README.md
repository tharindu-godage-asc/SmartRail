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

Set `GEMINI_API_KEY` in `.env`. The default model is `gemini-2.5-flash`; set
`GEMINI_MODEL` in `.env` to override it.

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
