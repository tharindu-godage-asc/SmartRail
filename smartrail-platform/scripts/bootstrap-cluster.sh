#!/usr/bin/env bash
# Creates the local Kubernetes platform: kind cluster + Argo CD + the SmartRail Argo CD apps
# (dev, test, staging, prod and the in-cluster OpenTelemetry collector). Safe to re-run.
#
# Usage (from the repo root or anywhere):  smartrail-platform/scripts/bootstrap-cluster.sh
# Optional env vars:
#   CLUSTER_NAME   kind cluster name (default: smartrail)
#   ARGOCD_VERSION Argo CD release to install (default: stable)
#   KIND_CONFIG    kind config file (default: deploy/kind/cluster.yaml)
set -euo pipefail

CLUSTER_NAME="${CLUSTER_NAME:-smartrail}"
ARGOCD_VERSION="${ARGOCD_VERSION:-stable}"
KIND_CONFIG="${KIND_CONFIG:-}"
CONTEXT="kind-${CLUSTER_NAME}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

step() { printf '\n==> %s\n' "$*"; }

step "Checking prerequisites"
for tool in docker kind kubectl; do
  command -v "$tool" >/dev/null || { echo "Missing required tool: $tool" >&2; exit 1; }
done
docker info >/dev/null 2>&1 || { echo "Docker is not running. Start Docker Desktop and retry." >&2; exit 1; }

step "kind cluster '${CLUSTER_NAME}'"
if kind get clusters 2>/dev/null | grep -qx "$CLUSTER_NAME"; then
  echo "Already exists, skipping."
else
  kind create cluster --name "$CLUSTER_NAME" --config "${KIND_CONFIG:-$ROOT/deploy/kind/cluster.yaml}"
fi

# Every command below targets the cluster explicitly, so the current kubectl context does not matter.
K="kubectl --context $CONTEXT"

step "Installing Argo CD (${ARGOCD_VERSION})"
$K get namespace argocd >/dev/null 2>&1 || $K create namespace argocd
$K apply -n argocd --server-side --force-conflicts \
  -f "https://raw.githubusercontent.com/argoproj/argo-cd/${ARGOCD_VERSION}/manifests/install.yaml" >/dev/null
$K -n argocd rollout status deploy/argocd-server --timeout=300s
$K -n argocd rollout status deploy/argocd-repo-server --timeout=300s
$K -n argocd rollout status deploy/argocd-applicationset-controller --timeout=300s

step "Applying SmartRail Argo CD applications"
$K apply -f "$ROOT/deploy/argocd/"

step "Waiting for applications to sync (up to 5 minutes; prod is synced manually, so it is not awaited)"
deadline=$((SECONDS + 300))
while (( SECONDS < deadline )); do
  pending=$($K -n argocd get applications.argoproj.io \
    -o jsonpath='{range .items[*]}{.metadata.name}{"="}{.status.sync.status}{"/"}{.status.health.status}{"\n"}{end}' \
    | grep -v -E '^smartrail-prod=' | grep -v -E '=Synced/Healthy$' || true)
  [[ -z "$pending" ]] && break
  sleep 10
done

$K -n argocd get applications.argoproj.io
if [[ -n "${pending:-}" ]]; then
  echo
  echo "Not everything is Synced/Healthy yet:" >&2
  echo "$pending" >&2
  echo "Check the Argo CD UI or: kubectl --context $CONTEXT -n argocd describe application <name>" >&2
  echo "Note: smartrail-prod has manual sync; it stays OutOfSync until you sync it." >&2
fi

PASSWORD="$($K -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' 2>/dev/null | base64 -d || true)"
cat <<EOF

Done.

Argo CD UI:
  kubectl --context $CONTEXT -n argocd port-forward svc/argocd-server 8443:443
  https://localhost:8443   user: admin   password: ${PASSWORD:-<secret not found; already changed?>}

Telemetry: pods send data to the in-cluster collector, which forwards to the Docker Compose stack
(host.docker.internal:4317). Start it with:  docker compose up -d   (in smartrail-platform/)
EOF
