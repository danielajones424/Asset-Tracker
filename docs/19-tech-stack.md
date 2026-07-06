# Technology Stack — Recommendations & Rationale

**Status:** Draft for review. Rule applied throughout: boring, proven, smallest thing that meets the requirement (ponytail); one language end-to-end where it pays.

## Frontend

| Choice | Why | Rejected alternatives |
|---|---|---|
| **Fable 5 (F#→JS)** | Mandated; shared domain/DTO types with server eliminate contract drift | — |
| **React via Feliz** | Fable's most mature rendering target; ecosystem access | Plain Fable.React (Feliz is the maintained idiom) |
| **Elmish** | Predictable state for a form/queue-heavy app; time-travel debugging | Redux-style JS interop (loses F# typing) |
| **Vite** | Standard fast bundler for Fable 5 | Webpack (slower, legacy) |
| **CSS: vanilla + design tokens** | Small surface; wireframe token system (doc 11); no Tailwind compile pipeline in an F# toolchain | Tailwind (extra toolchain), CSS-in-JS (poor Fable fit) |
| **Playwright** | E2E across the real browser + CAC test-cert handling | Cypress (weaker cert/mTLS story) |

## Backend

| Choice | Why | Rejected alternatives |
|---|---|---|
| **F# on .NET 8 LTS, Giraffe (ASP.NET Core)** | One language with client; functional composition over ASP.NET's hardened HTTP core (Kestrel, middleware, health checks); LTS support window covers project life | Node/TS (loses shared types), Python (second language; weaker typing), Saturn (thin sugar over Giraffe — skip a layer) |
| **Dapper + Npgsql** | Thin, explicit SQL — matches "no magic" posture, easy EXPLAIN discipline | EF Core (F# fit is awkward; migrations magic vs. our SQL-first rule) |
| **Flyway-style SQL migrations (dbup or Flyway)** | Reviewable, ordered, no ORM coupling | EF migrations |
| **Serilog (JSON) + CloudWatch** | Structured logging standard on .NET | — |
| **Expecto + FsCheck + Testcontainers** | Idiomatic F# testing; property tests for validators/parsers; real Postgres in CI | xUnit (fine, but Expecto composes better in F#) |

## Document pipeline

| Choice | Why | Rejected alternatives |
|---|---|---|
| **Amazon Textract** | Managed OCR/table extraction in GovCloud; zero OCR ops | Tesseract self-hosted (ops burden, worse tables) |
| **Amazon Bedrock (Claude)** | Managed structured extraction with confidence; GovCloud availability; swappable via `IDocumentParser` | Self-hosted models (ops+accreditation burden), regex-only (brittle on real docs) |
| **SQS** | Durable staging, retries, DLQ, scaling signal | Step Functions (more moving parts than 5 linear stages justify — ADR-2), Kafka (wild overkill) |
| **ClamAV on ECS** | Proven scanner, GovCloud-safe | GuardDuty S3 malware scan *preferred if available in region* — decide Milestone 0 |

## Data

| Choice | Why | Rejected alternatives |
|---|---|---|
| **PostgreSQL 16 (RDS Multi-AZ)** | Relational hierarchy + transactions + FTS/trigram search in one engine; smallest stack that meets every data requirement including search (ADR-4) | Aurora (cost premium unjustified at this scale; migration path exists), DynamoDB (relational/query model mismatch), +OpenSearch (second store to secure/sync — only if search NFR fails) |
| **S3 (+ Object Lock for audit exports)** | Documents, exports, backups | — |

## Infrastructure & delivery

| Choice | Why | Rejected alternatives |
|---|---|---|
| **AWS GovCloud, ECS Fargate, ALB(mTLS), VPC endpoints, KMS, Secrets Manager, CloudWatch, GuardDuty/Security Hub** | Justified per-service in doc 07 | EKS (Kubernetes tax with no payoff at this size), Lambda API (mTLS/session/RDS-connection mismatch) |
| **Terraform** | Multi-account GovCloud modules, plan-in-PR review flow, team familiarity in IaC hiring pool | CDK (ties IaC to app language runtime; Terraform's GovCloud track record is deeper), CloudFormation raw (verbose) |
| **GitHub Actions** | Mandated GitHub; OIDC to AWS; environments/approvals built in | Jenkins/CodePipeline (ops burden / weaker PR integration) |
| **Docker (distroless .NET images)** | Standard, scanned by Trivy | — |

## Cross-cutting rationale

One language (F#) across client, server, and shared domain is the highest-leverage decision: validation logic, DTOs, and domain types are written once and cannot drift; a small team maintains one mental model. Everything else follows the pattern "managed service over self-hosted, SQL over ORM magic, one datastore until metrics prove otherwise." Each rejected alternative is re-openable via the ADR process — none of these choices are one-way doors except GovCloud itself.
