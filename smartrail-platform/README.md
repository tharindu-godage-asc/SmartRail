# SmartRail platform (skeleton)

.NET API with a full delivery pipeline: CI, GitOps deployment to four environments, and observability. The API itself only has a status endpoint so far.

## Layout
- `src/` .NET solution: Domain, Application (feature folders), Infrastructure, Api
- `tests/` unit tests and integration tests (xUnit)
- `docker-compose.yml` local environment: API, Postgres, OpenTelemetry Collector, Prometheus, Loki, Tempo, Grafana
- `observability/` collector, Prometheus and Tempo config, Grafana datasources and the SmartRail Overview dashboard
- `deploy/` Kustomize base and overlays (dev, test, staging, prod), the in-cluster collector, kind config and Argo CD applications
- `scripts/` `bootstrap.sh` (solution and packages), `bootstrap-cluster.sh` (kind + Argo CD + apps)
- `docs/` ADR-15, ADR-16, [workflows](docs/workflows.md) and [branch protection](docs/branch-protection.md)
- The CI workflows live in `.github/workflows/` at the repository root.

## First run
1. Install .NET 10 SDK, Docker, kind, kubectl (and Argo CD CLI if wanted). If you use an older SDK, change `TargetFramework` in `Directory.Build.props` and the Dockerfile image tags.
2. `./scripts/bootstrap.sh` creates the solution, adds packages, builds and runs the tests.
3. `docker compose up --build`. API at http://localhost:8080/api/v1/status, Grafana at http://localhost:3000 (anonymous admin, dev only).
4. In Grafana, Explore, pick Prometheus, Loki or Tempo and filter by `deployment_environment`: `local` is this Docker Compose stack; `dev`, `test`, `staging` and `prod` are the Kubernetes environments.

## GitOps on your machine
1. Start Docker, then run `./scripts/bootstrap-cluster.sh` (Git Bash on Windows). It creates the kind cluster, installs Argo CD, applies the apps in `deploy/argocd/` and prints the Argo CD admin password. It is safe to re-run; a fresh cluster takes about 3 minutes.
2. `docker compose up -d` so the in-cluster collector has somewhere to send telemetry (`host.docker.internal:4317`).
3. Argo CD UI: `kubectl -n argocd port-forward svc/argocd-server 8443:443`, then https://localhost:8443 (user `admin`).
4. The GHCR package `smartrail-api` must be public, or the cluster needs an image pull secret.
5. Push to `main`: CI builds, tests, scans and pushes the image, then commits the new tag to the dev, test and staging overlays; Argo CD syncs them. Run the "promote-prod" workflow, then sync `smartrail-prod` in Argo CD by hand.

Changes to `main` go through a pull request; see [docs/branch-protection.md](docs/branch-protection.md).

## Not done yet
- The observability stack runs in Docker Compose on your machine, not in the cluster, so cluster telemetry only arrives while Compose is up.
- No alerting, ingress or secrets management; all four environments share one cluster.
- No database use, migrations, authentication or real features; only a status endpoint and health checks.
