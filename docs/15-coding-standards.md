# Coding Standards

**Status:** Draft for review. Enforced by tooling wherever possible; standards that can't be automated are review checklist items.

## 1. Guiding principle (ponytail rule)

The laziest solution that actually works wins. Before writing code ask, in order: does this need to exist (YAGNI)? Does .NET/F# core or the platform already do it? Can an existing dependency do it? Only then write it — and write the short version. Speculative abstraction is a defect; the only sanctioned extension points are those named in doc 06 §7. Deliberate shortcuts are marked `// ponytail: <what was deferred and why>` and harvested via `/ponytail-debt` each milestone.

## 2. F# (server, shared, client)

- Formatting: Fantomas, default settings, CI-enforced. No style debates.
- Modules + functions over classes; classes only where a framework demands them.
- Domain modeling: make illegal states unrepresentable — single-case unions for ids (`AssetId of Guid`), enums as DUs, `Result<'T,DomainError>` for fallible operations. No exceptions for domain flow; exceptions only for truly exceptional infrastructure failure.
- No `Unchecked.defaultof`, no nulls crossing layer boundaries (`Option` instead).
- Async: `task { }` on server; no blocking `.Result`/`.Wait()`.
- Shared project rules: pure, Fable-compatible (no BCL APIs Fable can't compile); DTOs and validation live here once, used by both sides.
- Dependency direction (doc 06 §2) enforced by project references — Domain references nothing; Application references Domain; Infrastructure and Presentation reference Application.

## 3. Client (Fable 5 + React via Feliz)

- Elmish for app-level state; local component state for purely local UI concerns. No business logic in views — views render state and dispatch messages.
- API calls only through the generated typed client over shared DTOs; no hand-built fetch bodies.
- Follow the design tokens/system from the wireframe direction (doc 11); no inline hex colors or ad-hoc spacing; permission-based rendering: hide, don't disable.
- Accessibility is code review scope: semantic elements, labels, focus management on route/dialog changes, keyboard paths for the review queue.

## 4. SQL & migrations

- Versioned migrations only (`V###__description.sql`); never edit an applied migration; expand/contract per doc 12 §4.
- All queries parameterized (Dapper/Npgsql parameters) — string-built SQL is a blocking review finding.
- New query patterns require an index check (EXPLAIN in the PR description for non-trivial queries).

## 5. General

- Naming: intention-revealing; no abbreviations except domain-standard (EDIPI, NSN).
- Comments explain *why*, not *what*; public module functions get doc comments.
- Errors surfaced to users are actionable and human-readable; correlation ID included.
- Logging: structured (Serilog message templates); never log CUI field values, cert contents, or session ids — log entity ids.
- No TODOs without a linked backlog item or `ponytail:` tag.
- PRs: ≤ ~400 lines changed, one concern, description states what/why/tests.

## 6. Tooling gate summary

Fantomas check, `dotnet build -warnaserror`, ESLint (JS glue only), gitleaks, CodeQL — all CI-blocking (doc 13).
