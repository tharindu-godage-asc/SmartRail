#!/usr/bin/env bash
# Runs the 5 critical flows (operator report to passenger, verify a delay reason, lost device, controller resolves a fault report, catering) in a visible browser,
# slowed down so a person can follow each step. Needs the stack (./start.sh, or ./scripts/reset-db.sh). Run from anywhere in Git Bash.
#
#   ./scripts/e2e-critical-demo.sh              headed, 700 ms after every browser action
#   ./scripts/e2e-critical-demo.sh --slow 1500  slower (milliseconds after every action)
#   ./scripts/e2e-critical-demo.sh --reset      afterwards, put the database back to its initial state (DELETES local data)
#   ./scripts/e2e-critical-demo.sh --flow 3     only one flow: 1, 2, 3, 4 (controller resolves a fault report) or 5 (catering)
#
# Many steps call the API directly (no browser), so the window is only busy on the controller screens. Each flow makes its own test data.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

SLOW=700; RESET=0; FLOW=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --slow) SLOW="${2:?--slow needs a number of milliseconds}"; shift 2 ;;
    --reset) RESET=1; shift ;;
    --flow) FLOW="${2:?--flow needs 1 to 5}"; shift 2 ;;
    *) echo "Unknown option: $1 (use --slow <ms>, --flow <1-5>, --reset)"; exit 2 ;;
  esac
done

if ! curl -fs http://localhost:8080/health/ready >/dev/null 2>&1; then
  echo "The API does not answer on :8080. Start the stack first: ./start.sh (or ./scripts/reset-db.sh for a clean one)." >&2
  exit 1
fi

cd "$ROOT/web/ops"
[[ -d node_modules ]] || npm ci
npx playwright install chromium >/dev/null 2>&1 || true

args=(e2e/critical-flows.spec.ts e2e/controller-flow.spec.ts -g "flow ${FLOW:-[1-5]}:")

echo "Running the critical flows headed, ${SLOW} ms after every action${FLOW:+ (flow $FLOW only)}."
SLOWMO="$SLOW" npx playwright test "${args[@]}" --headed --workers=1
status=$?

if [[ $RESET == 1 ]]; then
  "$ROOT/scripts/reset-db.sh" || status=1
fi
exit $status
