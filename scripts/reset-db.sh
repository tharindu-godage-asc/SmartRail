#!/usr/bin/env bash
# Puts the local Compose stack back to its initial state: removes the containers AND the volumes (Postgres and Keycloak data, so every test
# train, journey and user change is gone), then starts the stack again so the init scripts, migrations and the Keycloak realm import run fresh.
# Run from anywhere in Git Bash. Docker Desktop must be running.
#
#   ./scripts/reset-db.sh           initial state, as after `docker compose down -v` and `up` (Seeding:DemoData off, the Compose default)
#   ./scripts/reset-db.sh --demo    the same, with the demo layer on (journey 8058 for Nimal, 8070), which display.spec.ts needs
#
# This deletes local data by design. It never touches the cluster.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/smartrail-platform"

DEMO=0
for arg in "$@"; do
  case "$arg" in
    --demo) DEMO=1 ;;
    *) echo "Unknown option: $arg (use --demo)"; exit 2 ;;
  esac
done

FILES=(-f docker-compose.yml)
if [[ $DEMO == 1 ]]; then
  OVERRIDE="$(mktemp)"
  trap 'rm -f "$OVERRIDE"' EXIT
  printf 'services:\n  api:\n    environment:\n      Seeding__DemoData: "true"\n' > "$OVERRIDE"
  FILES+=(-f "$OVERRIDE")
fi

echo "[$(date +%H:%M:%S)] Removing the stack and its volumes"
docker compose down -v
echo "[$(date +%H:%M:%S)] Starting the stack (demo layer: $([[ $DEMO == 1 ]] && echo on || echo off))"
docker compose "${FILES[@]}" up -d --build

# Keycloak imports its realm on the first start, so it is ready later than the API.
wait_for() { # name, url
  echo "[$(date +%H:%M:%S)] Waiting for $1 (up to 3 minutes)"
  for _ in $(seq 1 90); do
    curl -fs "$2" >/dev/null 2>&1 && return 0
    sleep 2
  done
  echo "$1 did not become ready. See: (cd smartrail-platform && docker compose logs --tail 80)" >&2
  return 1
}
wait_for "the API" http://localhost:8080/health/ready
wait_for "Keycloak (realm import)" http://localhost:8180/realms/smartrail
echo "[$(date +%H:%M:%S)] Ready: the database is in its initial state."
