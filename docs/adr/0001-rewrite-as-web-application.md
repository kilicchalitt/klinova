# ADR-0001: Rewrite the legacy desktop prototype as a web application

- **Status:** Accepted
- **Date:** 2026-11-04

## Context

The project started as **SOHATS** (*Sağlık Ocağı Hasta Takip Sistemi*), a
Windows Forms application targeting .NET Framework 4.7.2 and an attached
SQL Server LocalDB file. A review of that prototype showed:

| Area | Finding |
| --- | --- |
| Security | Passwords stored and compared in plain text; a hard-coded `admin / 1234` account recreated on every start. |
| Reliability | `SqlConnection` objects opened in a property getter and never disposed (connection-pool exhaustion); several `SqlDataReader`s left open. |
| Data integrity | No foreign keys in the runtime schema, schema changes executed as ad-hoc `ALTER TABLE` statements at startup, string-built SQL for seed data. |
| Maintainability | Duplicated screens (`HastaProcess.cs` ≈ `SevkIslemleri.cs`, `Form1.cs` ≈ `Login.cs`), UI built by hand in code, business logic mixed into event handlers, no tests. |
| Deployment | Windows-only, single workstation, database file copied next to the executable. Unusable for several desks (reception, doctors, cashier) at the same time. |
| Repository hygiene | Build output, IDE caches and the database file were committed. |

An institution needs several people to work on the same data at the same
time, role-based permissions, an audit trail and a deployment that can be
upgraded centrally.

## Decision

Rebuild the system as **Klinova**, an ASP.NET Core web application, instead of
refactoring the desktop prototype in place.

* **Runtime:** .NET 10 (LTS).
* **UI:** ASP.NET Core Razor Pages with server-side rendering and Bootstrap 5.
  No SPA build chain is required, pages work on any modern browser and on
  tablets used in examination rooms.
* **Architecture:** a layered solution — `Domain` (entities and rules),
  `Application` (use cases), `Infrastructure` (EF Core, Identity, auditing)
  and `Web` (presentation). Business rules are unit tested without a UI.
* **Data access:** Entity Framework Core with versioned migrations.
  SQLite is the zero-configuration default for small clinics; SQL Server is
  supported for institutions that already run it.
* **Identity:** ASP.NET Core Identity — salted password hashing, lockout,
  role-based authorisation, forced password change for seeded accounts.
* **Operations:** Docker image, health checks, structured logging, CI on
  every pull request.

The domain knowledge of the prototype (patients, polyclinics, doctors, price
list, referrals with queue numbers, discharge with billing, revenue reports)
is preserved and extended.

## Consequences

* The desktop prototype is removed from the working tree. It remains
  available in the Git history (last commit before removal: `ebc5eec`).
* Users access the system through a browser; a single server (or container)
  serves every workstation in the institution.
* Contributors need the .NET 10 SDK; no Windows-specific tooling is required.
