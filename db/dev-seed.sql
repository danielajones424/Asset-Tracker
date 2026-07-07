-- DEV ONLY seed — fixed GUIDs so .env.local, curl tests, and docs can
-- reference them verbatim. Never run against a shared environment.
-- Usage: psql "$DATABASE_URL" -f db/dev-seed.sql

INSERT INTO squadron (id, name, code)
VALUES ('00000000-0000-4000-8000-000000000001', 'Dev Squadron', 'DEV')
ON CONFLICT (id) DO NOTHING;

INSERT INTO unit (id, squadron_id, name, code)
VALUES ('00000000-0000-4000-8000-000000000002',
        '00000000-0000-4000-8000-000000000001',
        'Dev Unit 1', 'DEV1')
ON CONFLICT (id) DO NOTHING;

INSERT INTO app_user (id, edipi, cert_subject_dn, display_name, role, unit_id)
VALUES ('00000000-0000-4000-8000-000000000003',
        '9999999999',
        'CN=DEV.CUSTODIAN.9999999999,OU=DEV,O=DEV,C=US',
        'Dev Custodian',
        'unit_custodian',
        '00000000-0000-4000-8000-000000000002')
ON CONFLICT (id) DO NOTHING;
