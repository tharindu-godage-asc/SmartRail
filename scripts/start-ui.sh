#!/usr/bin/env bash
# Starts the user interfaces for a demo: the Operations Dashboard (Vite dev server), the station displays (platform displays and main
# boards), and optionally the Android emulator with the two mobile apps. It does NOT start the backend: run ./start.sh first (it brings up
# Docker Compose: API, Postgres, Keycloak, SmallBites, the display service and observability). Run from anywhere in Git Bash.
#
#   ./scripts/start-ui.sh                  dashboard + displays, print the addresses
#   ./scripts/start-ui.sh --open           also open the dashboard and a main board in the browser
#   ./scripts/start-ui.sh --emulator       also start the Pixel_6 emulator and forward the ports (no app is installed)
#   ./scripts/start-ui.sh --apps           also install and run the passenger app and the operator app on the emulator (implies --emulator)
#   ./scripts/start-ui.sh --stop           stop the dashboard dev server (the emulator and the Docker services are left running)
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

OPEN=0; EMULATOR=0; APPS=0
for arg in "$@"; do
  case "$arg" in
    --open) OPEN=1 ;;
    --emulator) EMULATOR=1 ;;
    --apps) EMULATOR=1; APPS=1 ;;
    --stop)
      if [[ -f .web-dev.pid ]]; then kill "$(cat .web-dev.pid)" 2>/dev/null || true; rm -f .web-dev.pid; fi
      echo "Dashboard dev server stopped."; exit 0 ;;
    *) echo "Unknown option: $arg"; exit 2 ;;
  esac
done

up() { curl -fs -m 3 -o /dev/null "$1" 2>/dev/null; }

# 1. The backend should already be running: the screens talk to it. Warn, but go on (the servers below can start without it).
if ! up http://localhost:8080/health/ready; then
  echo "WARNING: the API is not answering on http://localhost:8080, so the screens will show no data. Start the backend with:  ./start.sh"
fi

# 2. The Operations Dashboard (fixed port 5173: Keycloak only accepts this redirect address).
echo "==> Operations Dashboard (Vite dev server)"
if up http://localhost:5173; then
  echo "    already running"
else
  [[ -d web/ops/node_modules ]] || (cd web/ops && npm install)
  (cd web/ops && nohup npm run dev >"$ROOT/web-dev.log" 2>&1 & echo $! >"$ROOT/.web-dev.pid")
  for _ in $(seq 1 30); do up http://localhost:5173 && break; sleep 1; done
  up http://localhost:5173 && echo "    started (log: web-dev.log)" || echo "    did not start: see web-dev.log"
fi

# 3. The station displays run as their own container (nginx, port 5174).
echo "==> Station displays (their own service on port 5174)"
if up http://localhost:5174/display/board/VYG; then
  echo "    already running"
else
  (cd smartrail-platform && docker compose up -d display) >/dev/null 2>&1
  for _ in $(seq 1 30); do up http://localhost:5174/display/board/VYG && break; sleep 1; done
  up http://localhost:5174/display/board/VYG && echo "    started" || echo "    not reachable: run  (cd smartrail-platform && docker compose up -d --build display)"
fi

# 4. The Android emulator and the port forwarding the apps need (API, Keycloak sign-in, caterer).
if [[ $EMULATOR == 1 ]]; then
  echo "==> Android emulator (Pixel_6)"
  if ! adb devices | grep -q "emulator-.*device"; then
    nohup emulator -avd Pixel_6 >/dev/null 2>&1 &
    adb wait-for-device
    until [[ "$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" == "1" ]]; do sleep 3; done
    echo "    started"
  else
    echo "    already running"
  fi
  adb reverse tcp:8080 tcp:8080 >/dev/null; adb reverse tcp:8180 tcp:8180 >/dev/null; adb reverse tcp:8090 tcp:8090 >/dev/null
  echo "    ports forwarded: 8080 (API), 8180 (Keycloak), 8090 (caterer)"
  if [[ $APPS == 1 ]]; then
    for app in passenger_app operator_app; do
      echo "==> Installing $app on the emulator"
      (cd "mobile/$app" && flutter pub get >/dev/null && flutter run -d emulator-5554 --no-resident 2>&1 | tail -1)
    done
  fi
fi

cat <<'MSG'

Ready.
  Operations Dashboard   http://localhost:5173             sign in: jayasinghe / jayasinghe-dev-only
  Main boards            http://localhost:5174/display/board/<code>
  Platform displays      http://localhost:5174/display/platform/<code>/<platform>      (platform 1 is the one in use)
  Station codes          CF Colombo Fort, MDA Maradana, RGM Ragama, GPH Gampaha, VYG Veyangoda

  Main boards            CF   http://localhost:5174/display/board/CF
                         MDA  http://localhost:5174/display/board/MDA
                         RGM  http://localhost:5174/display/board/RGM
                         GPH  http://localhost:5174/display/board/GPH
                         VYG  http://localhost:5174/display/board/VYG
  Platform 1 displays    CF   http://localhost:5174/display/platform/CF/1
                         MDA  http://localhost:5174/display/platform/MDA/1
                         RGM  http://localhost:5174/display/platform/RGM/1
                         GPH  http://localhost:5174/display/platform/GPH/1
                         VYG  http://localhost:5174/display/platform/VYG/1

  Operator app sign-in   nimal / nimal-dev-only   (or operator2 / operator2-dev-only; a controller must assign a journey first)
  Stop the dashboard     ./scripts/start-ui.sh --stop
MSG

if [[ $OPEN == 1 ]]; then
  for url in http://localhost:5173 http://localhost:5174/display/board/VYG http://localhost:5174/display/platform/VYG/1; do
    cmd //c start "" "$url" >/dev/null 2>&1 || true
  done
fi
