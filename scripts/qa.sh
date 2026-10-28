#!/usr/bin/env bash
# One command for the whole QA run: makes sure the stack is up, runs every test suite (unit, integration, SmallBites, web, both Flutter apps),
# then the browser end-to-end flows against the running stack, and ends with one PASS/FAIL table and a non-zero exit code if anything failed.
# Run from anywhere in Git Bash. Docker Desktop must be running.
#
#   ./scripts/qa.sh                  everything (about 15 minutes; the .NET integration tests are the slow part)
#   ./scripts/qa.sh --quick          skip the slow .NET integration tests
#   ./scripts/qa.sh --e2e-only       only the browser flows (needs the stack; starts it if it is down)
#   ./scripts/qa.sh --no-e2e         only the suites, no browser flows
#   ./scripts/qa.sh --no-coverage    skip coverage in the suites
#   ./scripts/qa.sh --no-reset       keep the test data after the browser flows (by default the database is reset to its initial state,
#                                    which DELETES the local Compose data: ./scripts/reset-db.sh)
#
# Results: test-report/index.html (suites, from scripts/test-report.sh), web/ops/playwright-report/ and test-results/ (browser flows, with traces
# of the failures), and test-report/qa-summary.txt (the table below). The same flows run on GitHub as the "qa" workflow (on pull requests and pushes to dev and main, nightly and by hand).
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

QUICK=0; COVERAGE=1; E2E=1; SUITES=1; RESET=1
for arg in "$@"; do
  case "$arg" in
    --no-reset) RESET=0 ;;
    --quick) QUICK=1 ;;
    --no-coverage) COVERAGE=0 ;;
    --no-e2e) E2E=0 ;;
    --e2e-only) SUITES=0 ;;
    *) echo "Unknown option: $arg (use --quick, --e2e-only, --no-e2e, --no-coverage, --no-reset)"; exit 2 ;;
  esac
done

mkdir -p test-report
declare -a NAMES=() RESULTS=() TIMES=()
record() { NAMES+=("$1"); RESULTS+=("$2"); TIMES+=("$3"); }
ts() { date +%H:%M:%S; }
say() { printf '\n[%s] ==> %s\n' "$(ts)" "$*"; }

run_step() { # name, command...
  local name="$1"; shift
  local began=$(date +%s)
  say "$name"
  if "$@"; then record "$name" PASS "$(( $(date +%s) - began ))s"; else record "$name" FAIL "$(( $(date +%s) - began ))s"; fi
}

stack_ready() { curl -fs http://localhost:8080/health/ready >/dev/null 2>&1; }

# ---- the stack, needed by the browser flows ----
if [[ $E2E == 1 ]]; then
  if stack_ready; then
    say "The stack is already up (API answers on :8080)"
  else
    say "Starting the stack with ./start.sh (the browser flows need the API, Keycloak, SmallBites and the web app)"
    ./start.sh || { echo "The stack did not start, so the browser flows cannot run."; record "Start the stack" FAIL "-"; E2E=0; }
  fi
fi

# ---- the suites ----
if [[ $SUITES == 1 ]]; then
  args=()
  [[ $QUICK == 1 ]] && args+=(--quick)
  [[ $COVERAGE == 0 ]] && args+=(--no-coverage)
  run_step "Suites: unit, integration, SmallBites, web, passenger app, operator app" ./scripts/test-report.sh "${args[@]}"
fi

# ---- the browser flows ----
if [[ $E2E == 1 ]]; then
  [[ -d web/ops/node_modules ]] || (cd web/ops && npm ci)
  # The browser itself (first time only; quiet when already installed).
  (cd web/ops && npx playwright install chromium >/dev/null 2>&1) || true
  # Playwright writes its HTML report to playwright-report/ and a traces folder for each failure.
  run_step "Browser flows: critical flows, timetable and controller (Playwright)" bash -c 'cd web/ops && QA=1 npm run e2e:qa'
  # The flows leave test trains behind (reports are append-only), so put the database back to its initial state.
  if [[ $RESET == 1 ]]; then
    run_step "Reset the database to its initial state" ./scripts/reset-db.sh
  fi
fi

# ---- the table ----
{
  echo
  echo "QA result  $(date '+%Y-%m-%d %H:%M')"
  echo "------------------------------------------------------------------"
  for i in "${!NAMES[@]}"; do printf '%-4s  %-8s  %s\n' "${RESULTS[$i]}" "${TIMES[$i]}" "${NAMES[$i]}"; done
  echo "------------------------------------------------------------------"
} | tee test-report/qa-summary.txt

[[ -f test-report/index.html ]] && echo "Suites report:        test-report/index.html"
[[ -d web/ops/playwright-report ]] && echo "Browser flow report:  web/ops/playwright-report/index.html  (npx playwright show-report, from web/ops)"

for r in "${RESULTS[@]}"; do [[ "$r" == FAIL ]] && exit 1; done
echo "All QA steps passed."
exit 0
