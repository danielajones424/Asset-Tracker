# Asset Tracking System

CUI-capable asset tracking for Squadron → Unit → Asset hierarchies. F# end-to-end: Fable 5 + Feliz/Elmish client, Giraffe server, shared domain. PostgreSQL, AWS GovCloud, CAC/PIV auth.

Planning documents: [`docs/`](docs/) — start with `docs/20-design-review.md`.

## Layout

```
src/Shared/    Pure domain + DTOs (compiled by both .NET and Fable — no package refs)
src/Server/    Giraffe API (presentation/application/infrastructure)
src/Client/    Fable 5 SPA (Feliz + Elmish, Vite)
tests/         Expecto test projects
db/migrations/ Versioned SQL (V###__*.sql), run by db/Migrate runner
infra/         Terraform (GovCloud)
```

## Prerequisites

.NET 8 SDK · Node 20+ · Docker (for Testcontainers/Postgres)

## Develop

```
dotnet tool restore
dotnet build -warnaserror
dotnet test
cd src/Client && npm install && npm run dev   # Vite dev server, proxies /api to :5000
```

## Process

Trunk-based, feature branches, squash merge, conventional commits — see `docs/14-branching-strategy.md`. Definition of done: `docs/16-testing-standards.md` + review + audit.
