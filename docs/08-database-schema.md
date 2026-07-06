# Database Schema — PostgreSQL 16

**Status:** Draft for review. Migrations via Flyway-style versioned SQL (`V001__…`). Naming: snake_case, singular tables. All tables: `id uuid PK default gen_random_uuid()`, `created_at`, `updated_at timestamptz`.

## 1. Entity relationship overview

```
squadron 1──* unit 1──* asset 1──* asset_history
                         │ *
                         │
app_user *──1 unit       │ document_asset (link)
   │                     │ *
   └──* audit_log     document 1──* extraction_candidate ──1 extraction_review
                         └──1 file_metadata
```

## 2. Tables

### squadron
| column | type | notes |
|---|---|---|
| name | text NOT NULL UNIQUE | |
| code | text NOT NULL UNIQUE | short designator |
| is_active | boolean default true | soft delete |

### unit
| column | type | notes |
|---|---|---|
| squadron_id | uuid NOT NULL FK→squadron | ON DELETE RESTRICT |
| name, code | text NOT NULL | UNIQUE(squadron_id, code) |
| is_active | boolean | |

### app_user
| column | type | notes |
|---|---|---|
| edipi | text NOT NULL UNIQUE | from CAC; identity key |
| cert_subject_dn | text NOT NULL | last-seen subject |
| display_name, email, phone | text | profile |
| role | app_role enum NOT NULL | unit_member \| unit_custodian \| squadron_admin \| system_admin |
| unit_id | uuid FK→unit | required for unit roles |
| squadron_id | uuid FK→squadron | required for squadron_admin |
| is_active | boolean | deactivation, not deletion |

CHECK: role/scope consistency (unit roles ⇒ unit_id NOT NULL; squadron_admin ⇒ squadron_id NOT NULL).

### asset
| column | type | notes |
|---|---|---|
| unit_id | uuid NOT NULL FK→unit ON DELETE RESTRICT | |
| asset_tag | text NOT NULL UNIQUE | org property tag — the org-unique key |
| serial_number | text NOT NULL | UNIQUE(make, serial_number) — serials are only unique per manufacturer; cross-make collisions are legitimate |
| device_type | device_type enum NOT NULL | desktop, laptop, phone, tablet, monitor, network, peripheral, other |
| make, model | text NOT NULL | |
| os_name, os_version | text | |
| mac_addresses | macaddr[] | 0..n interfaces |
| condition | asset_condition enum | new, good, fair, poor, unserviceable |
| status | asset_status enum NOT NULL default 'in_use' | in_use, in_storage, in_repair, pending_transfer, transferred, disposed |
| assigned_to | text | free-text person/office (not FK — assignees often aren't users) |
| location | text | |
| acquisition_date | date | |
| acquisition_cost | numeric(12,2) | |
| warranty_expiry | date | |
| notes | text | |
| source_confidence | numeric(3,2) | overall extraction confidence if machine-created |

Duplicate policy (FR-12): asset_tag and (make, serial_number) UNIQUE constraints are hard; admin "override" is implemented as merge/correct in review — never two live rows for one physical device. Duplicate *detection* in the pipeline additionally warns on serial-only matches across makes.

### asset_history (append-only)
| column | type | notes |
|---|---|---|
| asset_id | uuid NOT NULL FK→asset | |
| changed_by | uuid NOT NULL FK→app_user | |
| changed_at | timestamptz NOT NULL default now() | |
| field | text NOT NULL | column name or 'transfer' / 'created' |
| old_value, new_value | text | canonical string form |
| document_id | uuid FK→document | when change originated from a document |

### document
| column | type | notes |
|---|---|---|
| unit_id | uuid NOT NULL FK→unit | attribution |
| uploaded_by | uuid NOT NULL FK→app_user | |
| status | doc_status enum NOT NULL | uploaded, scanning, quarantined, parsing, extracting, validating, needs_review, completed, failed |
| failure_reason | text | human-readable |
| completed_at | timestamptz | |

### file_metadata
| column | type | notes |
|---|---|---|
| document_id | uuid NOT NULL UNIQUE FK→document | 1:1 |
| original_filename | text NOT NULL | sanitized |
| content_type | text NOT NULL | application/pdf |
| size_bytes | bigint NOT NULL | ≤ 25 MB enforced app-side too |
| sha256 | text NOT NULL | dedupe + integrity |
| s3_bucket, s3_key | text NOT NULL | current location (quarantine→clean) |

### extraction_candidate
| column | type | notes |
|---|---|---|
| document_id | uuid NOT NULL FK→document | |
| row_index | int NOT NULL | position in source |
| payload | jsonb NOT NULL | extracted fields + per-field confidence |
| overall_confidence | numeric(3,2) NOT NULL | |
| validation_flags | text[] | missing_field, bad_format, duplicate, low_confidence |
| disposition | candidate_disposition enum | auto_accepted, pending_review, accepted, corrected, merged, rejected |
| asset_id | uuid FK→asset | resulting/merged asset |

### extraction_review
| column | type | notes |
|---|---|---|
| candidate_id | uuid NOT NULL UNIQUE FK→extraction_candidate | |
| reviewed_by | uuid FK→app_user | |
| reviewed_at | timestamptz | |
| decision | review_decision enum | accept, correct, merge, reject |
| corrections | jsonb | field-level edits (kept for parser tuning, FR-29) |
| review_note | text | |

### document_asset (link)
`document_id` + `asset_id` composite PK — assets tracéable to source documents (n:m; one doc creates many assets; an asset may be touched by many docs).

### audit_log (append-only)
| column | type | notes |
|---|---|---|
| occurred_at | timestamptz NOT NULL default now() | |
| actor_id | uuid FK→app_user | NULL for system |
| actor_edipi | text | denormalized for permanence |
| action | text NOT NULL | e.g. asset.update, auth.login, export.csv |
| entity_type, entity_id | text / uuid | target |
| detail | jsonb | diff or context |
| correlation_id | text | request/pipeline trace |
| ip_address | inet | |

Partitioned by month (`PARTITION BY RANGE (occurred_at)`) for 7-year retention management.

## 3. Integrity & append-only enforcement

- `REVOKE UPDATE, DELETE ON audit_log, asset_history FROM app_rw;` — app role can only INSERT/SELECT. Trigger raises exception on UPDATE/DELETE as defense in depth.
- App connects as `app_rw` (no DDL); migrations run as separate `app_migrate` role.
- All FKs RESTRICT by default; soft delete via `is_active`/status everywhere user-visible.

## 4. Indexes

- asset: `(unit_id)`, `(status)`, `(device_type)`, UNIQUEs on tag/serial, `GIN (to_tsvector('simple', coalesce(make,'')||' '||coalesce(model,'')||' '||coalesce(assigned_to,'')))` for search, `pg_trgm` GIN on serial_number and asset_tag for partial matches.
- asset_history: `(asset_id, changed_at DESC)`.
- document: `(unit_id, created_at DESC)`, `(status)`.
- extraction_candidate: `(document_id)`, partial index `(disposition) WHERE disposition='pending_review'` for the queue.
- audit_log: `(occurred_at)`, `(actor_id, occurred_at)`, `(entity_type, entity_id, occurred_at)`.

## 5. Scaling notes

100k assets is small for Postgres; headroom to 10× with these indexes. Future: read replica for reporting, `audit_log` partition archival to S3, OpenSearch only if search NFR fails in practice (ADR-4).
