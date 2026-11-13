# Burhan Nexus Hospital Management

A modern, secure, web-based Hospital Management System by **Burhan Nexus**.
One central server and database; every computer in the hospital (reception, laboratory, pharmacy,
X-Ray, ultrasound, administration…) opens it in a web browser over the LAN – no internet needed.

> **Status: Phase 1 complete** – core system, architecture, setup wizard, login, users, departments,
> responsibilities, granular permissions, dashboard, branding & printing, licensing, audit log,
> backup & restore. Clinical modules (OPD, Emergency, Laboratory, Pharmacy, X-Ray, Ultrasound,
> billing…) follow in later phases on top of this foundation.

## Highlights

| Area | Phase 1 |
|------|---------|
| LAN | One server (e.g. `http://192.168.1.100:5000`), unlimited client computers, configurable address/port, works offline |
| First run | 5-step Setup Wizard: hospital info + logo, Government/Private type, first administrator, preferences, printers |
| Login | Show/hide password, remember username, PBKDF2 hashing, lockout after failed attempts, per-computer rate limit, idle timeout, login history |
| Users | Individual accounts, multiple departments, multiple responsibilities, activate/deactivate, password reset, unlock |
| Permissions | 17 modules × up to 12 actions; responsibilities + per-user Grant/Deny overrides; hidden in UI **and** enforced on the server/API |
| Branding | Hospital name/logo/headers/footers stored centrally; printable report & receipt preview (A4/A5/Letter/Legal/80 mm/58 mm) |
| Licensing | 30-day trial on install, offline Annual/Lifetime activation keys (ECDSA-signed), expiry ⇒ read-only, **never** deletes data |
| Audit | Every important action with user, computer, IP, before/after values; tamper-proof at database level; CSV export |
| Backup | Manual backup (pg_dump), history, checksum, download, protected restore with automatic safety backup |

## Technology

| Layer | Choice | Why |
|-------|--------|-----|
| Backend | ASP.NET Core **.NET 10 LTS** | .NET 8 support ends Nov 2026; .NET 10 is supported until Nov 2028 |
| UI | Razor Pages + own CSS/JS (no CDN, no npm) | Works fully offline on the LAN, fast on old PCs, same C# skill set as other Burhan Nexus apps |
| Database | PostgreSQL 16+ via EF Core 10 (Npgsql) | One central database, transactions, optimistic concurrency (xmin) |
| Hosting | Self-contained Windows Service (Kestrel) | No IIS or .NET install needed on the server |

## Repository layout

```
src/
  BurhanNexus.HMS.Core/            Domain entities, permission catalogue, licensing contracts (no dependencies)
  BurhanNexus.HMS.Infrastructure/  EF Core DbContext + migrations, services (auth, users, settings, licence, backup, audit)
  BurhanNexus.HMS.Web/             Razor Pages UI, API controllers, middleware, security
tests/BurhanNexus.HMS.Tests/       Integration tests against a real PostgreSQL database
tools/BurhanNexus.LicenseGenerator Vendor-only activation key generator (never shipped to hospitals)
tools/ui-check/                    Automated browser walk-through that produces screenshots
deploy/windows/                    Install / update / uninstall scripts for the server
docs/                              Installation, user guide, backup, troubleshooting, test report…
```

## Documentation

- [Server installation & LAN setup](docs/INSTALLATION.md)
- [User & administrator guide](docs/USER-GUIDE.md) – first admin, users, departments, permissions, settings
- [Backup & restore](docs/BACKUP-RESTORE.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Licensing (Burhan Nexus staff)](docs/LICENSING.md)
- [Architecture & database](docs/ARCHITECTURE.md)
- [Development](docs/DEVELOPMENT.md)
- [Phase 1 testing report](docs/PHASE1-TEST-REPORT.md)

## Quick start (developer)

```bash
# PostgreSQL running locally with user postgres / postgres
dotnet run --project src/BurhanNexus.HMS.Web      # http://localhost:5000 → Setup Wizard
dotnet test                                       # needs HMS_TEST_PG connection string (see docs/DEVELOPMENT.md)
```

© Burhan Nexus. All rights reserved.
