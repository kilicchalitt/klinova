# Development Guide

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (see `global.json`)
- Optional: Docker, an editor with EditorConfig support (Visual Studio 2026, Rider, VS Code + C# Dev Kit)

## Getting started

```bash
git clone https://github.com/kilicchalitt/klinova.git
cd klinova
dotnet tool restore        # installs the pinned dotnet-ef version
dotnet build
dotnet test
dotnet run --project src/Klinova.Web --launch-profile http
```

The Development environment seeds demo data. To use fixed passwords locally, keep them
out of the repository with user secrets:

```bash
dotnet user-secrets --project src/Klinova.Web set "Klinova:Seed:AdminPassword" "Local-Admin-2026"
dotnet user-secrets --project src/Klinova.Web set "Klinova:Seed:DemoPassword" "Local-Demo-2026"
```

Delete `src/Klinova.Web/data/` to start again from an empty database.

## Coding standards

- Nullable reference types, analyzers (`latest-recommended`) and code style are enforced
  at build time; warnings are errors.
- Formatting follows `.editorconfig`; CI runs `dotnet format --verify-no-changes`.
- File-scoped namespaces, `_camelCase` private fields, PascalCase constants.
- Identifiers and comments in English; user-facing text in Turkish.
- Business rules belong in `Klinova.Domain`; use cases in `Klinova.Application`;
  the web layer only binds input, authorises and renders.
- No inline `<script>` or `style=""` attributes — the Content-Security-Policy forbids them.
  Put behaviour in `wwwroot/js/site.js` (use `data-*` attributes) and styles in `site.css`.
- Never commit secrets, databases, `bin/`, `obj/`, or IDE state (see `.gitignore`).

## Database migrations

Each provider has its own migration history. When the model changes, add a migration
for **both**:

```bash
dotnet ef migrations add <Name> \
  -p src/Klinova.Infrastructure -s src/Klinova.Infrastructure \
  --context SqliteAppDbContext -o Persistence/Migrations/Sqlite

dotnet ef migrations add <Name> \
  -p src/Klinova.Infrastructure -s src/Klinova.Infrastructure \
  --context SqlServerAppDbContext -o Persistence/Migrations/SqlServer
```

CI fails if either provider has pending model changes.

## Tests

| Project | Scope |
| --- | --- |
| `Klinova.Domain.Tests` | Business rules: visit lifecycle, billing, national id, search normalisation. |
| `Klinova.Application.Tests` | Use cases against an in-memory SQLite database created from the migrations, including the audit interceptor. |
| `Klinova.Web.Tests` | The full application over HTTP: security headers, authentication, authorisation per role, page rendering, admission flow. |

```bash
dotnet test --collect "XPlat Code Coverage"
```

## Commit messages

The project follows [Conventional Commits](https://www.conventionalcommits.org/):

```
feat(app): add appointment scheduling
fix(web): keep filters when paging the visit list
docs: describe SQL Server backups
```

Common scopes: `domain`, `app`, `infra`, `db`, `web`, `identity`, `docker`, `ci`.

## Releasing

1. Update `CHANGELOG.md` and the `<Version>` in `Directory.Build.props`.
2. Merge to `main`; CI must be green.
3. Tag the release (`vX.Y.Z`) and publish release notes from the changelog.
