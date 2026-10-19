#!/usr/bin/env bash
# Brings up everything for local work: the Docker Compose stack (API, Postgres, Keycloak, displays, observability),
# the controller web app (dev server), and the emulator port forwarding. Run from the repo root in Git Bash.
#   ./start.sh                start the stack and the web app
#   ./start.sh --emulator     also start the Pixel_6 emulator and run the passenger app
#   ./start.sh --operator     same, but run the operator app instead
#   ./start.sh --stop         stop the stack and the web dev server (data is kept)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

if [[ "${1:-}" == "--stop" ]]; then
  (cd smartrail-platform && docker compose stop)
  [[ -f .web-dev.pid ]] && kill "$(cat .web-dev.pid)" 2>/dev/null || true
  rm -f .web-dev.pid
  echo "Stopped."; exit 0
fi

docker info >/dev/null 2>&1 || { echo "Docker is not running. Start Docker Desktop first."; exit 1; }

echo "==> Docker Compose stack (first build takes a few minutes)"
(cd smartrail-platform && docker compose up -d --build)

echo "==> Waiting for the API"
for i in $(seq 1 60); do
  curl -fs http://localhost:8080/health/ready >/dev/null 2>&1 && break
  [[ $i == 60 ]] && { echo "API did not become ready. See: docker compose logs api"; exit 1; }
  sleep 3
done

echo "==> Controller web app (http://localhost:5173)"
if [[ ! -f .web-dev.pid ]] || ! kill -0 "$(cat .web-dev.pid)" 2>/dev/null; then
  [[ -d web/ops/node_modules ]] || (cd web/ops && npm install)
  (cd web/ops && nohup npm run dev >"$ROOT/web-dev.log" 2>&1 & echo $! >"$ROOT/.web-dev.pid")
fi

if [[ "${1:-}" == "--emulator" || "${1:-}" == "--operator" ]]; then
  if ! adb devices | grep -q "emulator-"; then
    echo "==> Starting emulator Pixel_6"
    nohup emulator -avd Pixel_6 >/dev/null 2>&1 &
    adb wait-for-device
    until [[ "$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" == "1" ]]; do sleep 3; done
  fi
  adb reverse tcp:8080 tcp:8080
  adb reverse tcp:8180 tcp:8180
  adb reverse tcp:8090 tcp:8090
  app=passenger_app; [[ "$1" == "--operator" ]] && app=operator_app
  echo "==> Running $app on the emulator"
  (cd "mobile/$app" && flutter pub get >/dev/null && flutter run -d emulator-5554 --no-resident)
fi

cat <<MSG

Ready.
  API                  http://localhost:8080  (e.g. /api/v1/trains?from=VYG&to=CF)
  Controller web app   http://localhost:5173  (jayasinghe / jayasinghe-dev-only)
  Station displays     http://localhost:5174/display/board/VYG
  SmallBites (caterer) http://localhost:8090/admin
  Keycloak             http://localhost:8180  (admin / admin-dev-only)
  Grafana              http://localhost:3000
Mobile: ./start.sh --emulator   Stop: ./start.sh --stop
MSG
