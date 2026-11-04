<div align="center">

<img src="src/Klinova.Web/wwwroot/favicon.svg" alt="Klinova logo" width="88" height="88" />

# Klinova

### Outpatient Clinic & Patient Tracking System

**A free, open-source web application that manages a patient's whole journey —<br/>
from registration at the front desk to the receipt at the cashier.**

[![CI](https://github.com/kilicchalitt/klinova/actions/workflows/ci.yml/badge.svg)](https://github.com/kilicchalitt/klinova/actions/workflows/ci.yml)
[![CodeQL](https://github.com/kilicchalitt/klinova/actions/workflows/codeql.yml/badge.svg)](https://github.com/kilicchalitt/klinova/actions/workflows/codeql.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-ready-2496ED?logo=docker&logoColor=white)](#-try-it-in-5-minutes)
[![License: MIT](https://img.shields.io/badge/license-MIT-0f766e.svg)](LICENSE)

[**Try it in 5 minutes**](#-try-it-in-5-minutes) ·
[Features](#features) ·
[Installation](docs/installation.md) ·
[Architecture](docs/architecture.md) ·
[Türkçe](README.md)

</div>

<br/>

<p align="center">
  <img src="docs/images/dashboard.png" alt="Klinova dashboard" width="920" />
</p>

---

## What is Klinova?

Klinova is a patient tracking system for **outpatient clinics, medical centres, family health
centres, private practices and company infirmaries**. Front-desk staff, physicians and the cashier
use the same system **in their browsers, at the same time**. One server — or one Docker command —
is all it takes; nothing is installed on the workstations.

> **Language note:** the user interface is in Turkish and the product targets institutions in
> Türkiye (national ID validation, Turkish alphabetical order, ₺ formatting, KVKK). Code, commit
> history and technical documentation are in English, and multi-language UI is on the roadmap.

## How it works

```mermaid
flowchart LR
    A["🗂️ <b>1. Registration & admission</b><br/>Reception<br/><i>find or register the patient,<br/>assign a queue number</i>"]
    B["🩺 <b>2. Examination</b><br/>Physician<br/><i>call the next patient,<br/>record diagnosis and services</i>"]
    C["💳 <b>3. Billing</b><br/>Cashier<br/><i>discount, payment method,<br/>printed receipt</i>"]
    D["📊 <b>4. Reporting</b><br/>Management<br/><i>daily cash, monthly revenue,<br/>per polyclinic and physician</i>"]
    A --> B --> C --> D
```

## Screenshots

<table>
  <tr>
    <td width="50%"><img src="docs/images/examination.png" alt="Examination" /><br/><b>Examination</b>: medical alerts, complaint, diagnosis, notes and services on one page.</td>
    <td width="50%"><img src="docs/images/worklist.png" alt="Work list" /><br/><b>Physician work list</b>: today's queue and one-click "call next patient".</td>
  </tr>
  <tr>
    <td><img src="docs/images/reports.png" alt="Reports" /><br/><b>Reports</b>: revenue and activity for any date range, CSV export.</td>
    <td><img src="docs/images/receipt.png" alt="Receipt" /><br/><b>Receipt</b>: numbered A4 receipt with institution details.</td>
  </tr>
</table>

<sub>All people and institutions shown are fictional.</sub>

## Features

- **Patients:** national ID checksum validation and duplicate prevention, automatic file numbers,
  insurance and emergency contact, allergy and chronic condition alerts on every screen,
  accent-insensitive search by name, file number, national ID or phone.
- **Admission:** polyclinic and physician selection, conflict-free daily queue number per
  polyclinic, printable queue slip.
- **Physicians:** today's list, one-click "call next", complaint, diagnosis, notes, services,
  previous visits.
- **Cashier:** outstanding visits, discounts and payment methods, numbered receipts,
  end-of-day totals per payment method.
- **Reports:** gross/net revenue, discounts, visit counts by polyclinic, physician, service and
  payment method; Excel-friendly CSV.
- **Administration:** polyclinics, physicians, price list (changes never alter past receipts),
  users and roles, institution profile, full audit log.

| | Reception | Physician | Cashier | Admin |
| --- | :---: | :---: | :---: | :---: |
| Register and admit patients | ✅ | — | — | ✅ |
| View patients and visits | ✅ | ✅ | ✅ | ✅ |
| Record examinations | — | ✅ | — | ✅ |
| Collect payments | ✅ | — | ✅ | ✅ |
| Revenue reports | — | — | ✅ | ✅ |
| Users, prices, settings, audit log | — | — | — | ✅ |

## 🚀 Try it in 5 minutes

Requires [Docker](https://www.docker.com/products/docker-desktop/).

```bash
git clone https://github.com/kilicchalitt/klinova.git
cd klinova
cp .env.example .env
```

Edit `.env`:

```ini
KLINOVA_ADMIN_PASSWORD=Try-Admin-2026
KLINOVA_DEMO_DATA=true
KLINOVA_DEMO_PASSWORD=Try-Demo-2026
```

```bash
docker compose up -d
```

Open <http://localhost:8080> and sign in as `admin` (Admin), `kabul` (Reception),
`hekim` (Physician) or `vezne` (Cashier).

For production, keep demo data off, serve over HTTPS and follow the
[installation guide](docs/installation.md) (Docker, Linux/systemd, Windows/IIS, SQL Server, backups).

<details>
<summary><b>Run without Docker</b></summary>

<br/>

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/Klinova.Web --launch-profile http
```

Demo data is created in the Development environment; generated passwords are printed once to the console.
</details>

## Security & privacy

- No default credentials: the initial admin password is configured or randomly generated and must be changed at first sign-in.
- Hashed passwords, account lockout, rate-limited sign-in, role-based authorisation with a deny-by-default policy.
- CSRF protection, `HttpOnly` + `SameSite=Strict` cookies, HSTS and a strict Content-Security-Policy.
- Full audit trail; cashiers never see diagnoses or examination notes.
- Data stays on the institution's own server.

Please report vulnerabilities privately, see [SECURITY.md](SECURITY.md).

## Tech stack

ASP.NET Core 10 (Razor Pages) · Entity Framework Core 10 (SQLite or SQL Server) ·
ASP.NET Core Identity · Bootstrap 5 · xUnit (116 tests) · Docker · GitHub Actions, CodeQL, Dependabot.
See [architecture](docs/architecture.md) and [development guide](docs/development.md).

## Contributing

Bug reports, ideas, documentation and code are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md)
and the [Code of Conduct](CODE_OF_CONDUCT.md). Never share real patient data in issues.

## About

**Name:** *Klinova* = **Klin**ic + **nova** (Latin for "new").

**History:** Klinova started as **SOHATS**, a Windows Forms student project, and was rewritten
with ASP.NET Core so that a real institution can use it with many users, securely.
See [ADR-0001](docs/adr/0001-rewrite-as-web-application.md) and the [changelog](CHANGELOG.md).

## License

[MIT](LICENSE) © Klinova contributors. Klinova is not a medical device and does not provide
clinical decision support.
