# SmartRail platform (skeleton)

Written on 12 Oct 2026 without a .NET SDK or Docker available, so nothing here has been compiled or run yet. Expect small fixes on first build.

## Layout
- `src/` .NET solution: Domain, Application (feature folders), Infrastructure, Api
- `tests/` unit tests and integration tests (xUnit)
- `docker-compose.yml` dev environment: API, Postgres, OpenTelemetry Collector, Prometheus, Loki, Tempo, Grafana
- `.github/workflows/` CI on every commit, manual promotion to prod
- `deploy/` Kustomize overlays (staging, prod) and Argo CD applications
- `docs/` ADR-15 and ADR-16

## First run
1. Install .NET 10 SDK, Docker, kind, kubectl (and Argo CD CLI if wanted). If you use an older SDK, change `TargetFramework` in `Directory.Build.props` and the Dockerfile image tags.
2. `./scripts/bootstrap.sh` creates the solution, adds packages, builds and runs the tests.
3. `docker compose up --build`. API at http://localhost:8080/api/v1/status, Grafana at http://localhost:3000 (anonymous admin, dev only).
4. In Grafana, Explore, pick Prometheus, Loki or Tempo and filter by `deployment_environment`: `local` is this Docker Compose stack; `dev`, `test`, `staging` and `prod` are the Kubernetes environments.

## GitOps on your machine
1. Replace `OWNER` in `deploy/` and `.github/` with your GitHub user or organisation, and push the repository.
2. `kind create cluster --config deploy/kind/cluster.yaml`
3. Install Argo CD: `kubectl create namespace argocd` then apply the stable install manifest from the Argo CD documentation.
4. `kubectl apply -f deploy/argocd/`
5. Make the GHCR package public, or add an image pull secret in both namespaces.
6. Push to `main`: CI builds, tests, pushes the image, and commits the new tag to `deploy/overlays/staging`; Argo CD syncs staging. Run the "promote-prod" workflow, then sync prod in Argo CD by hand.

## Not done yet
- The cluster does not yet run the observability stack (the API there will log export errors until it does).
- No database, migrations, authentication or real features; only a status endpoint and health checks.
