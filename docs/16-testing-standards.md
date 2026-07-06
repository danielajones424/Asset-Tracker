# Testing Standards

**Status:** Draft for review.

## 1. Test pyramid & tools

| Level | Scope | Tools | Runs |
|---|---|---|---|
| Unit | Domain rules, application use cases (ports mocked), shared validation | Expecto + FsCheck (property tests for validation/parsers) | every PR, <1 min |
| Integration | Repositories vs real Postgres; S3/SQS adapters vs LocalStack; migrations | Expecto + Testcontainers | every PR |
| API | HTTP contract: authZ matrix, status codes, problem+json shapes | ASP.NET TestServer | every PR |
| E2E | Critical user journeys in a browser | Playwright | merge to main (dev env), nightly (staging) |
| Load | NFR-2/3 targets | k6 | pre-release, staging |

Coverage gate: ≥80% lines on Domain + Application (measured, CI-blocking). Infrastructure/UI measured but not gated — E2E covers them behaviorally.

## 2. Non-negotiable test areas

- **AuthZ matrix tests:** for every endpoint × role × scope combination, generated table-driven tests assert allow/deny. A new endpoint without matrix entries fails CI (registry test).
- **Pipeline stage tests:** each stage idempotent (run twice = same result), retry-safe, and correct on malformed input. Golden-file tests: sample PDFs → expected candidates (parser adapters mocked at the `IDocumentParser` seam; one live-service smoke per adapter in staging only).
- **Audit invariants:** property test — every mutating use case writes exactly one audit entry; append-only enforcement tested at DB level (UPDATE/DELETE must raise).
- **Migration tests:** every migration applies to a copy of the previous schema + representative data; rollback compatibility per expand/contract.
- **Validation properties:** FsCheck generators for serials/MACs/CSV rows — round-trip and rejection properties.

## 3. E2E critical journeys (must stay green)

1. CAC login (test CA cert) → session → logout.
2. Custodian uploads PDF → pipeline completes → assets appear (LocalStack + stub parser in dev; real services in staging nightly).
3. Low-confidence document → review queue → admin corrects + accepts → asset created, history + audit written.
4. Role scoping: custodian cannot see/edit another unit's asset (UI and direct API).
5. Transfer request → squadron admin approval → history reflects move.
6. Search returns expected asset by serial within scope.

## 4. Conventions

- Test names describe behavior: `"custodian cannot edit asset outside own unit"`.
- Arrange with builders/object mothers in a shared TestKit project; no copy-pasted fixtures.
- No test order dependence; integration tests get isolated schema per run; E2E data reset per suite.
- Flaky test = quarantine label + backlog item within 24 h; quarantined >1 sprint gets deleted or fixed.
- Bug fix PRs must include a failing-then-passing regression test.

## 5. Definition of done (testing slice)

Feature PRs ship with: unit tests for new domain/application logic, integration tests for new persistence/adapters, authZ matrix entries for new endpoints, and an E2E addition/update if the feature touches a critical journey.
