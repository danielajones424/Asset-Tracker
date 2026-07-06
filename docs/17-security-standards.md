# Security Standards

**Status:** Draft for review. Target: NIST SP 800-171 alignment (CUI), OWASP ASVS Level 2. This doc is the working standard; the control-mapping spreadsheet for accreditation is a Milestone 3 deliverable.

## 1. Data classification & handling

- System ceiling: **CUI**. No classified data — stated in user-facing terms of use and upload page.
- CUI at rest: KMS CMK encryption on RDS, S3, EBS, CloudWatch Logs, SQS. In transit: TLS 1.2+ only (ALB policy), internal traffic via VPC endpoints.
- Logs and error messages never contain document contents, extracted field values, or certificates (doc 15 §5).
- Retention: documents + audit 7 years (OQ3); deletion only via retention policy, never user-initiated hard delete.

## 2. Application security (OWASP mapping)

| Threat | Control |
|---|---|
| Injection | Parameterized SQL only (review-blocking); no shell-outs; Bedrock prompts treat document text as data (delimited, never executed as instructions; output schema-validated) |
| Broken authN | CAC/PIV mTLS only (doc 09); no fallback; OCSP revocation; session hardening (`__Host-`, HttpOnly, SameSite=Strict, idle/absolute timeouts) |
| Broken authZ / IDOR | Server-side RBAC choke point; scope-required repositories; UUID ids; 404-on-out-of-scope; generated authZ matrix tests (doc 16) |
| XSS | React/Feliz encoding by default; no `dangerouslySetInnerHTML`; CSP: `default-src 'self'`, no inline script; PDF viewing via sandboxed viewer, served with `Content-Disposition` + `X-Content-Type-Options: nosniff` |
| CSRF | SameSite=Strict + custom header requirement on mutations |
| SSRF | No user-supplied URLs fetched anywhere |
| Insecure upload | Type allowlist (PDF only), 25 MB cap, content sniffing (magic bytes, not extension), ClamAV scan pre-processing, quarantine bucket separation, pre-signed URLs (5 min, content-length/type conditions), uploads never executed or served from origin domain path |
| Rate limiting | WAF per-IP rules + app-level limits on upload-slot creation and search |
| Sensitive data exposure | Problem+json errors carry no internals; stack traces never leave the server |
| Vulnerable components | Dependabot + `dotnet list --vulnerable` + Trivy image scans, CI-blocking |

Security headers: HSTS (preload), CSP, X-Content-Type-Options, Referrer-Policy: no-referrer, Permissions-Policy minimal.

## 3. Infrastructure security

- Least-privilege IAM per component (doc 09 §3); no wildcard resources; permission boundaries on CI roles.
- Private subnets for all compute/data; security groups deny-by-default; no SSH — ECS Exec (audited) for break-glass.
- S3: Block Public Access (account level), bucket policies require VPC endpoint + TLS; Object Lock on audit exports.
- KMS CMKs with rotation; key policies restrict to component roles.
- GuardDuty, Security Hub (AWS Foundational Best Practices + NIST 800-53 packs), AWS Config rules, CloudTrail (all regions, log-file validation) — findings triaged weekly, criticals same-day.
- Container hardening: distroless/chiseled base images, non-root user, read-only root FS, no privileged tasks; images pinned by digest.

## 4. Secrets & keys

Secrets Manager only; rotation (DB 90 d); no secrets in code/images/CI logs (gitleaks CI gate); GitHub OIDC short-lived cloud creds (doc 13 §2).

## 5. Audit & monitoring (security slice)

Security events audited: login success/failure/denied, session anomalies (cert/session mismatch), authZ denials, permission changes, exports, quarantine events. Alerts: spike in authZ denials, quarantined upload, GuardDuty finding, WAF block spike, DLQ growth. App audit log append-only (doc 08 §3) + CloudTrail for control plane.

## 6. Process

- Threat model review at each milestone boundary (STRIDE pass on new surfaces).
- `security-review` skill run on every auth/upload/pipeline PR; full review before prod go-live.
- Pen-test checklist (OWASP WSTG subset) executed against staging in Milestone 3; findings triaged as P0.
- Incident response runbook: detect → contain (disable user/rotate creds/quarantine) → assess CUI impact → report per org requirements → post-mortem. On-call via SNS.
- Dependency and base-image updates: weekly cadence, security patches within 72 h of disclosure for HIGH/CRITICAL.
