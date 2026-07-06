# AWS Architecture — GovCloud (US)

**Status:** Draft for review. All services below are FedRAMP High authorized in GovCloud (verify against the current AWS services-in-scope list before provisioning — availability changes).

## 1. Account & network layout

- **Accounts:** separate GovCloud accounts for `dev`, `staging`, `prod` under AWS Organizations; centralized CloudTrail + Config.
- **VPC (per env):** 2 AZs. Public subnets: ALB only. Private subnets: ECS tasks, RDS. VPC endpoints (S3, SQS, Secrets Manager, CloudWatch, Textract, Bedrock, ECR) so traffic never leaves the AWS backbone — required posture for CUI and removes NAT dependency for AWS calls.

## 2. Service selection & justification

| Service | Role | Why this service |
|---|---|---|
| **ECS Fargate** | API containers + pipeline workers | No EC2 fleet to patch (small team); long-lived mTLS-terminating API fits containers better than Lambda; same platform for workers keeps ops uniform. Lambda rejected for API due to mTLS/CAC termination needs and connection reuse to RDS. |
| **ALB** | TLS + **mTLS (client cert) termination** | ALB supports mutual TLS with trust store (DoD PKI CAs); passes cert to API via headers for validation/mapping. This is the linchpin of CAC auth (doc 09). |
| **RDS PostgreSQL, Multi-AZ** | System of record | Relational fit (hierarchy, FKs, transactions, audit); Multi-AZ meets RPO/RTO; encrypted with KMS CMK; automated backups + PITR. |
| **S3** | Documents (quarantine + clean buckets), exports, logs | Durable, KMS-encrypted, versioned; bucket policies deny non-VPC-endpoint access; Object Lock (compliance mode) on audit-export bucket. |
| **SQS** | Pipeline queue + DLQ | Durable async stage handoff, retries, backpressure; drives worker auto-scaling. |
| **Textract** | OCR: text + table extraction from PDFs | Managed OCR available in GovCloud; no OCR fleet to run. |
| **Bedrock (Claude)** | AI structuring of extracted text → candidate assets + confidence | Managed inference in GovCloud; no model hosting; swappable behind IDocumentParser. |
| **ECR** | Container registry | Image scanning on push; immutable tags. |
| **CloudWatch** | Logs, metrics, dashboards, alarms | Native, FedRAMP High; EMF metrics; alarm → SNS. |
| **CloudTrail + AWS Config** | API audit + drift/compliance rules | Control-plane accountability for the accreditation package. |
| **Secrets Manager** | DB creds, API keys; rotation | No secrets in env files/images. |
| **KMS (CMKs)** | Encryption at rest everywhere | Customer-managed keys with rotation; key policy least privilege. |
| **IAM** | Task roles, least privilege | Distinct role per component (API, worker, CI deploy). |
| **Route 53 + ACM** | DNS + server certs | ACM public/private certs for ALB. |
| **SNS** | Ops alerting fan-out | Alarm delivery to on-call. |
| **GuardDuty + Security Hub** | Threat detection, findings aggregation | Cheap, continuous detection appropriate for CUI. |
| **SES** | Email notifications (pending OQ4) | Post-MVP toggle. |

**CloudFront: not used.** Historically unavailable/limited in GovCloud and user base is CONUS + modest; the ALB serves the SPA's static bundle via the API container (or S3-behind-ALB). Revisit if latency data says otherwise.

**Virus scanning (ADR):** ClamAV as an ECS service consuming the scan stage (definitions auto-updated), rather than GuardDuty Malware Protection for S3 — verify current GovCloud availability of the latter; if available, prefer it and delete the ClamAV service.

## 3. Topology

```
User (CAC) ──TLS+client cert──► Route53 ─► ALB (mTLS, WAF)
                                            │
                              ┌─────────────┴─────────────┐
                              ▼                           ▼
                      ECS Fargate: API            S3 (pre-signed PUT
                      (private subnets)            direct upload)
                        │        │                        │ S3 event
                        ▼        ▼                        ▼
                   RDS Postgres  SQS  ◄───────────────────┘
                   (Multi-AZ)     │
                                  ▼
                      ECS Fargate: workers ──► Textract / Bedrock / ClamAV
                                  │                (via VPC endpoints)
                                  └──► RDS / S3 clean bucket
```

WAF (AWS WAF on ALB): managed rule sets (common, known-bad-inputs), rate limiting per IP.

## 4. Scaling & resilience

- API: 2+ tasks across AZs, target-tracking on CPU/req-count.
- Workers: scale on SQS queue depth (0–N; scale-to-near-zero off-hours).
- RDS: Multi-AZ failover; read replica deferred until metrics justify (NFR-5).
- Backups: RDS PITR (35 d) + weekly logical dump to S3; S3 versioning + replication to second GovCloud region for DR (RPO 1 h met by PITR; RTO 4 h via IaC re-provision runbook).

## 5. Environments & cost posture

dev (single-AZ RDS, minimal tasks) ≈ $250/mo · staging ≈ $400/mo · prod ≈ $900–1,200/mo (dominated by Multi-AZ RDS, Fargate, ALB; Textract/Bedrock usage-based at ~1k docs/mo is minor). Estimates to be validated in Milestone 0.

## 6. IaC

Terraform, state in S3 + DynamoDB lock, one root module per environment, shared modules for vpc/ecs-service/rds. All changes via PR + plan review (doc 13).
