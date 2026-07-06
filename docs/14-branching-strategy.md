# Branching Strategy

**Status:** Draft for review.

## 1. Model: trunk-based with short-lived feature branches

- `main` is always releasable; deploys flow from `main` via pipeline promotion (doc 13). No `develop`, no release branches (ADR: GitFlow rejected — release branches add ceremony a continuously-deployed 2-env promotion model doesn't need).
- All work on short-lived branches off `main`; target lifetime ≤ 3 days, ≤ ~400 changed lines per PR. Bigger features are sliced (feature flags / dark endpoints) rather than long-lived branches.
- **Never commit directly to `main`** (enforced by branch protection).

## 2. Branch naming

```
feature/<scope>-<summary>     feature/customer-upload, feature/aws-auth
bugfix/<scope>-<summary>      bugfix/upload-timeout
refactor/<scope>-<summary>    refactor/database-schema
infra/<summary>               infra/vpc-endpoints
docs/<summary>                docs/adr-007-search
hotfix/<summary>              hotfix/session-expiry   (still via PR; expedited review)
```

## 3. Commits

- Conventional Commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `infra:`, `chore:`; scope optional (`feat(pipeline): …`).
- Atomic: one logical change per commit; unrelated changes never share a commit or a PR (project rule).
- Squash merge to `main` — PR title becomes the conventional commit; history stays linear and revertable per feature.

## 4. Lifecycle

```
git switch -c feature/document-parser main
… atomic commits … push → PR (template: what/why/tests/screens)
CI green + review (correctness + /ponytail-review) → squash merge → branch auto-delete
```

Rebase on `main` to update branches (no merge commits into feature branches). Revert = `git revert` of the squash commit.

## 5. Tags & releases

Prod deploys tagged `release/YYYY-MM-DD[-n]` automatically by the release pipeline; changelog generated from conventional commits since last tag.
