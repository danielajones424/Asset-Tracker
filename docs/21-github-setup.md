# GitHub Setup & First Push — Runbook

Run these from the project folder on your machine (`~/Documents/Claude/Projects/Asset Tracker`). Requires the [GitHub CLI](https://cli.github.com) (`gh auth login` first), or use the web-UI alternative noted at each step.

## 1. Create the repository and push

```bash
gh repo create asset-tracker --private --source . --push
```

Web alternative: create an empty private repo `asset-tracker` on github.com (no README/license — we have them), then:

```bash
git remote add origin git@github.com:<you>/asset-tracker.git
git push -u origin main
```

## 2. Watch the first CI run

```bash
gh run watch
```

The `PR` workflow triggers on push to `main`. **Expect possible failures** — nothing has compiled locally (no .NET SDK in the Cowork sandbox), so this run is the first honest build. Likely first-run issues: package version wildcards resolving unexpectedly, Fantomas formatting differences, Fable tool version. Fix on a `bugfix/ci-first-run` branch; that's normal.

## 3. Branch protection (docs/14 §1)

```bash
gh api repos/{owner}/asset-tracker/branches/main/protection -X PUT --input - <<'EOF'
{
  "required_status_checks": { "strict": true, "contexts": ["build-test", "fable-check", "security"] },
  "enforce_admins": true,
  "required_pull_request_reviews": { "required_approving_review_count": 1, "dismiss_stale_reviews": true },
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "required_linear_history": true
}
EOF
```

Web alternative: Settings → Branches → Add rule for `main`: require PR + 1 approval, require status checks (build-test, fable-check, security), linear history, include administrators.

Also in Settings → General → Pull Requests: enable **squash merging only**, enable **auto-delete head branches**.

## 4. Environments (docs/13 §2)

Settings → Environments: create `dev` (no protection), `staging` (required reviewer: you), `prod` (required reviewers, 10-min wait timer). Deploy jobs reference these when the release pipeline lands.

## 5. Secrets — later, not now

No secrets are needed for the current CI (build/test only). When AWS deploy jobs land (Milestone 0 exit): add `AWS_ROLE_ARN_DEV` etc. as **Environment** secrets — after verifying GitHub OIDC federation availability in GovCloud (open item, docs/13 §2). Never add application secrets to GitHub.

## 6. Verify process end-to-end

```bash
git switch -c docs/verify-workflow
# trivial change, e.g. fix a typo in README
git commit -am "docs: verify PR workflow"
git push -u origin docs/verify-workflow
gh pr create --fill && gh pr view --web
```

Confirm: checks run, merge is blocked until green + review, squash merge works, branch auto-deletes. Then delete the test change if unwanted (revert PR).

## Current local state being pushed

`main` @ 4 commits: planning docs · repo scaffold (#1) · initial schema + migration runner (#3) · Terraform baseline (#2). All produced via feature-branch + squash-merge; no direct commits to main except the initial docs commit.
