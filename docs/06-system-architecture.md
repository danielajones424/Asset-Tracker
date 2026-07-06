# System Architecture — Asset Tracking System

**Status:** Draft for review. ADRs inline (ADR-1..6).

## 1. Style

Clean architecture, modular monolith API + asynchronous document pipeline. Not microservices (ADR-1): team size, MVP timeline, and single-domain scope make a well-layered monolith the maintainable choice; the pipeline is the only part with independent scaling needs, so it alone runs as queue-driven workers.

```
┌────────────────────────────┐        ┌───────────────────────────────┐
│  Fable 5 SPA (F# → JS)     │        │  Admin + Customer are one SPA │
│  Customer & Admin portals  │        │  with role-gated routes       │
└────────────┬───────────────┘        └───────────────────────────────┘
             │ HTTPS/mTLS (CAC) · JSON REST
┌────────────▼───────────────────────────────────────────┐
│  API (F# / Giraffe on ASP.NET Core, ECS Fargate)       │
│  ┌───────────────────────────────────────────────────┐ │
│  │ Presentation: HTTP handlers, request DTOs, authZ  │ │
│  ├───────────────────────────────────────────────────┤ │
│  │ Application: use cases, orchestration, ports      │ │
│  ├───────────────────────────────────────────────────┤ │
│  │ Domain: entities, value objects, invariants (pure)│ │  ← shared types
│  ├───────────────────────────────────────────────────┤ │    with Fable
│  │ Infrastructure: Postgres, S3, SQS, PKI, adapters  │ │    client
│  └───────────────────────────────────────────────────┘ │
└───────┬──────────────┬───────────────┬─────────────────┘
        │              │               │ enqueue
   PostgreSQL       S3 (docs,      SQS pipeline queue
   (RDS multi-AZ)   quarantine)        │
                                 ┌─────▼──────────────────────┐
                                 │ Pipeline workers (Fargate)  │
                                 │ scan → parse → extract →    │
                                 │ validate → commit/review    │
                                 │ (IDocumentParser adapters:  │
                                 │  Textract, Bedrock, rules)  │
                                 └─────────────────────────────┘
```

## 2. Layering rules

- Domain: pure F# — no I/O, no framework references. Compiled into both server and Fable client (shared validation, types, DTO contracts).
- Application: use-case functions taking ports (interfaces) — `IAssetRepository`, `IDocumentParser`, `IVirusScanner`, `IClock`, `IAuditWriter`. All business rules testable without infrastructure.
- Infrastructure: adapter implementations; only layer referencing AWS SDKs / Npgsql.
- Presentation: thin — deserialize, authorize, invoke use case, serialize. No business logic in handlers or UI (project rule).
- Dependencies point inward only.

## 3. Document pipeline (ADR-2: queue-driven stages, single worker binary)

One worker deployable consumes a single SQS queue; message carries `documentId` + `stage`. Each stage is idempotent, writes status to Postgres, and enqueues the next stage. DLQ + alarm for poison messages.

Stages: `Scan` (ClamAV sidecar/service) → `Parse` (Textract: text + tables) → `Extract` (Bedrock Claude: structured candidate assets w/ per-field confidence) → `Validate` (domain rules, duplicate check) → `Commit` (auto-accept) or `Review` (queue row for admins).

Parser swappability (FR-23): `Parse`/`Extract` call `IDocumentParser` implementations selected by configuration; a composite parser can chain OCR→AI. Adding a new parser = new adapter + config entry, no pipeline change.

Failure handling: per-stage retry (3× exponential backoff via SQS visibility), then `failed` status with reason surfaced to uploader; DLQ alarm to ops.

## 4. Data flow: upload → asset

1. Client requests upload slot → API validates (role, size, type) → returns pre-signed S3 PUT (quarantine prefix) + creates `document` row (`uploaded`).
2. S3 event → SQS → worker runs stages; status transitions persisted; client polls document status (simple polling, ADR-3 — WebSockets deferred; 5 s poll is adequate at this scale).
3. Auto-accepted candidates become `asset` rows + `asset_history` + `audit_log` entries, linked via `document_asset`.
4. Review-flagged candidates land in `extraction_review`; admin decisions commit/merge/reject with audit.

## 5. Key decisions (ADR summary)

- **ADR-1 Modular monolith over microservices.** Rationale: one domain, small team, 3-month MVP; pipeline isolated behind a queue gives the only scaling seam needed. Revisit at 10× load.
- **ADR-2 SQS-staged pipeline, idempotent stages.** Rationale: durability, retry semantics, horizontal scale, replaceable stages. Alternative (Step Functions) rejected for GovCloud cost/complexity at this volume — reconsider if orchestration grows branches.
- **ADR-3 Polling over WebSockets for status.** Simplicity; revisit if UX demands push.
- **ADR-4 PostgreSQL for everything including search.** GIN/trigram indexes satisfy NFR (100k assets, <1 s); OpenSearch deferred until proven necessary.
- **ADR-5 F# end-to-end (Fable client + Giraffe server + shared domain).** One language, shared types eliminate a class of contract bugs; see doc 19.
- **ADR-6 REST/JSON with OpenAPI, not GraphQL.** Simple resource model, easier to secure/audit, better tooling for the team size.

## 6. Cross-cutting

- AuthN/AuthZ: doc 09. Enforced in presentation layer middleware; scope filters injected into repository queries (no "filter in UI" ever).
- Audit: `IAuditWriter` port invoked by application layer on every mutating use case — not by DB triggers (keeps intent/actor context) — plus DB-level append-only protections (doc 08).
- Observability: structured logs (Serilog JSON) with correlation ID propagated client→API→worker via message attributes; metrics to CloudWatch EMF; health: `/health/live`, `/health/ready`.
- Configuration/secrets: environment + AWS Secrets Manager; no secrets in code or images.

## 7. Extensibility points

`IDocumentParser` (new extraction tech), notification port (SES later), export port (new formats), external-system sync port (DPAS later), asset-type metadata designed to allow configurable fields post-MVP.
