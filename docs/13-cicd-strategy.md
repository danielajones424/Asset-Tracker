# CI/CD Strategy — GitHub Actions

**Status:** Draft for review.

## 1. Pipelines

### PR pipeline (every push to a PR branch)
```
lint+format check (Fantomas, ESLint for JS glue)
  → build (dotnet build; Fable/Vite client build)
  → unit tests (domain + application; coverage gate ≥80% on those layers)
  → integration tests (Testcontainers PostgreSQL + LocalStack for S3/SQS)
  → dependency audit (dotnet list --vulnerable, npm audit) 
  → container build + Trivy image scan (fail on HIGH/CRITICAL)
  → SAST (CodeQL) · secret scan (gitleaks)
  → terraform fmt/validate/plan (posted to PR when infra changed)
```
All jobs required for merge. Target: <10 min via caching (NuGet, npm, Docker layers).

### Main pipeline (merge to main)
PR pipeline + push image `:sha` to ECR → deploy dev → E2E suite (Playwright) against dev → publish OpenAPI + coverage artifacts.

### Release pipeline (manual dispatch)
Promote `:sha` → staging (migrations → deploy → smoke tests) → manual approval (GitHub Environments, required reviewers) → prod (migrations → rolling deploy → smoke tests → tag `release/YYYY-MM-DD`).

### Nightly
Full E2E on staging, load-test smoke, dependency-update PRs (Dependabot), ClamAV definition refresh check.

## 2. GitHub configuration

- **Branch protection on `main`:** PRs only; required checks; ≥1 review; no force push; linear history (squash merge); stale review dismissal; CODEOWNERS for `/infra`, `/src/Server/Auth`, `/docs`.
- **Environments:** `dev` (auto), `staging` (maintainers), `prod` (required reviewers = 2, wait timer 10 min).
- **AWS auth:** GitHub OIDC federation → short-lived role per environment (no long-lived keys). GovCloud note: verify OIDC availability in the target partition; fallback is a scoped IAM user with rotated keys stored in GitHub Environments secrets — decide in Milestone 0.
- **Secrets:** GitHub Environment secrets only for bootstrap (AWS role ARNs); application secrets never touch GitHub.

## 3. Quality gates summary

| Gate | Threshold | Blocks |
|---|---|---|
| Unit coverage (domain+app) | ≥80% | merge |
| Trivy image scan | no HIGH/CRITICAL | merge |
| CodeQL / gitleaks | no new findings | merge |
| E2E on dev | green | release promotion |
| Smoke on staging/prod | green | (prod: auto-rollback trigger) |

## 4. Review workflow (per project rules)

Every PR: self-review checklist → automated checks → `/ponytail-review` (over-engineering pass) and correctness review before merge → squash merge with conventional-commit title → branch auto-deleted. Periodic (per milestone): `/ponytail-audit` over the repo and `/ponytail-debt` ledger review; findings become backlog items.
