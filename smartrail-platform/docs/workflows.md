# CI/CD workflows

SmartRail has three automations, all defined under `.github/` at the repo root:

| # | File | Trigger | Purpose |
|---|------|---------|---------|
| 1 | `workflows/ci.yml` | Push to `main` / `develop`, any pull request | Build, test, scan, publish the image, deploy to staging |
| 2 | `workflows/promote-prod.yml` | Manual (`workflow_dispatch`) | Promote the staging image tag to prod |
| 3 | `dependabot.yml` | Weekly schedule | Open PRs for dependency updates |

The delivery model is **GitOps**: pipelines never talk to the cluster. They only change
files in git (an image tag), and Argo CD pulls that change into Kubernetes.

```
 PR / push ──► build-test ┐
                          ├─► image ──► deploy-staging ──► Argo CD ──► staging
              security ───┘     │            (main only)
                                └─ PRs: build + Trivy scan, no push

 Manual: promote-prod ──► copies staging tag into prod overlay ──► Argo CD (manual sync) ──► prod
```

---

## 1. `ci.yml`: continuous integration and staging deploy

**Triggers:** every push to `main` or `develop`, and every pull request.

**Permissions:** `contents: write` (to commit the staging tag), `packages: write`
(to push to GHCR), `security-events: write` (to upload CodeQL results).

**Shared setting:** `IMAGE` is `ghcr.io/<repo owner>/smartrail-api`.

### Jobs

#### `build-test`
Runs in the `smartrail-platform` folder on Ubuntu with .NET 10.

1. `dotnet restore`
2. `dotnet build -c Release -warnaserror`: warnings fail the build.
3. `dotnet test` with code coverage collection.
4. `dotnet list package --vulnerable --include-transitive`: fails if any NuGet
   package (including indirect ones) has a known vulnerability.
5. Uploads the coverage results as the `coverage` artifact.

#### `security`
Runs in parallel with `build-test`.

- **Gitleaks** scans the full git history for committed secrets.
- **CodeQL** analyses the C# code for security issues. It builds the project,
  then uploads findings to the repo's Security tab.

#### `image`
Starts only when `build-test` **and** `security` both pass.

| Context | Behaviour |
|---------|-----------|
| Pull request / `develop` | Builds the image locally (not pushed) and scans it with **Trivy**. Fails on fixable `CRITICAL` or `HIGH` vulnerabilities. |
| `main` | Logs in to GHCR and **pushes** `ghcr.io/<owner>/smartrail-api:<commit sha>`. |

Images are tagged with the full commit SHA, so every image maps to exactly one commit.

#### `deploy-staging`
Runs on `main` only, after `image` succeeds.

Edits `newTag:` in `smartrail-platform/deploy/overlays/staging/kustomization.yaml` to the
new commit SHA, then commits and pushes it as `ci-bot`. The commit message contains
`[skip ci]`, so it does not trigger another run. Argo CD sees the change and syncs staging
automatically.

---

## 2. `promote-prod.yml`: promote staging to production

**Trigger:** manual only. Run it from the **Actions** tab → **promote-prod** → *Run workflow*.

**What it does:**

1. Reads the `newTag` currently set in the **staging** overlay.
2. Writes that same tag into the **prod** overlay (`smartrail-platform/deploy/overlays/prod/kustomization.yaml`).
3. Commits and pushes the change as `ci-bot`.

This guarantees prod only ever runs an image that was already deployed to staging.

**Prod is not deployed automatically.** The Argo CD `smartrail-prod` app has no automated
sync. After this workflow runs, the app shows *OutOfSync*, and you press **Sync** in the
Argo CD UI (or run `argocd app sync smartrail-prod`) once you're happy with staging.

---

## 3. `dependabot.yml`: automated dependency updates

**Trigger:** weekly.

Dependabot opens pull requests when newer versions are available for:

| Ecosystem | Directory | Covers |
|-----------|-----------|--------|
| `nuget` | `/smartrail-platform` | .NET packages in the solution |
| `docker` | `/smartrail-platform/src/SmartRail.Api` | Base images in the API Dockerfile |
| `github-actions` | `/` | Action versions used in the workflows |

Each update PR runs through `ci.yml`, so a dependency bump only merges if it builds,
passes the tests and clears the security scans.

---

## Environments at a glance

| Env | Deployed by | Sync |
|-----|-------------|------|
| dev | Argo CD app `smartrail-dev` | automatic (tag edited by hand) |
| test | Argo CD app `smartrail-test` | automatic (tag edited by hand) |
| staging | `ci.yml` → `deploy-staging` | automatic |
| prod | `promote-prod.yml` | **manual** in Argo CD |

Dev and test are not updated by CI yet. Change their `newTag` in
`smartrail-platform/deploy/overlays/<env>/kustomization.yaml` to deploy to them.

## One-time setup checklist

- [ ] Push the repo to GitHub (`main` branch).
- [ ] After the first successful `main` build, set the `smartrail-api` package to **public**
      in GitHub → Packages, or add an image pull secret to the cluster.
- [ ] If `main` is branch-protected, allow `ci-bot` pushes. The deploy jobs commit directly to `main`.
- [ ] Apply the Argo CD apps: `kubectl apply -f smartrail-platform/deploy/argocd/`.
