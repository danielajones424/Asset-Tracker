# Infrastructure — Terraform (AWS GovCloud)

Layout per docs/07. One root module per environment; shared modules underneath.

```
infra/
  modules/
    network/    VPC, subnets, VPC endpoints
    database/   RDS PostgreSQL (Multi-AZ in prod)
    storage/    S3 buckets (quarantine, clean, exports) + KMS
    queue/      SQS pipeline queue + DLQ (deferred pipeline; cheap to keep)
    api/        ECS Fargate service + ALB (mTLS)
  envs/
    dev/        Single-AZ, minimal sizing
    staging/    (added when dev is proven)
    prod/       (added at M3)
```

State: S3 backend + DynamoDB lock table (bootstrapped manually once per account).
All changes via PR; `terraform plan` posted by CI (docs/13).

**M0 status:** module skeletons + dev env wiring. Not yet applied — GovCloud account
bootstrap (accounts, state backend, DoD PKI trust store) is a manual prerequisite
tracked in the M0 exit checklist.
