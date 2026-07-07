# Session Handoff — Project Status

**Updated:** 2026-07-06 · read this first in a new session, then `docs/20-design-review.md`.

## Where we are

Planning approved; Milestone 0 in progress. `main` @ 7 commits, working tree clean. Remote `origin` configured (git@github.com:danielajones424/asset-tracker.git) but unpushed — sandbox has no SSH credentials; push must happen from Daniel's machine or via PAT.

**2026-07-06 session:** sandbox egress now OPEN (NuGet + GitHub reachable). .NET SDKs 8.0.422 + 10.0.301 installed in VM (`$HOME/dotnet`; **10 is required** — Fable 5 tool package targets net10.0, tool restore fails on SDK 8 alone). `dotnet tool restore`, `build -warnaserror`, `test` (9/9) all green after one fix: FS3261 nullness error in `Validation.fs` `maxLen` (null comparison → `String.IsNullOrEmpty`), fixed on `bugfix/nullness-validation`, squash-merged, branch deleted.

## Decisions in force

- GovCloud CUI, CAC/PIV only (doc 09); F# end-to-end: Fable 5 client / Giraffe server / shared domain (doc 19).
- Document pipeline **deferred** (no sample docs); manual entry + CSV import is the MVP path (docs 01/05/18, rescoped 2026-07-06). Document tables ship in schema anyway (V001, per Daniel).
- Process: feature branches off main, squash merge, conventional commits, never commit to main directly (doc 14).

## Immediate next steps (in order)

1. ~~Local toolchain + first build~~ done 2026-07-06.
2. ~~Asset CRUD API~~ done 2026-07-06: `feature/asset-crud-api` squash-merged (backlog #9 API + #13 dup-probe endpoint). Endpoints: GET/POST `/api/v1/assets`, GET/PUT `/assets/{id}`, POST `/assets/{id}/status`, GET `/assets/check-duplicate`. Scoped queries per role, field-level history in-transaction, optimistic concurrency on `updatedAt`, CSRF header guard, ORDER-BY whitelist. Dev-auth seam (header principal, Development-only) unblocks work until CAC lands; test proves headers alone never authenticate. 33 tests green locally; Postgres integration suite is env-gated (`TEST_DATABASE_URL`) and wired into CI (postgres:16 service). CI also fixed: .NET 10 SDK required for Fable 5 tool (net10.0 payload). Local `feature/asset-crud-api` branch kept — origin has it and sandbox can't push/delete remote.
3. GitHub push: needs Daniel (sandbox has no git credentials). main is ahead several commits.
4. Next feature: `feature/asset-crud-ui` (#13 entry form, Fable/Feliz — consumes dup probe + shared validation) or `feature/aws-auth` spike (top risk; OQ1 may cap progress). UI recommended while OQ1 is open.
5. Server run config for dev: `ConnectionStrings__Default` + `ASPNETCORE_ENVIRONMENT=Development` + `AssetTracker__DevAuth=true`; dev principal via `X-Dev-UserId`/`X-Dev-Role`/`X-Dev-UnitId` headers (user row must exist in `app_user` for history FK).

## Open items needing Daniel/sponsor

DoD PKI trust store + OCSP path (OQ1) · sample documents (pipeline reactivation trigger) · retention confirmation (OQ3) · SES permitted? (OQ4) · GitHub repo name decision.

## Environment notes

Cowork sandbox previously had no .NET/Postgres/Terraform and restricted egress; CI (`.github/workflows/pr.yml`) is the verification gate until local tooling works. Delete permission for the project folder must be re-granted per session for git to work (`allow_cowork_file_delete`).
