# Session Handoff — Project Status

**Updated:** 2026-07-07 · read this first in a new session, then `docs/20-design-review.md`.

## Where we are

**MILESTONE 2026-07-07: first end-to-end asset saved.** Daniel's Mac runs the full stack: Postgres 16 (brew, via `pg_ctl` — brew services launchctl is flaky), migrations via dbup runner, dev-seed applied, API with dev auth, vite client → asset created through the form with history row. Bugs found and fixed on the way (each on a bugfix branch, squash-merged): Fable.Fetch throws on non-2xx (→ GlobalFetch), integration-test seed missing NOT NULL columns, Thoth `Encode.decimal` emits a JSON *string* but STJ expects a number (acquisitionCost 500), and a proper error handler (malformed body → 400 problem+json, unexpected → 500 with correlation id, never a stack trace). **Debt: add a client↔server DTO round-trip serialization test — the decimal bug lived exactly in the gap between client coders and server options.**

Planning approved; Milestone 0 in progress. `main` @ 7 commits, working tree clean. Remote `origin` configured (git@github.com:danielajones424/asset-tracker.git) but unpushed — sandbox has no SSH credentials; push must happen from Daniel's machine or via PAT.

**2026-07-06 session:** sandbox egress now OPEN (NuGet + GitHub reachable). .NET SDKs 8.0.422 + 10.0.301 installed in VM (`$HOME/dotnet`; **10 is required** — Fable 5 tool package targets net10.0, tool restore fails on SDK 8 alone). `dotnet tool restore`, `build -warnaserror`, `test` (9/9) all green after one fix: FS3261 nullness error in `Validation.fs` `maxLen` (null comparison → `String.IsNullOrEmpty`), fixed on `bugfix/nullness-validation`, squash-merged, branch deleted.

## Decisions in force

- GovCloud CUI, CAC/PIV only (doc 09); F# end-to-end: Fable 5 client / Giraffe server / shared domain (doc 19).
- Document pipeline **deferred** (no sample docs); manual entry + CSV import is the MVP path (docs 01/05/18, rescoped 2026-07-06). Document tables ship in schema anyway (V001, per Daniel).
- Process: feature branches off main, squash merge, conventional commits, never commit to main directly (doc 14).

## Immediate next steps (in order)

1. ~~Local toolchain + first build~~ done 2026-07-06.
2. ~~Asset CRUD API~~ done 2026-07-06: `feature/asset-crud-api` squash-merged (backlog #9 API + #13 dup-probe endpoint). Endpoints: GET/POST `/api/v1/assets`, GET/PUT `/assets/{id}`, POST `/assets/{id}/status`, GET `/assets/check-duplicate`. Scoped queries per role, field-level history in-transaction, optimistic concurrency on `updatedAt`, CSRF header guard, ORDER-BY whitelist. Dev-auth seam (header principal, Development-only) unblocks work until CAC lands; test proves headers alone never authenticate. 33 tests green locally; Postgres integration suite is env-gated (`TEST_DATABASE_URL`) and wired into CI (postgres:16 service). CI also fixed: .NET 10 SDK required for Fable 5 tool (net10.0 payload). Local `feature/asset-crud-api` branch kept — origin has it and sandbox can't push/delete remote.
3. ~~Asset entry form~~ done 2026-07-06: `feature/asset-crud-ui` squash-merged (#13). Elmish/Feliz form validated by the SAME shared code as the server, debounced dup probe, GET `/session` bootstrap, make/model/type persist after save for batch entry. Dev identity injected by the **vite proxy** (`src/Client/.env.local`: `VITE_DEV_USER_ID`, `VITE_DEV_ROLE`, `VITE_DEV_UNIT_ID`) — never in app code. Client JSON coders are hand-written in `Api.fs` to mirror the server's STJ config exactly. Fable.Fetch pinned 2.* (no 3.x exists).
4. **History reconciled 2026-07-06:** Daniel's PR #1 merge on GitHub was an older snapshot; local main kept via `-s ours` merge. Daniel: `git push origin main` (then delete remote `feature/asset-crud-api`).
5. Next: `feature/aws-auth` spike (top risk; OQ1 may cap progress), or Squadron/Unit mgmt (#7 — entry form currently trusts the session's unit; a unit picker needs #7).
6. Dev run: server `ConnectionStrings__Default` + `ASPNETCORE_ENVIRONMENT=Development` + `AssetTracker__DevAuth=true`; client `npm run dev` in src/Client (dev user row must exist in `app_user` for the history FK).
7. Sandbox quirk: file-sync creates undeletable empty `* 2` ghost dirs (obj/, fable_modules/) that break fable/msbuild runs in the VM — fix is `rm -rf` of the parent from Daniel's machine; they're gitignored.

## Open items needing Daniel/sponsor

DoD PKI trust store + OCSP path (OQ1) · sample documents (pipeline reactivation trigger) · retention confirmation (OQ3) · SES permitted? (OQ4) · GitHub repo name decision.

## Environment notes

Cowork sandbox previously had no .NET/Postgres/Terraform and restricted egress; CI (`.github/workflows/pr.yml`) is the verification gate until local tooling works. Delete permission for the project folder must be re-granted per session for git to work (`allow_cowork_file_delete`).
