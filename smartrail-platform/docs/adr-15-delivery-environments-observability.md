# ADR-15 · Delivery pipeline, environments and observability

**Status:** Proposed by Claude, accepted by the owner on 12 Oct 2026 (Argo CD to be tried; runs on the owner's machine).
**Related:** brief §23 (Operations, QA, Security), NFR-04 (30-second update target), ADR-08.

## Context
One engineer, eight days to submission, a feature freeze on 15 Oct. The brief asks for observability and production-readiness thinking, and the owner wants industry-standard practice: containers, a pipeline that builds and tests on every commit, four environments, GitOps with Argo CD, and observability at every stage.

## Decision
| Area | Choice |
|---|---|
| Backend shape | .NET modular monolith, feature folders, Clean Architecture dependency rule (Domain, Application, Infrastructure, Api). Separate deployables only for the simulated caterer, web dashboard, mobile apps and displays. |
| Containers | One Dockerfile per deployable; one image per commit, tagged with the commit SHA, promoted unchanged between environments. |
| Pipeline | GitHub Actions: build with warnings as errors, unit and integration tests, vulnerable-package check, gitleaks, CodeQL, image build, Trivy scan, push to GHCR, then update the staging image tag in `deploy/`. |
| Deployment | GitOps. Argo CD (running in a local kind cluster) pulls `deploy/overlays/staging` (automatic sync) and `deploy/overlays/prod` (manual sync). CI never has cluster access. |
| Environments | **dev**: Docker Compose on the owner's machine. **test**: created inside the pipeline for each commit (throwaway Postgres via Testcontainers). **staging** and **prod**: two namespaces in the local kind cluster. |
| Observability | OpenTelemetry in the code. Collector sends metrics to Prometheus, logs to Loki, traces to Tempo; Grafana shows all three. Every signal carries `deployment.environment`. |
| Mobile | Flutter (see ADR-16). |

## Options compared
| Option | For | Against |
|---|---|---|
| Observability: OpenTelemetry + Prometheus + Loki + Tempo + Grafana (chosen) | One instrumentation, vendor-neutral, light (a few hundred MB), covers metrics, logs and traces | Several containers to configure |
| Observability: OpenSearch (+ Dashboards) | Strong full-text log search | Heavy memory use, separate setup for metrics and traces, poor fit for one person |
| Deploy: Argo CD on kind (chosen) | Industry-standard GitOps, pull model suits a machine with no inbound access, shows promotion and drift correction | Extra moving parts; setup time |
| Deploy: Docker Compose only | Fastest | No promotion model, no GitOps evidence |
| Architecture: microservices | Looks "enterprise" | Too much operational cost for one engineer; the domain does not need it yet |

## Reasons
- A pull-based GitOps tool works with a local machine, because the cluster pulls from Git and nothing needs to reach in.
- The test environment is per commit, so "build and test after each commit" is satisfied without a long-lived server.
- Prod is a manual Argo CD sync, so a human checks staging first.
- OpenTelemetry keeps the application independent of the observability backend.

## Limits
- Staging and prod share one local cluster, so this shows the process, not real isolation or capacity.
- Observability inside the cluster (collector, Prometheus, Loki, Tempo, Grafana) is defined for Docker Compose; the cluster deployment of it is a later step.
- Secrets in `docker-compose.yml` are development-only. Real secrets need a secret store; not done in the MVP.
- Nothing here has been run yet. The skeleton was written without a .NET SDK or Docker available, so the first build may need small fixes.

## Fallback rule
If Argo CD is not working by the feature freeze (15 Oct), stop and document GitOps as "designed, not built": keep the pipeline, the `deploy/` folders and the Argo CD manifests, and deploy staging with `kubectl apply -k deploy/overlays/staging` instead. Do not spend time after the freeze on the cluster.
