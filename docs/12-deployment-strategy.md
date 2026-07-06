# Deployment Strategy

**Status:** Draft for review.

## 1. Environments

| Env | Account | Purpose | Data | Auth |
|---|---|---|---|---|
| dev | govcloud-dev | Integration of merged work | Synthetic | Test CA soft certs |
| staging | govcloud-staging | Pre-prod validation, UAT, load tests | Anonymized/synthetic | Test CA + selected real CACs |
| prod | govcloud-prod | Live | Real (CUI) | DoD PKI only |

Same Terraform modules everywhere; environments differ only by tfvars (size, counts, domains). No snowflakes.

## 2. Artifact flow

One immutable container image per service (API, worker) built once in CI, tagged with git SHA, pushed to ECR, promoted dev → staging → prod by re-tagging — never rebuilt. SPA static bundle built in the same pipeline, versioned with the API image (shared DTOs make client/server version pairing mandatory).

## 3. Release process

1. Merge to `main` → CI builds, tests, scans, pushes image, auto-deploys **dev**.
2. Deploy to **staging** via pipeline promotion (manual trigger); smoke suite + migration runs automatically.
3. **prod** deploy: manual approval gate (two-person rule: engineer + reviewer), change note auto-generated from merged PRs.
4. ECS rolling deployment: new tasks start, pass `/health/ready`, old tasks drain (min healthy 100%, max 200%). Roll back = redeploy previous image tag (one command; runbook documented).

Canary/blue-green deferred (ADR): at hundreds of users, rolling + fast rollback is sufficient; revisit with scale.

## 4. Database migrations

- Versioned SQL migrations run as a dedicated ECS task **before** the service deploy step, using `app_migrate` role.
- Expand/contract pattern mandatory: additive migration first (deploy N), destructive cleanup only after N+1 is stable. No migration may break the currently-running version.
- Every migration reviewed like code; staging is the rehearsal; prod migration runs are audited and backed by a pre-deploy snapshot.

## 5. Configuration & secrets

- Config: environment variables injected by Terraform/ECS task definitions.
- Secrets: Secrets Manager references in task definitions — never in images, env files, or logs. Rotation: DB creds 90 days (managed rotation).

## 6. DR & rollback posture

- RDS Multi-AZ auto-failover; PITR 35 days; weekly logical dump to S3 (cross-region replicated).
- Full-environment rebuild from Terraform + latest snapshot rehearsed once before go-live (RTO 4 h evidence).
- S3 documents: versioned + cross-region replication.

## 7. Go-live checklist (prod)

DNS + ACM certs valid; DoD PKI trust store loaded and CAC login verified with real cards; WAF enabled; alarms firing to on-call SNS; backup/restore rehearsed; security review (doc 17) signed off; runbooks (deploy, rollback, DLQ drain, cert expiry) in repo; load test meets NFR-2/3.
