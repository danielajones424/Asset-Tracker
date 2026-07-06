# User Stories — Asset Tracking System

Format: As a <role>, I want <capability>, so that <benefit>. AC = acceptance criteria (abbreviated). Maps to SRS FRs.

## Epic A — Authentication & Access (FR-1..6)

- **US-A1** As any user, I want to log in with my CAC so that I never manage a password. AC: valid cert + provisioned account → session; revoked/expired cert → clear rejection; unprovisioned → "request access" guidance.
- **US-A2** As a System Admin, I want to provision users with roles and unit/squadron scope so that access follows duty assignment. AC: create/deactivate user, assign role+scope, changes audited, take effect on next request.
- **US-A3** As a Unit Member, I want to see only what I can act on so that the UI is simple. AC: no edit/upload affordances rendered for read-only roles; server rejects out-of-scope calls regardless.

## Epic B — Document Upload & Processing (FR-20..29)

- **US-B1** As a Unit Custodian, I want to drag-and-drop PDFs and watch their status so that I know processing is happening. AC: ≤25 MB PDF accepted; status chip advances through pipeline stages; failures show a human-readable reason.
- **US-B2** As a Unit Custodian, I want extracted assets created automatically so that I stop re-keying. AC: high-confidence records committed and visible in asset list, linked to source document.
- **US-B3** As a Unit Custodian, I want to see per-document results (created / needs review / failed rows) so that I can act on gaps. AC: document detail shows outcome per candidate record.
- **US-B4** As a System Admin, I want a review queue of low-confidence extractions so that bad data never enters silently. AC: queue sorted by age; side-by-side source PDF and editable fields with per-field confidence highlighting; accept/correct/merge/reject; all decisions audited.
- **US-B5** As a System Admin, I want duplicate candidates flagged against existing assets so that we don't double-book devices. AC: match on serial/asset tag shows existing record; merge or reject options.
- **US-B6** As a Security Engineer (system), I want every upload virus-scanned before parsing so that malicious files never reach processing. AC: EICAR test file quarantined, uploader notified, admin alerted.

## Epic C — Asset Management (FR-10..15)

- **US-C1** As a Unit Custodian, I want to create/edit assets in my unit so that records stay current. AC: allowable fields editable; validation on serial/MAC formats; every change in asset history.
- **US-C2** As a Unit Custodian, I want to change asset status (in-use, storage, repair, disposed) so that state reflects reality. AC: status transitions recorded with actor/timestamp; disposed = soft delete.
- **US-C3** As a Squadron Admin, I want to approve transfers between my units so that custody changes are controlled. AC: transfer request → pending → approve/deny; asset history records both units and approver.
- **US-C4** As a System Admin, I want to manage squadrons and units so that the hierarchy matches the org. AC: CRUD with referential safety (cannot delete unit with assets; must reassign first).

## Epic D — Search, History, Audit (FR-30..33)

- **US-D1** As any user, I want fast scoped search so that I find any device in seconds. AC: search by serial/tag/model/user returns in <1 s over 100k assets; results limited to my scope.
- **US-D2** As a Squadron Admin, I want an asset's full history so that I can answer "where has this been?" AC: chronological field-level changes with actors; includes transfers and document links.
- **US-D3** As a System Admin, I want a searchable audit log so that any action is attributable. AC: filter by actor/entity/action/date; append-only; export audited.

## Epic E — Admin Dashboard & Reporting (FR-34..36)

- **US-E1** As a Squadron Admin, I want a dashboard of my squadron so that property review is quick. AC: counts by unit/type/status; stale-record indicator; drill-down to lists.
- **US-E2** As a System Admin, I want processing metrics so that I can spot pipeline problems. AC: queue depth, auto-accept rate, failure rate, median processing time.
- **US-E3** As a System Admin, I want bulk CSV import/export so that legacy data onboards without an engineer. AC: template download; per-row validation report; import is atomic per row; export respects filters; both audited.

## Epic F — Customer Portal Profile & Documents (FR-40..42)

- **US-F1** As a Unit Custodian, I want my unit's document history so that I can find past uploads. AC: list with status, date, uploader, outcome summary; link to detail.
- **US-F2** As any user, I want to manage my profile display/contact fields so that colleagues can reach me. AC: identity fields (EDIPI, name from cert) read-only.

## Epic G — Platform (NFRs)

- **US-G1** As an operator, I want structured logs, metrics, and alerts so that incidents surface before users report them. AC: correlation ID per request; alarm on 5xx rate, queue age, DB CPU.
- **US-G2** As the org, I want automated deploys with rollback so that releases are boring. AC: pipeline per doc 13; one-step rollback.
