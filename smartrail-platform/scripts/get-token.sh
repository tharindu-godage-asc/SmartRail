#!/usr/bin/env bash
# DEV ONLY. Prints an access token for a demo user from the Docker Compose Keycloak (client smartrail-dev-token).
# The realm in test, staging and prod has no such client; never point this at them.
#
# Usage:   scripts/get-token.sh nimal            # operator Nimal (the assigned demo operator)
#          scripts/get-token.sh operator2        # an operator with no assignment
#          scripts/get-token.sh jayasinghe       # controller
#          scripts/get-token.sh caterer          # the caterer system client (client credentials)
# Example: curl -H "Authorization: Bearer $(scripts/get-token.sh nimal)" http://localhost:8080/api/v1/whoami
# Optional env: KEYCLOAK_URL (default http://localhost:8180), REALM (default smartrail)
set -euo pipefail

USER_NAME="${1:?usage: get-token.sh <nimal|operator2|jayasinghe|caterer>}"
KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8180}"
REALM="${REALM:-smartrail}"
ENDPOINT="$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token"

case "$USER_NAME" in
  nimal)      PASSWORD="nimal-dev-only" ;;
  operator2)  PASSWORD="operator2-dev-only" ;;
  jayasinghe) PASSWORD="jayasinghe-dev-only" ;;
  caterer)
    RESPONSE=$(curl -fsS -X POST "$ENDPOINT" -d client_id=smartrail-caterer -d client_secret=caterer-dev-only-secret -d grant_type=client_credentials)
    printf '%s' "$RESPONSE" | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p'
    exit 0 ;;
  *) echo "Unknown user '$USER_NAME'. Use nimal, operator2, jayasinghe or caterer." >&2; exit 1 ;;
esac

RESPONSE=$(curl -fsS -X POST "$ENDPOINT" -d client_id=smartrail-dev-token -d grant_type=password -d "username=$USER_NAME" -d "password=$PASSWORD")
printf '%s' "$RESPONSE" | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p'
