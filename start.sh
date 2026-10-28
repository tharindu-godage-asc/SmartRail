#!/usr/bin/env bash
# Brings up everything for local work: the Docker Compose stack (API, Postgres, Keycloak, displays, observability),
# the controller web app (dev server), and the emulator port forwarding. Run from the repo root in Git Bash.
# It says what each process is doing as it starts: a numbered step, a timestamp, container states and how long each wait took.
#   ./start.sh                start the stack and the web app
#   ./start.sh --emulator     also start the Pixel_6 emulator and run the passenger app
#   ./start.sh --operator     same, but run the operator app instead
#   ./start.sh --stop         stop the stack and the web dev server (data is kept)
#   ./start.sh --quiet        (first or last argument) hide the Docker build output and the container table
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

# ---- arguments: one mode, plus an optional --quiet in any position ----
MODE=""; QUIET=0
for arg in "$@"; do
  case "$arg" in
    --quiet) QUIET=1 ;;
    --stop|--emulator|--operator) MODE="$arg" ;;
    *) echo "Unknown option: $arg (use --emulator, --operator, --stop, --quiet)"; exit 1 ;;
  esac
done

# ---- small helpers ----
START_TS=$(date +%s)
STEP=0
ts() { date +%H:%M:%S; }
elapsed() { echo "$(( $(date +%s) - START_TS ))s"; }
step() { STEP=$((STEP + 1)); printf '\n[%s] [%s/%s] ==> %s\n' "$(ts)" "$STEP" "$TOTAL_STEPS" "$*"; }
info() { printf '[%s]        %s\n' "$(ts)" "$*"; }
ok()   { printf '[%s]        OK: %s\n' "$(ts)" "$*"; }
fail() { printf '[%s]        FAILED: %s\n' "$(ts)" "$*" >&2; exit 1; }

# Prints one line per compose service: its state and health.
show_containers() {
  [[ $QUIET == 1 ]] && return 0
  (cd smartrail-platform && docker compose ps --format '        {{.Service}}: {{.State}} {{.Health}}' 2>/dev/null) || true
}

# Waits until a URL answers, printing a dot-line every few seconds so it never looks stuck. Usage: wait_for <name> <url> <tries> <seconds>
wait_for() {
  local name="$1" url="$2" tries="$3" pause="$4" began i
  began=$(date +%s)
  info "waiting for $name at $url (up to $(( tries * pause ))s)"
  for i in $(seq 1 "$tries"); do
    if curl -fs "$url" >/dev/null 2>&1; then
      ok "$name answered after $(( $(date +%s) - began ))s"
      return 0
    fi
    # A status line every 3 tries, and the container table every 5, so slow starts show what is still coming up.
    (( i % 3 == 0 )) && info "$name not ready yet ($(( $(date +%s) - began ))s)"
    (( i % 5 == 0 )) && show_containers
    sleep "$pause"
  done
  return 1
}

# ---- stop ----
if [[ "$MODE" == "--stop" ]]; then
  TOTAL_STEPS=2
  step "Stopping the Docker Compose stack (data is kept)"
  (cd smartrail-platform && docker compose stop)
  step "Stopping the controller web dev server"
  if [[ -f .web-dev.pid ]] && kill -0 "$(cat .web-dev.pid)" 2>/dev/null; then
    kill "$(cat .web-dev.pid)" 2>/dev/null || true
    ok "stopped process $(cat .web-dev.pid)"
  else
    info "it was not running"
  fi
  rm -f .web-dev.pid
  echo; echo "Stopped."; exit 0
fi

# Steps: Docker check, compose, API, other services, web app, (emulator + app), summary.
TOTAL_STEPS=5
[[ "$MODE" == "--emulator" || "$MODE" == "--operator" ]] && TOTAL_STEPS=7

# ---- 1. Docker ----
step "Checking Docker"
docker info >/dev/null 2>&1 || fail "Docker is not running. Start Docker Desktop first."
ok "Docker is running ($(docker version --format '{{.Server.Version}}' 2>/dev/null || echo 'version unknown'))"

# ---- 2. Compose stack ----
step "Starting the Docker Compose stack (the first build takes a few minutes)"
info "services: $(cd smartrail-platform && docker compose config --services 2>/dev/null | tr '\n' ' ')"
if [[ $QUIET == 1 ]]; then
  (cd smartrail-platform && docker compose up -d --build >/dev/null 2>&1) || fail "docker compose up failed. Run it by hand without --quiet to see why."
else
  # --progress plain prints every build and start line instead of a redrawn bar, so it also reads well in a log.
  (cd smartrail-platform && docker compose --progress plain up -d --build) || fail "docker compose up failed (see the lines above)."
fi
ok "compose finished after $(elapsed)"
show_containers

# ---- 3. API ----
step "Waiting for the API (it applies migrations and seeds the demo data first)"
wait_for "API" "http://localhost:8080/health/ready" 60 3 || {
  show_containers
  fail "the API did not become ready. See: (cd smartrail-platform && docker compose logs api --tail 80)"
}

# ---- 4. Other services ----
step "Checking the other services"
check_service() { # name url
  if curl -fs -o /dev/null "$2" 2>/dev/null; then ok "$1 is up ($2)"; else info "$1 is not answering yet ($2); it may still be starting"; fi
}
check_service "Keycloak" "http://localhost:8180/realms/smartrail"
check_service "Station displays" "http://localhost:5174/"
check_service "SmallBites (caterer)" "http://localhost:8090/"
check_service "Grafana" "http://localhost:3000/api/health"

# ---- 5. Controller web app ----
step "Starting the controller web app (http://localhost:5173)"
if [[ -f .web-dev.pid ]] && kill -0 "$(cat .web-dev.pid)" 2>/dev/null; then
  ok "already running (process $(cat .web-dev.pid)); log: web-dev.log"
else
  if [[ ! -d web/ops/node_modules ]]; then
    info "node_modules is missing: running npm install (this can take a minute)"
    (cd web/ops && npm install)
  fi
  info "starting 'npm run dev'; its output goes to web-dev.log"
  (cd web/ops && nohup npm run dev >"$ROOT/web-dev.log" 2>&1 & echo $! >"$ROOT/.web-dev.pid")
  info "process $(cat .web-dev.pid) started"
  wait_for "web app" "http://localhost:5173/" 30 2 || {
    info "last lines of web-dev.log:"; tail -n 15 web-dev.log | sed 's/^/        /'
    fail "the web app did not answer. See web-dev.log"
  }
fi

# ---- 6 and 7. Emulator and app ----
if [[ "$MODE" == "--emulator" || "$MODE" == "--operator" ]]; then
  step "Android emulator (Pixel_6)"
  if adb devices | grep -q "emulator-"; then
    ok "an emulator is already running"
  else
    info "launching the emulator (its own output is hidden; a cold boot takes about a minute)"
    nohup emulator -avd Pixel_6 >/dev/null 2>&1 &
    info "waiting for the device to connect"
    adb wait-for-device
    began=$(date +%s)
    until [[ "$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" == "1" ]]; do
      info "Android is still booting ($(( $(date +%s) - began ))s)"
      sleep 5
    done
    ok "the emulator booted after $(( $(date +%s) - began ))s"
  fi
  for port in 8080 8180 8090; do
    adb reverse "tcp:$port" "tcp:$port" >/dev/null
    info "forwarded emulator port $port to this machine"
  done

  app=passenger_app; [[ "$MODE" == "--operator" ]] && app=operator_app
  step "Building and running $app on the emulator (the first build takes a few minutes)"
  info "flutter pub get"
  (cd "mobile/$app" && flutter pub get)
  info "flutter run -d emulator-5554 --no-resident"
  (cd "mobile/$app" && flutter run -d emulator-5554 --no-resident)
  ok "$app is running"
fi

cat <<MSG

Ready after $(elapsed).
  API                  http://localhost:8080  (e.g. /api/v1/trains?from=VYG&to=CF)
  Controller web app   http://localhost:5173  (jayasinghe / jayasinghe-dev-only)
  Station displays     http://localhost:5174/display/board/VYG
  SmallBites (caterer) http://localhost:8090/admin
  Keycloak             http://localhost:8180  (admin / admin-dev-only)
  Grafana              http://localhost:3000
Mobile: ./start.sh --emulator   Stop: ./start.sh --stop   Quieter: ./start.sh --quiet
MSG
