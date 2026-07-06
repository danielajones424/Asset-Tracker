-- V001: Initial schema per docs/08-database-schema.md
-- Document/extraction tables included per decision 2026-07-06 (pipeline deferred, schema ships).

CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- ── Enums ────────────────────────────────────────────────────────────────
CREATE TYPE app_role AS ENUM ('unit_member','unit_custodian','squadron_admin','system_admin');
CREATE TYPE device_type AS ENUM ('desktop','laptop','phone','tablet','monitor','network','peripheral','other');
CREATE TYPE asset_condition AS ENUM ('new','good','fair','poor','unserviceable');
CREATE TYPE asset_status AS ENUM ('in_use','in_storage','in_repair','pending_transfer','transferred','disposed');
CREATE TYPE doc_status AS ENUM ('uploaded','scanning','quarantined','parsing','extracting','validating','needs_review','completed','failed');
CREATE TYPE candidate_disposition AS ENUM ('auto_accepted','pending_review','accepted','corrected','merged','rejected');
CREATE TYPE review_decision AS ENUM ('accept','correct','merge','reject');

-- ── updated_at convention ────────────────────────────────────────────────
CREATE FUNCTION set_updated_at() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN NEW.updated_at = now(); RETURN NEW; END $$;

-- ── Hierarchy ────────────────────────────────────────────────────────────
CREATE TABLE squadron (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name        text NOT NULL UNIQUE,
  code        text NOT NULL UNIQUE,
  is_active   boolean NOT NULL DEFAULT true,
  created_at  timestamptz NOT NULL DEFAULT now(),
  updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE unit (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  squadron_id uuid NOT NULL REFERENCES squadron ON DELETE RESTRICT,
  name        text NOT NULL,
  code        text NOT NULL,
  is_active   boolean NOT NULL DEFAULT true,
  created_at  timestamptz NOT NULL DEFAULT now(),
  updated_at  timestamptz NOT NULL DEFAULT now(),
  UNIQUE (squadron_id, code)
);

-- ── Users ────────────────────────────────────────────────────────────────
CREATE TABLE app_user (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  edipi           text NOT NULL UNIQUE,
  cert_subject_dn text NOT NULL,
  display_name    text NOT NULL,
  email           text,
  phone           text,
  role            app_role NOT NULL,
  unit_id         uuid REFERENCES unit,
  squadron_id     uuid REFERENCES squadron,
  is_active       boolean NOT NULL DEFAULT true,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT role_scope_consistent CHECK (
    (role IN ('unit_member','unit_custodian') AND unit_id IS NOT NULL)
    OR (role = 'squadron_admin' AND squadron_id IS NOT NULL)
    OR (role = 'system_admin')
  )
);

-- ── Assets ───────────────────────────────────────────────────────────────
CREATE TABLE asset (
  id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  unit_id           uuid NOT NULL REFERENCES unit ON DELETE RESTRICT,
  asset_tag         text NOT NULL UNIQUE,
  serial_number     text NOT NULL,
  device_type       device_type NOT NULL,
  make              text NOT NULL,
  model             text NOT NULL,
  os_name           text,
  os_version        text,
  mac_addresses     macaddr[],
  condition         asset_condition,
  status            asset_status NOT NULL DEFAULT 'in_use',
  assigned_to       text,
  location          text,
  acquisition_date  date,
  acquisition_cost  numeric(12,2),
  warranty_expiry   date,
  notes             text,
  source_confidence numeric(3,2),
  created_at        timestamptz NOT NULL DEFAULT now(),
  updated_at        timestamptz NOT NULL DEFAULT now(),
  -- serials collide across manufacturers; org-unique key is asset_tag (design review fix)
  UNIQUE (make, serial_number)
);

CREATE TABLE asset_history (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  asset_id    uuid NOT NULL REFERENCES asset,
  changed_by  uuid NOT NULL REFERENCES app_user,
  changed_at  timestamptz NOT NULL DEFAULT now(),
  field       text NOT NULL,
  old_value   text,
  new_value   text,
  document_id uuid  -- FK added after document table
);

-- ── Documents & extraction (pipeline deferred; schema ships empty) ──────
CREATE TABLE document (
  id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  unit_id        uuid NOT NULL REFERENCES unit,
  uploaded_by    uuid NOT NULL REFERENCES app_user,
  status         doc_status NOT NULL DEFAULT 'uploaded',
  failure_reason text,
  completed_at   timestamptz,
  created_at     timestamptz NOT NULL DEFAULT now(),
  updated_at     timestamptz NOT NULL DEFAULT now()
);

ALTER TABLE asset_history
  ADD CONSTRAINT asset_history_document_fk FOREIGN KEY (document_id) REFERENCES document;

CREATE TABLE file_metadata (
  id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  document_id       uuid NOT NULL UNIQUE REFERENCES document,
  original_filename text NOT NULL,
  content_type      text NOT NULL,
  size_bytes        bigint NOT NULL CHECK (size_bytes > 0 AND size_bytes <= 26214400),
  sha256            text NOT NULL,
  s3_bucket         text NOT NULL,
  s3_key            text NOT NULL,
  created_at        timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE extraction_candidate (
  id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  document_id        uuid NOT NULL REFERENCES document,
  row_index          int NOT NULL,
  payload            jsonb NOT NULL,
  overall_confidence numeric(3,2) NOT NULL,
  validation_flags   text[] NOT NULL DEFAULT '{}',
  disposition        candidate_disposition NOT NULL DEFAULT 'pending_review',
  asset_id           uuid REFERENCES asset,
  created_at         timestamptz NOT NULL DEFAULT now(),
  UNIQUE (document_id, row_index)
);

CREATE TABLE extraction_review (
  id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  candidate_id uuid NOT NULL UNIQUE REFERENCES extraction_candidate,
  reviewed_by  uuid REFERENCES app_user,
  reviewed_at  timestamptz,
  decision     review_decision,
  corrections  jsonb,
  review_note  text
);

CREATE TABLE document_asset (
  document_id uuid NOT NULL REFERENCES document,
  asset_id    uuid NOT NULL REFERENCES asset,
  PRIMARY KEY (document_id, asset_id)
);

-- ── Audit log (append-only, monthly partitions) ──────────────────────────
CREATE TABLE audit_log (
  id             uuid NOT NULL DEFAULT gen_random_uuid(),
  occurred_at    timestamptz NOT NULL DEFAULT now(),
  actor_id       uuid,
  actor_edipi    text,
  action         text NOT NULL,
  entity_type    text,
  entity_id      uuid,
  detail         jsonb,
  correlation_id text,
  ip_address     inet,
  PRIMARY KEY (id, occurred_at)
) PARTITION BY RANGE (occurred_at);

-- bootstrap partitions; ongoing creation is an ops runbook item (docs/20 DBA)
CREATE TABLE audit_log_2026_07 PARTITION OF audit_log
  FOR VALUES FROM ('2026-07-01') TO ('2026-08-01');
CREATE TABLE audit_log_2026_08 PARTITION OF audit_log
  FOR VALUES FROM ('2026-08-01') TO ('2026-09-01');
CREATE TABLE audit_log_2026_09 PARTITION OF audit_log
  FOR VALUES FROM ('2026-09-01') TO ('2026-10-01');

-- ── Append-only enforcement (defense in depth; role grants in V002) ─────
CREATE FUNCTION reject_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION '% is append-only', TG_TABLE_NAME; END $$;

CREATE TRIGGER audit_log_append_only BEFORE UPDATE OR DELETE ON audit_log
  FOR EACH ROW EXECUTE FUNCTION reject_mutation();
CREATE TRIGGER asset_history_append_only BEFORE UPDATE OR DELETE ON asset_history
  FOR EACH ROW EXECUTE FUNCTION reject_mutation();

-- ── updated_at triggers ──────────────────────────────────────────────────
CREATE TRIGGER squadron_updated BEFORE UPDATE ON squadron FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER unit_updated BEFORE UPDATE ON unit FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER app_user_updated BEFORE UPDATE ON app_user FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER asset_updated BEFORE UPDATE ON asset FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER document_updated BEFORE UPDATE ON document FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ── Indexes (docs/08 §4) ─────────────────────────────────────────────────
CREATE INDEX idx_unit_squadron ON unit (squadron_id);
CREATE INDEX idx_asset_unit ON asset (unit_id);
CREATE INDEX idx_asset_status ON asset (status);
CREATE INDEX idx_asset_type ON asset (device_type);
CREATE INDEX idx_asset_search ON asset USING gin (
  to_tsvector('simple', coalesce(make,'') || ' ' || coalesce(model,'') || ' ' || coalesce(assigned_to,''))
);
CREATE INDEX idx_asset_serial_trgm ON asset USING gin (serial_number gin_trgm_ops);
CREATE INDEX idx_asset_tag_trgm ON asset USING gin (asset_tag gin_trgm_ops);
CREATE INDEX idx_history_asset ON asset_history (asset_id, changed_at DESC);
CREATE INDEX idx_document_unit ON document (unit_id, created_at DESC);
CREATE INDEX idx_document_status ON document (status);
CREATE INDEX idx_candidate_document ON extraction_candidate (document_id);
CREATE INDEX idx_candidate_pending ON extraction_candidate (created_at)
  WHERE disposition = 'pending_review';
CREATE INDEX idx_audit_occurred ON audit_log (occurred_at);
CREATE INDEX idx_audit_actor ON audit_log (actor_id, occurred_at);
CREATE INDEX idx_audit_entity ON audit_log (entity_type, entity_id, occurred_at);
