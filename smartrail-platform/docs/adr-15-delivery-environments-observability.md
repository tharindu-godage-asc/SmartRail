# ADR-15 · Delivery pipeline, environments and observability

**Status:** Proposed by Claude, accepted by the owner on 12 Oct 2026 (Argo CD to be tried; runs on the owner's machine). Amended 12 Oct 2026 after the setup was built: the environment layout below is what runs; the original draft put dev in Compose and test in the pipeline only.
**Related:** brief §23 (Operations, QA, Security), NFR-04 (30-second update target), ADR-08.

## Context
One engineer, eight days to submission, a feature freeze on 15 Oct. The brief asks for observability and production-readiness thinking, and the owner wants industry-standard practice: containers, a pipeline that builds and tests on every commit, four environments, GitOps with Argo CD, and observability at every stage.

## Decision
| Area | Choice |
|---|---|
| Backend shape | .NET modular monolith, feature folders, Clean Architecture dependency rule (Domain, Application, Infrastructure, Api). Separate deployables only for the simulated caterer, web dashboard, mobile apps and displays. |
| Containers | One Dockerfile per deployable; one image per commit, tagged with the commit SHA, promoted unchanged between environments. |
| Pipeline | GitHub Actions: build with warnings as errors, unit and integration tests, vulnerable-package check, gitleaks, CodeQL, image build, Trivy scan, push to GHCR, then update the image tag for dev, test and staging in `deploy/`. Prod is promoted by a manual workflow that copies staging's tag. Details: `workflows.md`. |
| Deployment | GitOps. Argo CD (running in a local kind cluster) has one application per environment: `deploy/overlays/dev`, `test` and `staging` sync automatically; `deploy/overlays/prod` is a manual sync. CI never has cluster access. |
| Environments | **local**: Docker Compose on the owner's machine (`deployment_environment="local"`). **dev**, **test**, **staging**, **prod**: four namespaces (`smartrail-<env>`) in one local kind cluster, one Argo CD application each. Automated tests also run in CI on every commit. |
| Branching | Trunk-based: one protected `main`, short-lived feature branches and pull requests. Environments are deployments, not branches. See `branch-protection.md`. |
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
- CI builds and tests every commit, and one image per commit is promoted unchanged through dev, test and staging, so what is tested is what is deployed.
- Prod is a manual Argo CD sync, so a human checks staging first.
- OpenTelemetry keeps the application independent of the observability backend.

## Limits
- Dev, test, staging and prod share one local cluster, so this shows the process, not real isolation or capacity. The four environments currently differ only in name; per-environment replicas, resources and configuration are not set.
- The observability backend (Prometheus, Loki, Tempo, Grafana) runs in Docker Compose, not in the cluster. A small in-cluster collector forwards the cluster's telemetry to it, so cluster telemetry only arrives while Compose is running.
- No alerting, ingress or secret store. Secrets in `docker-compose.yml` are development-only; real secrets need a secret store, not done in the MVP.
- Verified on 12 Oct 2026: the pipeline is green, all four Argo CD applications sync, and telemetry from every environment appears in Grafana with its `deployment.environment` value.

## Fallback rule
If Argo CD stops working before the feature freeze (15 Oct), stop and document GitOps as "designed, not built": keep the pipeline, the `deploy/` folders and the Argo CD manifests, and deploy with `kubectl apply -k smartrail-platform/deploy/overlays/<env>` instead. Do not spend time after the freeze on the cluster. Rebuilding the cluster is scripted: `smartrail-platform/scripts/bootstrap-cluster.sh`.
