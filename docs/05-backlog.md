# Initial Backlog — Asset Tracking System

Priorities: P0 = MVP blocker, P1 = MVP target, P2 = post-MVP. Sized T-shirt (S/M/L/XL). Ordered for execution. Branch names follow doc 14.

## Milestone 0 — Foundations (Week 1–2)

| # | Item | Pri | Size | Stories |
|---|------|-----|------|---------|
| 1 | Repo scaffold: solution layout, Fable 5 client, Giraffe server, shared domain project, lint/format, CI skeleton | P0 | M | US-G2 |
| 2 | Infrastructure as code baseline (Terraform): GovCloud account layout, VPC, RDS, S3, ECS skeleton | P0 | L | US-G2 |
| 3 | Database migrations framework + initial schema (doc 08) | P0 | M | — |
| 4 | CAC/PIV auth: mTLS termination, cert validation, user mapping, sessions | P0 | L | US-A1 |
| 5 | RBAC middleware + scope enforcement + tests | P0 | M | US-A3 |
| 6 | Structured logging, correlation IDs, health endpoints | P0 | S | US-G1 |

## Milestone 1 — Core Domain (Week 3–5)

| # | Item | Pri | Size | Stories |
|---|------|-----|------|---------|
| 7 | Squadron/Unit management (API + admin UI) | P0 | M | US-C4 |
| 8 | User provisioning & role assignment (API + admin UI) | P0 | M | US-A2 |
| 9 | Asset CRUD + validation + soft delete (API + UI) | P0 | L | US-C1, US-C2 |
| 10 | Asset history (field-level change capture) | P0 | M | US-D2 |
| 11 | Audit log write path (append-only) + admin search UI | P0 | M | US-D3 |
| 12 | Asset search/filter/sort/pagination | P0 | M | US-D1 |

## Milestone 2 — Manual Entry & Bulk Import (Week 5–7) — *rescoped 2026-07-06*

Document pipeline (formerly this milestone) deferred until sample documents are available; items preserved below. Manual entry becomes the primary data-entry path; bulk CSV import promoted from M3 to cover volume entry.

| # | Item | Pri | Size | Stories |
|---|------|-----|------|---------|
| 13 | Manual asset entry UX: fast single-asset form (<60 s), duplicate check on tag/serial as-you-type | P0 | M | US-C1 |
| 14 | Quick multi-row entry (spreadsheet-style grid for a batch of devices) | P1 | M | US-C1 |
| 15 | Bulk CSV import with per-row validation report (promoted from M3) | P0 | L | US-E3 |
| 16 | Bulk export (filtered, audited) (promoted from M3) | P1 | S | US-E3 |

## Deferred — Document Pipeline (reactivate when sample documents arrive)

| # | Item | Pri | Size | Stories |
|---|------|-----|------|---------|
| D1 | Pre-signed S3 upload + document metadata + status model | — | M | US-B1 |
| D2 | Virus scanning stage (quarantine + notification) | — | M | US-B6 |
| D3 | `IDocumentParser` abstraction + Textract adapter | — | M | US-B2 |
| D4 | Bedrock extraction adapter (structured output + confidence) | — | L | US-B2 |
| D5 | Validation stage: required fields, formats, duplicate detection | — | M | US-B5 |
| D6 | Review queue API + admin review UI (side-by-side) | — | L | US-B4 |
| D7 | Commit stage: accepted records → assets + document links; uploader notifications | — | M | US-B2, US-B3 |
| D8 | Customer document history + per-document outcomes UI | — | M | US-F1, US-B3 |

Design docs 06 §3–4, 08 (document tables), and 11 (C1, A2 wireframes) remain valid for reactivation. Schema note: document/extraction tables ship in the initial migration anyway (they cost nothing empty and avoid a disruptive migration later) — **decision for Daniel**: confirm or drop from V001.

## Milestone 3 — Admin Visibility & Hardening (Week 7–10)

| # | Item | Pri | Size | Stories |
|---|------|-----|------|---------|
| 21 | Dashboards: squadron + system (counts, activity; pipeline widgets deferred) | P0 | M | US-E1, US-E2 |
| 22 | Transfer workflow (request/approve) | P1 | M | US-C3 |
| 25 | Reports: inventory, status/age (processing throughput deferred) | P1 | M | US-E2 |
| 26 | Profile page | P1 | S | US-F2 |
| 27 | Monitoring/alerting completion; dashboards; runbooks | P0 | M | US-G1 |
| 28 | E2E test suite over critical paths; load test vs NFR-2/3 | P0 | L | all |
| 29 | Security review, pen-test checklist, OWASP ASVS L2 audit | P0 | M | — |
| 30 | Deployment to production GovCloud; go-live checklist | P0 | M | US-G2 |

## Post-MVP (P2, unscheduled)

Reviewer-correction feedback loop for parser tuning (FR-29/US-B4), SES email notifications (OQ4), barcode scanning, DPAS integration, configurable asset fields, multi-tenancy, mobile app.

## De-scoping triggers (PM note)

If Milestone 2/3 slips >1 week: cut items 14, 16, 22, 25, 26 from MVP (P1s). Items 28, 29 are never cut. Timeline pressure eased ~2 weeks by the pipeline deferral.
