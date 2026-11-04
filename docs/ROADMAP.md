# Klinova Roadmap

This roadmap describes how the project evolves from the original **SOHATS**
desktop prototype into **Klinova**, an open-source clinic and outpatient
management system that an institution can deploy and use every day.

Status legend: ✅ done · 🚧 in progress · 🗓️ planned

## Phase 0 — Repository hygiene ✅

- ✅ Remove build output, IDE state and database files from version control.
- ✅ Comprehensive `.gitignore` that blocks secrets, keys and local data.
- ✅ Architecture decision record for the rewrite ([ADR-0001](adr/0001-rewrite-as-web-application.md)).

## Phase 1 — Foundation (v1.0) ✅

- ✅ Layered .NET 10 solution (`Domain`, `Application`, `Infrastructure`, `Web`).
- ✅ Central build configuration, analyzers, `.editorconfig`, nullable reference types.
- ✅ EF Core data model with migrations for SQLite and SQL Server.
- ✅ ASP.NET Core Identity with roles: *Admin*, *Reception*, *Physician*, *Cashier*.
- ✅ Forced password change for seeded accounts, account lockout.
- ✅ Automatic audit trail of every create / update / delete.

## Phase 2 — Core clinical workflow (v1.0) ✅

- ✅ Patient registry with Turkish national ID (T.C. Kimlik No) checksum validation,
  duplicate detection, search and paging.
- ✅ Polyclinic, physician and medical-service (price list) management.
- ✅ Patient admission to a polyclinic with a daily queue number per polyclinic.
- ✅ Physician work list: call the next patient, record complaint, diagnosis and notes,
  add procedures, complete the examination.
- ✅ Billing: collect payment with discount and payment method, printable receipt.
- ✅ Dashboard with today's figures; revenue reports by polyclinic, physician and
  service with CSV export.
- ✅ Institution settings (name, address, receipt footer) editable by administrators.

## Phase 3 — Operations & quality (v1.0) ✅

- ✅ Unit and integration test suites, GitHub Actions CI.
- ✅ Docker image and `docker compose` setup with persistent volumes.
- ✅ Health check endpoint, security headers, HTTPS/HSTS, anti-forgery protection.
- ✅ Contributor documentation, security policy, issue and pull-request templates.

## Phase 4 — Next releases 🗓️

- 🗓️ Appointment scheduling with physician calendars.
- 🗓️ ICD-10 diagnosis catalogue with search.
- 🗓️ Prescription and laboratory request forms.
- 🗓️ PostgreSQL provider.
- 🗓️ Multi-language UI (Turkish / English resource files).
- 🗓️ Queue display screen for waiting rooms.
- 🗓️ PDF export for receipts and reports.
- 🗓️ Data export / anonymisation tooling to support KVKK and GDPR requests.
- 🗓️ Optional integration points (e-Nabız, MEDULA) behind clearly separated adapters.

Have an idea? Open a [feature request](../.github/ISSUE_TEMPLATE/feature_request.yml).
