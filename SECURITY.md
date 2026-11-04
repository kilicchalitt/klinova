# Security Policy

Klinova processes health data, which is a special category of personal data under
KVKK (Law No. 6698) and the GDPR. We take security reports seriously.

## Supported versions

| Version | Supported |
| --- | --- |
| 1.x | ✅ |
| SOHATS desktop prototype | ❌ (retired) |

## Reporting a vulnerability

**Please do not open a public issue for security problems.**

Use GitHub's private vulnerability reporting: open the repository's **Security** tab and
choose **Report a vulnerability**. Include:

- the affected version or commit,
- steps to reproduce or a proof of concept,
- the impact you expect (data exposure, privilege escalation, …).

You can expect an acknowledgement within 5 working days and a status update at least
every 14 days until the issue is resolved. We will credit reporters in the release
notes unless they prefer to remain anonymous.

## Scope

In scope: the code in this repository, the Docker image built from it and the default
configuration. Out of scope: vulnerabilities that require an already compromised
server, missing hardening of a specific installation (TLS, firewall, backups), and
denial of service through volumetric traffic.

## Security features

- Passwords hashed by ASP.NET Core Identity (PBKDF2), account lockout after 5 failures,
  per-IP rate limiting of the sign-in page.
- No default credentials: the initial administrator password is configured or randomly
  generated, and must be changed at first sign-in.
- Role-based authorisation with an authenticated-by-default fallback policy.
- Anti-forgery tokens on every form, `HttpOnly` + `SameSite=Strict` cookies, HSTS.
- Strict Content-Security-Policy without inline scripts or third-party origins;
  `X-Frame-Options: DENY`, `nosniff`, restrictive referrer and permissions policies.
- Audit trail of all data changes; sensitive Identity fields are masked.
- CSV exports are protected against formula injection.
- Dependencies are monitored by Dependabot and the code by CodeQL.

## Operator responsibilities

Institutions deploying Klinova are data controllers. Before going live, review the
[security checklist](docs/installation.md#güvenlik-kontrol-listesi): HTTPS only, changed
initial password, demo data disabled, restricted file permissions, encrypted and tested
backups, individual accounts for every staff member, and the KVKK documentation
(privacy notice, VERBİS registration, retention and destruction policy).
