#!/usr/bin/env bash
# Runs the Flutter integration tests (the integration_test package) of the passenger and operator apps on the Android emulator, against the
# real stack: the real API, and for the operator app the real Keycloak. Run from anywhere in Git Bash.
#
#   ./scripts/mobile-integration.sh              both apps
#   ./scripts/mobile-integration.sh passenger    one app: passenger or operator
#
# Needs: Docker stack up (./start.sh, or ./scripts/reset-db.sh --demo; the operator test needs the demo layer or it skips itself), an Android
# emulator running (./start.sh --emulator starts Pixel_6) or a phone, and Flutter. The first build per app takes a few minutes.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

APPS=(passenger operator)
[[ $# -ge 1 ]] && APPS=("$1")

curl -fs http://localhost:8080/health/ready >/dev/null 2>&1 || { echo "The API does not answer on :8080. Start the stack first." >&2; exit 1; }
DEVICE="$(adb devices | awk '/emulator-[0-9]+[[:space:]]+device/ {print $1; exit}')"
[[ -n "$DEVICE" ]] || { echo "No Android emulator is running. Start one: ./start.sh --emulator (or the Pixel_6 AVD)." >&2; exit 1; }

# The apps reach the host as localhost through these forwards.
adb -s "$DEVICE" reverse tcp:8080 tcp:8080 >/dev/null
adb -s "$DEVICE" reverse tcp:8180 tcp:8180 >/dev/null

status=0
for app in "${APPS[@]}"; do
  case "$app" in passenger|operator) ;; *) echo "Unknown app: $app (use passenger or operator)"; exit 2 ;; esac
  echo "==> ${app}_app integration test on $DEVICE"
  (cd "$ROOT/mobile/${app}_app" && flutter test integration_test --device-id "$DEVICE") || status=1
done
exit $status
