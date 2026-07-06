# Product Requirements Document — Asset Tracking System

**Version:** 1.0 · **Date:** 2026-07-06 · **Owner:** Product Manager · **Status:** Draft for review

## 1. Problem

Squadrons track IT devices (desktops, laptops, phones, peripherals) across their subordinate Units using paper hand receipts and ad-hoc spreadsheets. Property records are inconsistent, hard to audit, and re-keyed manually from PDF inventory documents. There is no single source of truth, no audit trail, and no visibility for squadron leadership.

## 2. Solution

A CUI-capable web application, hosted in AWS GovCloud, where unit custodians upload asset documents (PDFs), an automated pipeline (OCR + AI extraction) converts them into structured asset records, low-confidence extractions are routed to administrator review, and all parties get role-appropriate search, reporting, and full audit/asset history.

## 3. Goals

- G1: Single authoritative asset database with Squadron → Unit → Asset hierarchy.
- G2 (deferred with document pipeline): ≥80% of uploaded asset documents processed to structured records without manual re-keying. MVP substitute: manual entry of an asset takes < 60 seconds; bulk CSV import covers volume entry.
- G3: Complete, immutable audit trail of every data change.
- G4: Role-based access — users see only their unit/squadron scope; admins see everything.
- G5: Production deployment in AWS GovCloud suitable for CUI, CAC/PIV authenticated.

## 4. Non-Goals (v1)

- Mobile native apps (responsive web only).
- Barcode/RFID scanning integration.
- Integration with external property systems (DPAS etc.) — extensibility point only.
- Multi-tenancy across separate organizations (single org; hierarchy provides scoping).
- Classified data. CUI is the ceiling.

## 5. Users

- **Unit Member** — views assets in their unit.
- **Unit Custodian** — uploads documents, manages assets for their unit ("customer").
- **Squadron Admin** — oversight of all units in their squadron.
- **System Administrator** — full visibility and management, document review queue, user/permission management.

## 6. Core Capabilities

| # | Capability | Priority |
|---|-----------|----------|
| C1 | CAC/PIV login for all users | P0 |
| C2 | PDF asset-document upload with status tracking | **Deferred** (2026-07-06: no sample documents available; revisit when sponsor provides forms) |
| C3 | Automatic parsing (Textract OCR → Bedrock AI extraction), confidence-scored | **Deferred** (with C2) |
| C4 | Manual review queue for low-confidence/duplicate/missing-data extractions | **Deferred** (with C2) |
| C4b | Manual asset entry (single + quick multi-row) as the primary data-entry path for MVP | P0 |
| C5 | Asset CRUD scoped by role; assets belong to exactly one Unit; units to one Squadron | P0 |
| C6 | Search, filter, sort across assets/documents | P0 |
| C7 | Audit log (who/what/when) and per-asset history | P0 |
| C8 | Admin portal: dashboards, squadron/unit/customer management, document review | P0 |
| C9 | Bulk import (CSV) and export | P1 |
| C10 | Reporting & analytics (inventory summaries, processing metrics) | P1 |
| C11 | Virus scanning of uploads | P0 (security) |
| C12 | Monitoring, logging, alerting | P0 (ops) |

## 7. Device Asset Model (summary)

Asset tag, serial number, device type (desktop/laptop/phone/tablet/monitor/peripheral/network/other), make, model, OS/version, MAC address(es), condition, status (in-use/in-storage/in-repair/transferred/disposed), assigned user, location, acquisition date/cost, warranty expiry, unit, notes. Full schema in doc 08.

## 8. Success Metrics

- 100% of asset changes captured in audit log.
- ≥80% extraction auto-accept rate after tuning; <5% extraction field error rate post-review.
- Document upload→parsed median < 2 minutes.
- p95 page load < 2s; API p95 < 500ms.
- Zero critical security findings at launch review.

## 9. Constraints & Assumptions

- AWS GovCloud (US) regions only; only FedRAMP High–authorized services.
- CAC/PIV (x.509) is the sole authentication method; no passwords.
- Scale target: hundreds of users, ~100k assets, ~1k document uploads/month.
- MVP timeline 2–3 months with full scope — **flagged as high risk** (see roadmap doc 18 for de-scoping triggers).

## 10. Open Questions

- OQ1: Which DoD PKI intermediates must be trusted? (Need certificate policy from sponsor.)
- OQ2: Are there mandated document formats (e.g., DD Form 1150) we should optimize extraction for?
- OQ3: Retention period for uploaded source documents and audit logs (default proposal: 7 years).
- OQ4: Is email (SES) permitted for notifications in the target environment?
