# Branch protection for `main`

Goal: nothing reaches `main` (and therefore staging) without passing CI.

## What the rule enforces

| Rule | Effect |
|------|--------|
| Pull request required | No direct pushes; changes arrive through a PR (0 approvals needed, since this is a solo project) |
| Required checks `build-test`, `security`, `image` | The PR can only merge when build, tests, vulnerability scans, CodeQL, gitleaks and the Trivy image scan are green |
| No force-push, no deletion | History on `main` cannot be rewritten or removed |
| Bypass: repository **Admin** role | You (the owner) and the `DEPLOY_TOKEN` below can still push directly |

The rule is defined in [main-ruleset.json](main-ruleset.json).

## Setup (one time)

### 1. Import the ruleset
1. GitHub → repo **Settings** → **Rules** → **Rulesets** → **New ruleset** → **Import a ruleset**.
2. Choose `smartrail-platform/docs/main-ruleset.json`.
3. Check the summary, then **Create**.

### 2. Let the CI bot keep deploying (important)
`deploy-nonprod` and `promote-prod` push tag changes directly to `main`. The default
`GITHUB_TOKEN` is **not** an admin, so once the ruleset is active those pushes would be rejected.

Fix: give them a token owned by you (an admin, who is on the bypass list).

1. GitHub → your avatar → **Settings** → **Developer settings** → **Personal access tokens** →
   **Fine-grained tokens** → **Generate new token**.
2. Repository access: only `SmartRail`. Permission: **Contents: Read and write**. Set an expiry.
3. Repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**:
   name `DEPLOY_TOKEN`, value = the token.

Both workflows already use `secrets.DEPLOY_TOKEN || secrets.GITHUB_TOKEN`, so nothing else changes.
Until the secret exists they use the normal token (which works while `main` is unprotected).

### 3. Verify
- Try `git push` to `main` from a non-admin context, or open a PR with a failing test.
  The merge button should stay disabled until the checks pass.
- Merge a small PR and confirm the bot's `deploy: dev, test, staging ...` commit still lands on `main`.

## Day-to-day flow

```
git switch -c feature/x  →  push  →  open PR  →  CI runs  →  merge  →  CI deploys dev/test/staging
```

As an admin you can still push straight to `main` in an emergency. GitHub will warn that you are bypassing the rule.

## Notes
- Required check names are the **job** names in `ci.yml`. If you rename a job, update the ruleset.
- The tag commits made by the bot use `[skip ci]`, so they do not loop.
- A token that expires will make deploys fail with a permissions error. Renew it and update the secret.
