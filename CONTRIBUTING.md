# Contributing to Klinova

Thank you for helping to improve Klinova! This document explains how to propose
changes. By participating you agree to abide by the [Code of Conduct](CODE_OF_CONDUCT.md).

> Türkçe katkılar da memnuniyetle karşılanır. Issue ve pull request açıklamalarını
> Türkçe veya İngilizce yazabilirsiniz.

## Ways to contribute

- **Report bugs** using the [bug report template](https://github.com/kilicchalitt/klinova/issues/new?template=bug_report.yml).
  Never include real patient data in issues, screenshots or logs.
- **Suggest features** using the [feature request template](https://github.com/kilicchalitt/klinova/issues/new?template=feature_request.yml).
  Check the [roadmap](docs/ROADMAP.md) first.
- **Improve documentation**, translations and tests.
- **Report security issues privately** — see [SECURITY.md](SECURITY.md).

## Development workflow

1. Fork the repository and create a branch from `main`
   (`feat/appointment-calendar`, `fix/queue-number-reset`, …).
2. Set up your environment as described in [docs/development.md](docs/development.md).
3. Make focused changes with tests:
   - business rules → `tests/Klinova.Domain.Tests`
   - use cases → `tests/Klinova.Application.Tests`
   - pages, security and authorisation → `tests/Klinova.Web.Tests`
4. If you change the data model, add migrations for **both** SQLite and SQL Server.
5. Run the same checks as CI:

   ```bash
   dotnet format Klinova.slnx --verify-no-changes
   dotnet build Klinova.slnx -c Release
   dotnet test Klinova.slnx -c Release
   ```

6. Write commit messages following [Conventional Commits](https://www.conventionalcommits.org/)
   (`feat(web): …`, `fix(app): …`, `docs: …`).
7. Open a pull request and fill in the template. Keep pull requests small and focused;
   one topic per pull request.

## Pull request checklist

- [ ] Tests added or updated, `dotnet test` passes.
- [ ] No warnings; `dotnet format --verify-no-changes` passes.
- [ ] Migrations added for both providers if the model changed.
- [ ] User-facing text is in Turkish; code and comments are in English.
- [ ] No secrets, personal data, database files or build output committed.
- [ ] Documentation and `CHANGELOG.md` (*Unreleased* section) updated when relevant.

## Licensing

By contributing you agree that your contributions are licensed under the
[MIT License](LICENSE).
