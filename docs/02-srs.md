# Software Requirements Specification — Asset Tracking System

**Version:** 1.0 · **Status:** Draft for review · Companion to PRD (doc 01)

Requirement IDs: FR = functional, NFR = non-functional. Priority: M (must, MVP) / S (should) / C (could).

## 1. Functional Requirements

### 1.1 Authentication & Authorization
- FR-1 (M): The system shall authenticate all users via CAC/PIV x.509 client certificates validated against DoD PKI (path validation + revocation check).
- FR-2 (M): The system shall map certificate EDIPI/subject to a provisioned user account; unprovisioned certificates are rejected with an access-request message.
- FR-3 (M): The system shall enforce four roles: Unit Member, Unit Custodian, Squadron Admin, System Admin.
- FR-4 (M): Authorization shall be enforced server-side on every API call, scoped: Unit roles → own unit; Squadron Admin → own squadron's units; System Admin → all.
- FR-5 (M): Sessions shall expire after 15 minutes idle / 8 hours absolute.
- FR-6 (S): System Admin shall manage user accounts, role assignments, and unit/squadron membership via the admin portal.

### 1.2 Hierarchy & Asset Management
- FR-10 (M): The system shall model Squadron → Unit → Asset; each Asset belongs to exactly one Unit, each Unit to exactly one Squadron.
- FR-11 (M): CRUD on squadrons/units (System Admin); squadron admins may edit their own squadron's units' descriptive fields.
- FR-12 (M): Asset fields per device model in doc 08; serial number + asset tag unique system-wide (soft rule: duplicate detection warns, System Admin can override with justification).
- FR-13 (M): Unit Custodians shall create/edit assets in their unit (allowable fields); status transitions recorded in asset history.
- FR-14 (M): Asset transfer between units shall require Squadron Admin (intra-squadron) or System Admin (cross-squadron) approval and shall be recorded in history.
- FR-15 (S): Soft delete only; disposed assets remain queryable with status=disposed.

### 1.3 Document Upload & Processing
- FR-20 (M): Unit Custodians shall upload PDF documents ≤ 25 MB via the customer portal; uploads go directly to S3 via pre-signed URL.
- FR-21 (M): Every upload shall be virus-scanned before processing; infected files quarantined and reported.
- FR-22 (M): Pipeline stages: uploaded → scanning → parsing → extracting → validating → needs-review | completed | failed. Status visible to the uploader in near-real-time.
- FR-23 (M): Parsing shall be behind an `IDocumentParser` abstraction so OCR/AI/rule engines can be swapped or chained without redesign.
- FR-24 (M): Extraction shall produce candidate asset records with per-field confidence scores.
- FR-25 (M): Validation shall check required fields, formats (serial, MAC), duplicates against existing assets, and unit attribution.
- FR-26 (M): Records with any field confidence below threshold (configurable, default 0.85), duplicates, or missing required fields shall enter the admin review queue.
- FR-27 (M): Reviewers shall see the source document alongside extracted fields, and may correct, accept, merge-with-existing, or reject each candidate record.
- FR-28 (M): Accepted records shall be committed to the asset database linked to the source document; uploader notified of outcome and errors.
- FR-29 (S): Reviewer corrections shall be stored to enable future parser tuning.

### 1.4 Search, Reporting, History
- FR-30 (M): Full-text and fielded search over assets (serial, tag, make/model, assigned user, status, type) scoped by role; filter + sort + pagination on all list views.
- FR-31 (M): Per-asset history: every field change with actor, timestamp, old/new values.
- FR-32 (M): System-wide audit log (System Admin view; Squadron Admin sees own squadron): authentication events, CRUD, permission changes, document events, exports.
- FR-33 (M): Audit records shall be append-only and tamper-evident.
- FR-34 (M): Admin dashboard: asset counts by squadron/unit/type/status, processing queue depth, recent activity, error rates.
- FR-35 (S): Bulk CSV import with validation report; bulk export (CSV) of any filtered asset view; exports audited.
- FR-36 (S): Reports: inventory by unit/squadron, assets by status/age, processing throughput, review-queue SLA.

### 1.5 Customer Portal
- FR-40 (M): Profile view/edit (display name, contact info; identity fields immutable).
- FR-41 (M): Upload page with drag-drop, per-file status, processing-error display, document history per unit.
- FR-42 (M): Asset list/detail for own unit with search/filter and allowable-field editing (custodian).

## 2. Non-Functional Requirements

- NFR-1 Security (M): AWS GovCloud only; FedRAMP High services; NIST 800-171 control alignment for CUI; OWASP ASVS L2; TLS 1.2+ everywhere; encryption at rest (KMS) for S3, RDS, logs.
- NFR-2 Performance (M): API p95 < 500 ms; page interactive p95 < 2 s at 300 concurrent users; search over 100k assets < 1 s.
- NFR-3 Processing (M): Median upload→parsed < 2 min; pipeline horizontally scalable via queue.
- NFR-4 Availability (M): 99.5% monthly; RPO ≤ 1 h; RTO ≤ 4 h; multi-AZ database.
- NFR-5 Scalability (S): 10× growth (1M assets, thousands of users) without architectural change.
- NFR-6 Observability (M): Structured JSON logs with correlation IDs; metrics; health endpoints; alerting on error rate, queue depth, latency.
- NFR-7 Accessibility (M): WCAG 2.1 AA / Section 508.
- NFR-8 Auditability (M): Audit + asset history retained 7 years (pending OQ3); immutable storage.
- NFR-9 Maintainability (M): Clean architecture layering; ≥80% unit coverage on domain/application layers; ADRs for significant decisions.
- NFR-10 Browser support (M): Current Chrome/Edge/Firefox; no IE.

## 3. Interfaces

- Web UI (Fable 5 SPA) ↔ REST API (JSON, OpenAPI-documented).
- API ↔ PostgreSQL (RDS), S3, SQS, Textract, Bedrock, ClamAV scanner.
- Outbound notifications: in-app; SES email pending OQ4.

## 4. Acceptance

Each FR requires automated test coverage (unit and/or integration) plus, for M items, an E2E scenario. Traceability maintained in the backlog (doc 05).
