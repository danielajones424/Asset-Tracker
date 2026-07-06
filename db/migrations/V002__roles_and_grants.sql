-- V002: Database roles + least-privilege grants (docs/08 §3, docs/09 §3)
-- Roles are created by Terraform/bootstrap with NOLOGIN group semantics;
-- this migration is idempotent about their existence for local dev.

DO $$ BEGIN CREATE ROLE app_rw NOLOGIN; EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN CREATE ROLE app_ro NOLOGIN; EXCEPTION WHEN duplicate_object THEN NULL; END $$;

-- Read/write application role
GRANT USAGE ON SCHEMA public TO app_rw, app_ro;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_rw;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO app_ro;

-- Append-only: revoke mutation on audit/history from the app role (grants layer;
-- triggers in V001 are defense in depth)
REVOKE UPDATE, DELETE ON audit_log FROM app_rw;
REVOKE UPDATE, DELETE ON asset_history FROM app_rw;

-- Future partitions inherit via default privileges set by the migration owner
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT ON TABLES TO app_rw;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO app_ro;
