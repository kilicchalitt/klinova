# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Patient portal inbox ("Bildirimlerim"): appointment confirmations, reminders, cancellations and
  waiting-list messages are shown to patients in the portal with an unread counter. No SMS or e-mail
  provider is required.

### Changed

- SMS and e-mail messages are only queued when the corresponding provider is configured.

## [2.0.0] - 2026-11-04

Online appointments, patient portal and multi-facility support.
See [ADR-0002](docs/adr/0002-appointments-and-patient-portal.md).

### Added

- **Facilities:** sites with province and district; polyclinics belong to a facility and can be
  opened or closed for online booking.
- **Work schedules:** weekly blocks per physician with slot length, validity and online / desk-only
  flag; leave; full- and half-day holidays with Türkiye's fixed national holidays pre-filled.
- **Appointments** with MHRS-style rules: booking horizon and lead time, one active appointment per
  polyclinic, cancellation deadline, 15-day no-show restriction with escalation to all polyclinics,
  rescheduling, note to the physician, confirmation codes, check-in into a visit, no-show marking.
- **Waiting list:** patients are notified when a matching appointment is cancelled.
- **Patient portal:** registration with national id (existing files linked only when the phone on
  file matches), province → facility → polyclinic → physician → slot booking wizard, earliest free
  slot, my appointments, visit history, profile, KVKK privacy notice.
- **Notifications:** outbox with SMTP e-mail and HTTP webhook SMS senders, retries and an
  administration page; confirmation, reminder, cancellation and waiting-list messages.
- **Background jobs** for notification delivery and appointment maintenance.
- Staff appointment calendar, desk booking, waiting-list overview, appointment figures on the dashboard.

### Changed

- Staff pages require a staff role; patients and staff are kept in separate areas.
- Polyclinic names are unique per facility; drop-downs, dashboard and revenue report show the
  facility when several exist.
- Test tooling and GitHub Actions updated to their latest versions.

### Upgrade notes

- The migration attaches existing polyclinics to a default facility ("Merkez Poliklinik"); rename it
  under *Kurumlar ve Şubeler*.
- Define work schedules for physicians before announcing online booking.

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

[Unreleased]: https://github.com/kilicchalitt/klinova/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/kilicchalitt/klinova/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/kilicchalitt/klinova/releases/tag/v1.0.0
