# Session Handoff — Project Status

**Updated:** 2026-07-06 · read this first in a new session, then `docs/20-design-review.md`.

## Where we are

Planning approved; Milestone 0 in progress. `main` @ 5 commits (docs → scaffold → schema → terraform → runbook), working tree clean, no remote yet.

## Decisions in force

- GovCloud CUI, CAC/PIV only (doc 09); F# end-to-end: Fable 5 client / Giraffe server / shared domain (doc 19).
- Document pipeline **deferred** (no sample docs); manual entry + CSV import is the MVP path (docs 01/05/18, rescoped 2026-07-06). Document tables ship in schema anyway (V001, per Daniel).
- Process: feature branches off main, squash merge, conventional commits, never commit to main directly (doc 14).

## Immediate next steps (in order)

1. Verify sandbox network access (`curl` NuGet/GitHub); if open: install .NET 8 SDK in the VM, `dotnet tool restore && dotnet build -warnaserror && dotnet test` — first honest build, fix findings on `bugfix/` branch.
2. GitHub push: existing repo name `asset-tracker` is taken on Daniel's account — he's choosing reuse vs. new name. Runbook: `docs/21-github-setup.md`. If renamed, update runbook.
3. Then: `feature/aws-auth` (CAC/mTLS spike — top risk, doc 18) and `feature/asset-crud` (manual entry, backlog #9/#13).

## Open items needing Daniel/sponsor

DoD PKI trust store + OCSP path (OQ1) · sample documents (pipeline reactivation trigger) · retention confirmation (OQ3) · SES permitted? (OQ4) · GitHub repo name decision.

## Environment notes

Cowork sandbox previously had no .NET/Postgres/Terraform and restricted egress; CI (`.github/workflows/pr.yml`) is the verification gate until local tooling works. Delete permission for the project folder must be re-granted per session for git to work (`allow_cowork_file_delete`).
