# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-11-04

First release of **Klinova**, a rewrite of the SOHATS desktop prototype as a web
application. See [ADR-0001](docs/adr/0001-rewrite-as-web-application.md).

### Added

- Patient registry with T.C. Kimlik No validation, duplicate detection, insurance,
  emergency contact and medical alerts; accent-insensitive search; archiving.
- Polyclinic admission with conflict-free daily queue numbers and printable queue slips.
- Physician work list with "call next patient", examination notes, diagnosis and services.
- Cashier desk with discounts, payment methods, numbered receipts and daily totals.
- Revenue and activity reports by polyclinic, physician, service and payment method, with CSV export.
- Administration of polyclinics, physicians, price list, users and roles, institution profile.
- Automatic audit trail of all data changes with a viewer for administrators.
- ASP.NET Core Identity with four roles, lockout, rate-limited sign-in and forced password change.
- SQLite (default) and SQL Server support with EF Core migrations.
- Docker image, docker compose files, GitHub Actions CI, CodeQL and Dependabot.
- Documentation in Turkish and English.

### Changed

- The product was renamed from SOHATS to Klinova.

### Removed

- The Windows Forms prototype, its LocalDB database file and committed build output.
  The prototype source remains available in the Git history (commit `ebc5eec`).

### Security

- Plain-text passwords and the hard-coded `admin/1234` account of the prototype were
  replaced by hashed passwords, a generated or configured initial password and a
  mandatory password change at first sign-in.

[Unreleased]: https://github.com/kilicchalitt/klinova/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/kilicchalitt/klinova/releases/tag/v1.0.0
