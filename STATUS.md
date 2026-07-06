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

1. ~~Local toolchain + first build~~ done 2026-07-06 (see above).
2. GitHub push: `origin` now points at `danielajones424/asset-tracker`; needs Daniel to push (2 commits ahead) or provide auth.
3. Then: `feature/aws-auth` (CAC/mTLS spike — top risk, doc 18; OQ1 may limit how far the spike can go) and `feature/asset-crud` (manual entry, backlog #9/#13 — unblocked).

## Open items needing Daniel/sponsor

DoD PKI trust store + OCSP path (OQ1) · sample documents (pipeline reactivation trigger) · retention confirmation (OQ3) · SES permitted? (OQ4) · GitHub repo name decision.

## Environment notes

Cowork sandbox previously had no .NET/Postgres/Terraform and restricted egress; CI (`.github/workflows/pr.yml`) is the verification gate until local tooling works. Delete permission for the project folder must be re-granted per session for git to work (`allow_cowork_file_delete`).
