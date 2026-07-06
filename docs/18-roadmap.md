# Project Roadmap

**Status:** Draft for review. Timeline: MVP target ~12 weeks from kickoff. **PM risk note:** "everything in MVP" in 12 weeks is aggressive for the CAC/GovCloud items in particular; de-scoping triggers are pre-agreed (doc 05) so slips cut P1 scope, not quality gates.

## Milestone 0 — Foundations (Weeks 1–2)

Repo + solution scaffold, CI skeleton, Terraform baseline (dev account: VPC, RDS, S3, ECS), initial schema + migrations, CAC/mTLS auth spike **started week 1** (highest technical risk — see risks), logging/health plumbing.
**Exit:** hello-world API deployed to dev via pipeline; login with test-CA cert works end-to-end; ADRs 1–6 ratified.

## Milestone 1 — Core Domain (Weeks 3–5)

Squadron/unit/user management, asset CRUD + history, audit log, search/filter/pagination, admin + customer shells with role-gated nav.
**Exit:** all Epic A/C/D P0 stories demoable in dev; authZ matrix tests green; E2E journeys 1, 4, 6 green.

## Milestone 2 — Manual Entry & Bulk Import (Weeks 5–7) — *rescoped 2026-07-06*

Document pipeline deferred (no sample documents available yet — see Deferred section, doc 05). Fast manual asset entry (<60 s per asset, as-you-type duplicate check), quick multi-row grid entry, bulk CSV import/export promoted from M3.
**Exit:** custodian can enter a batch of 20 devices in under 10 minutes via grid or CSV; import validation report E2E green.

## Milestone 3 — Visibility & Hardening (Weeks 7–10)

Dashboards, transfers, bulk import/export, reports, profile; observability completion (alarms, dashboards, runbooks); load tests; security review + pen-test checklist; staging→prod promotion rehearsed; DR rehearsal.
**Exit:** all P0/P1 backlog complete or consciously de-scoped; NFRs evidenced; security sign-off.

## Milestone 4 — Go-Live (Week 11)

Prod deployment, real-CAC validation with pilot users (one squadron), go-live checklist (doc 12 §7), hypercare week with daily triage.
**Exit:** pilot squadron live; error/queue/latency alarms quiet for 5 consecutive days.

## Post-MVP themes (unscheduled)

**Document pipeline (first post-MVP priority, reactivates when sample documents arrive):** upload flow, virus scan, Textract + Bedrock behind `IDocumentParser`, review queue — design complete in docs 06/08/11, backlog items D1–D8. Then: parser tuning loop from reviewer corrections; SES notifications; reporting depth; barcode scanning; DPAS/external sync; configurable asset fields; multi-tenancy.

## Top risks & mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| DoD PKI trust store / OCSP reachability details late (OQ1) | Blocks real-CAC login | Spike in week 1 with test CA; sponsor engagement immediately; ALB mTLS proven early |
| GovCloud service availability differs from assumption (Bedrock models, OIDC, GuardDuty malware) | Redesign of pipeline pieces | Verify all service/feature availability in week 1 against the account, not documentation; adapters isolate blast radius |
| Extraction quality on real documents unknown | *(Deferred with pipeline)* | Sample-document collection remains the reactivation trigger; golden-file suite plan unchanged |
| Full scope in 12 weeks | Quality erosion | Pre-agreed de-scope order (doc 05); gates (tests, security) never cut |
| Solo/small team bus factor | Stall | Docs-as-you-go (this set), runbooks, plain architecture |
