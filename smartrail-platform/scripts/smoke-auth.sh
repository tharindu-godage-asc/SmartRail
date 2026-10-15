#!/usr/bin/env bash
# Manual check of the REAL Keycloak against the running Docker Compose stack (spec 02-keycloak-identity, section 10.4).
# Run it once by hand before merging changes to the realm file or the authentication code. It is not part of CI.
#
# What it proves: tokens from the realm are accepted by the API, carry the right subject, roles, audience and lifetime,
# and no token or a garbage token gets 401. The 403 behaviour (wrong role) is proved by the integration tests, because no
# controller-only endpoint exists in production code.
#
# Needs: the stack running (docker compose up -d), curl, and the API flag Diagnostics__WhoAmI=true (Compose sets it).
set -uo pipefail

API="${API_URL:-http://localhost:8080}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FAILED=0

pass() { printf '  PASS  %s\n' "$1"; }
fail() { printf '  FAIL  %s\n' "$1"; FAILED=1; }
check() { if [[ "$2" == "$3" ]]; then pass "$1"; else fail "$1 (expected '$3', got '$2')"; fi; }

# Reads one claim from a token's payload (decoding without verifying; verification is the API's job).
claim() {
  local payload
  payload=$(printf '%s' "$1" | cut -d. -f2 | tr '_-' '/+')
  while (( ${#payload} % 4 )); do payload="$payload="; done
  printf '%s' "$payload" | base64 -d 2>/dev/null | tr -d ' \n'
}
status() { curl -s -o /dev/null -w '%{http_code}' "$@"; }

echo "Public endpoints"
check "GET /api/v1/status without a token" "$(status "$API/api/v1/status")" 200
check "GET /health/ready without a token" "$(status "$API/health/ready")" 200

echo "No token, garbage token"
check "GET /api/v1/whoami without a token" "$(status "$API/api/v1/whoami")" 401
check "GET /api/v1/whoami with a garbage token" "$(status -H 'Authorization: Bearer abc.def.ghi' "$API/api/v1/whoami")" 401

echo "Nimal (operator)"
NIMAL=$("$HERE/get-token.sh" nimal) || { fail "could not fetch a token for nimal (is Keycloak up on :8180?)"; exit 1; }
CLAIMS=$(claim "$NIMAL")
[[ "$CLAIMS" == *'"sub":"d0d0d0d0-0000-4000-8000-000000000001"'* ]] && pass "sub is Nimal's fixed ID" || fail "sub is not Nimal's fixed ID: $CLAIMS"
[[ "$CLAIMS" == *'"roles":["operator"]'* ]] && pass "roles is [operator]" || fail "roles claim: $CLAIMS"
[[ "$CLAIMS" == *'"aud":"smartrail-api"'* ]] && pass "audience is smartrail-api" || fail "audience: $CLAIMS"
IAT=$(printf '%s' "$CLAIMS" | sed -n 's/.*"iat":\([0-9]*\).*/\1/p'); EXP=$(printf '%s' "$CLAIMS" | sed -n 's/.*"exp":\([0-9]*\).*/\1/p')
check "token lifetime is 15 minutes" "$((EXP - IAT))" 900
check "GET /api/v1/whoami with Nimal's token" "$(status -H "Authorization: Bearer $NIMAL" "$API/api/v1/whoami")" 200
WHOAMI=$(curl -s -H "Authorization: Bearer $NIMAL" "$API/api/v1/whoami")
[[ "$WHOAMI" == *'"subject":"d0d0d0d0-0000-4000-8000-000000000001"'* && "$WHOAMI" == *'"operator"'* ]] && pass "whoami returns his subject and role" || fail "whoami body: $WHOAMI"

echo "Jayasinghe (controller)"
JAYA=$("$HERE/get-token.sh" jayasinghe)
[[ "$(claim "$JAYA")" == *'"roles":["controller"]'* ]] && pass "roles is [controller]" || fail "controller roles claim"
check "GET /api/v1/whoami with Jayasinghe's token" "$(status -H "Authorization: Bearer $JAYA" "$API/api/v1/whoami")" 200

echo "Caterer (client credentials)"
CATERER=$("$HERE/get-token.sh" caterer)
CC=$(claim "$CATERER")
[[ "$CC" == *'"roles":["caterer"]'* ]] && pass "roles is [caterer]" || fail "caterer roles claim: $CC"
[[ "$CC" == *'"aud":"smartrail-api"'* ]] && pass "caterer audience is smartrail-api" || fail "caterer audience: $CC"

echo
if (( FAILED )); then echo "SMOKE TEST FAILED"; exit 1; fi
echo "Smoke test passed."
