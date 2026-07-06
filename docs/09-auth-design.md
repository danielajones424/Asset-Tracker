# Authentication & Authorization Design

**Status:** Draft for review.

## 1. Authentication — CAC/PIV (x.509 mutual TLS)

### Flow

```
Browser (CAC in reader) ── TLS handshake with client cert ──► ALB (mTLS verify mode)
    ALB trust store: DoD Root/Intermediate CAs (from DoD PKE, OQ1)
    ALB validates chain ──► forwards request with X-Amzn-Mtls-Clientcert-* headers
                                   │
                            API auth middleware:
                            1. Parse leaf cert from header
                            2. Revocation check (OCSP; CRL fallback, cached)
                            3. Extract EDIPI from SAN (CN pattern lastname.firstname.middle.EDIPI)
                            4. Lookup active app_user by EDIPI
                            5. Issue session cookie (server-side session in Postgres)
```

- **ALB mTLS "verify" mode** does chain validation against the uploaded trust store; the API performs revocation checking and identity mapping (ALB does not do OCSP).
- **Unprovisioned but valid cert** → 403 page with access-request instructions (US-A1); event audited.
- **Session:** opaque random ID in `__Host-` cookie: `Secure; HttpOnly; SameSite=Strict`. Server-side session row (user, cert thumbprint, expiry). Idle timeout 15 min, absolute 8 h (FR-5). Re-handshake not required per request; cert thumbprint re-checked against session on each request.
- **No passwords anywhere.** No fallback auth in production. Dev/staging use a test CA issuing soft certs so the same code path is exercised (never a bypass flag).
- **Logout** destroys the server session; login/logout/denied events → audit_log.

### Why sessions, not JWT
Immediate revocation (deactivate user → next request fails), no token theft replay window, trivial with a single API. JWTs reconsidered only if third-party API consumers appear.

## 2. Authorization — RBAC with scope

### Role → permission matrix (summary)

| Capability | Member | Custodian | Sqn Admin | Sys Admin |
|---|---|---|---|---|
| View assets | own unit | own unit | own squadron | all |
| Create/edit assets | — | own unit | own squadron | all |
| Upload documents | — | own unit | own squadron | all |
| View documents/history | own unit | own unit | own squadron | all |
| Approve transfers | — | — | intra-squadron | all |
| Review extraction queue | — | — | — | ✔ |
| Manage units/squadrons | — | — | edit own (descriptive) | ✔ |
| Manage users/roles | — | — | — | ✔ |
| Audit log | — | — | own squadron | all |
| Bulk import/export | — | — | export own squadron | ✔ |
| Dashboards | — | own unit | own squadron | all |

### Enforcement design

- **Single choke point:** every use case receives a `SecurityContext { userId; role; unitId; squadronId }` built by middleware from the session — never from client input.
- **Scope at the query level:** repositories require the scope filter as a parameter (`AssetScope.Unit id | Squadron id | All`); there is no repository method that returns unscoped data to non-admin contexts. Compile-time help: scope is a required constructor argument, not an optional filter.
- **Deny by default:** route table maps endpoint → required permission; unmapped routes 403.
- **UI trimming is cosmetic only** (US-A3): the client hides unauthorized affordances, but the server is the enforcement boundary.
- **Permission changes take effect on next request** (session stores only identity; role/scope re-read per request from `app_user`, cached ≤ 60 s).

## 3. Service-to-service & infrastructure identity

- ECS task roles: API role (RDS via Secrets Manager creds, S3 presign, SQS send), worker role (SQS consume, S3 read/write, Textract, Bedrock, RDS), CI deploy role (ECR push, ECS update, Terraform). Least privilege, no wildcard resources.
- DB: `app_rw` (DML minus audit UPDATE/DELETE), `app_migrate` (DDL, CI only), `app_ro` (future reporting).
- Pre-signed S3 URLs: 5-minute expiry, single key, content-type + length conditions.

## 4. Threats addressed (summary — full model in doc 17)

Stolen session cookie (Secure/HttpOnly/SameSite + cert thumbprint binding), CSRF (SameSite=Strict + custom header check on mutations), privilege escalation (server-side matrix + scope-required repositories + tests per role), revoked CAC (OCSP at login + daily re-validation of active sessions), IDOR (every entity load passes through scope check; object IDs are UUIDs).

## 5. Open items

- OQ1: exact DoD PKI CA set for the trust store — needed from sponsor before staging.
- Decision needed: OCSP responder reachability from GovCloud VPC (may require egress allowlist).
