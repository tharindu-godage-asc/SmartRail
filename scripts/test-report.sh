#!/usr/bin/env bash
# Runs every test suite of SmartRail with verbose output (each test name and PASS/FAIL), measures code coverage, and builds one HTML report.
# Run from anywhere in Git Bash. Docker Desktop must be running for the integration tests (they start Postgres containers).
#
#   ./scripts/test-report.sh                  everything, with coverage
#   ./scripts/test-report.sh --quick          skip the slow .NET integration tests (about 7 minutes)
#   ./scripts/test-report.sh --no-coverage    skip coverage
#   ./scripts/test-report.sh --only web       one suite: unit | integration | smallbites | web | passenger | operator
#
# Results go to test-report/ (git-ignored). Open test-report/index.html when it ends.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

QUICK=0; COVERAGE=1; ONLY=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --quick) QUICK=1 ;;
    --no-coverage) COVERAGE=0 ;;
    --only) ONLY="${2:-}"; shift ;;
    *) echo "Unknown option: $1"; exit 2 ;;
  esac
  shift
done

OUT="$ROOT/test-report"
rm -rf "$OUT"; mkdir -p "$OUT"
FAILED=0

want() { [[ -z "$ONLY" || "$ONLY" == "$1" ]]; }
banner() { printf '\n\033[1m==== %s ====\033[0m\n' "$1"; }

dotnet_suite() {
  local name="$1" project="$2"
  banner ".NET $name"
  local args=(test "$project" --logger "trx;LogFileName=$name.trx" --logger "console;verbosity=detailed" --results-directory "$OUT/$name")
  [[ $COVERAGE == 1 ]] && args+=(--collect:"XPlat Code Coverage")
  dotnet "${args[@]}" || FAILED=1
}

if want unit; then dotnet_suite unit smartrail-platform/tests/SmartRail.UnitTests; fi
if want integration && [[ $QUICK == 0 ]]; then dotnet_suite integration smartrail-platform/tests/SmartRail.IntegrationTests; fi
if want smallbites; then dotnet_suite smallbites smallbites/tests; fi

if want web; then
  banner "Web (vitest)"
  cov=()
  [[ $COVERAGE == 1 ]] && cov=(--coverage --coverage.reportOnFailure=true --coverage.reporter=json-summary --coverage.reportsDirectory=../../test-report/web-coverage)
  (cd web/ops && npx vitest run --maxWorkers=4 --testTimeout=30000 --reporter=verbose --reporter=json --outputFile.json=../../test-report/web.json "${cov[@]}") || FAILED=1
fi

flutter_suite() {
  local name="$1" dir="$2"
  banner "Flutter $name"
  local cov=()
  [[ $COVERAGE == 1 ]] && cov=(--coverage)
  (cd "$dir" && flutter test --reporter expanded --file-reporter "json:../../test-report/flutter-$name.json" "${cov[@]}") || FAILED=1
  [[ -f "$dir/coverage/lcov.info" ]] && cp "$dir/coverage/lcov.info" "$OUT/flutter-$name.lcov"
}

if want passenger; then flutter_suite passenger mobile/passenger_app; fi
if want operator; then flutter_suite operator mobile/operator_app; fi

banner "Report"
node scripts/test-report/build-report.mjs "$OUT" || FAILED=1
echo
echo "HTML report: $OUT/index.html"
exit $FAILED
