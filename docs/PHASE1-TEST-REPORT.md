# Phase 1 – testing report

**Build:** .NET 10 (SDK 10.0.4xx), EF Core 10, PostgreSQL 16 · **Date:** 13 Nov 2026
**How tested:** every push runs the GitHub Actions pipeline:

1. **22 automated integration tests** – the real application (`WebApplicationFactory`) against a real PostgreSQL 16 server; each test gets a fresh database. Several "computers" are simulated with separate browser sessions (own cookies) against the same server and database.
2. **Browser walk-through** – the published application is started as a normal server process and driven by Chromium (Playwright): setup wizard, login, users, responsibilities, permissions, settings, printing, licence, backup, audit, a second computer (receptionist), tablet layout. 33 screenshots, **0 JavaScript errors**.
3. **Windows installer check** – the self-contained Windows package is installed with `Install-Server.ps1` on a clean Windows Server 2022 machine with PostgreSQL: database + login created, Windows service registered and started, firewall rule created, `/health` OK, setup wizard served, service restart OK.

Result of the final run: **22 / 22 tests passed**, walk-through completed without errors.

## Completion checklist

| # | Requirement | Result | Evidence |
|---|-------------|--------|----------|
| 1 | Application starts successfully | ✅ | All tests + walk-through + Windows service start |
| 2 | PostgreSQL connection works | ✅ | Migrations applied at start-up; `/health` = Healthy; dashboard "Database: Connected" |
| 3 | Initial Setup Wizard works | ✅ | `Fresh_install_redirects_everything_to_setup_wizard`, `Setup_validates_input_and_saves_everything_centrally`, screenshots 01–06 |
| 4 | Hospital information saves correctly | ✅ | Same test checks every field in `HospitalSettings` / `HospitalConfiguration` |
| 5 | Hospital logo uploads and displays | ✅ | Logo stored in DB and served byte-identical from `/branding/logo` (`Logo_is_served…`); shown on login, sidebar, dashboard, print preview |
| 6 | Government/Private selection saves | ✅ | Government → pricing off, government stock on; `Hospital_type_change_requires_password_and_confirmation` |
| 7 | First admin created | ✅ | Single system administrator, password stored as PBKDF2 hash only |
| 8 | Admin login and logout | ✅ | `Login_logout_history_and_audit` (history, workstation name, audit, cookie cleared) |
| 9 | Admin can create users | ✅ | `CreateUserAsync` in many tests; duplicates rejected (case-insensitive) |
| 10 | Activate / deactivate users | ✅ | `Deactivation_ends_sessions_on_all_computers` – deactivated user is logged out on the other computer immediately |
| 11 | Assign departments | ✅ | `Departments_assignment_and_password_reset_flow` (multiple departments) |
| 12 | Granular permissions | ✅ | Grant/Deny/Inherit per action; responsibilities; deny overrides (`Responsibilities_grant_permissions_and_deny_overrides`) |
| 13 | Restricted menus hidden | ✅ | Receptionist dashboard has no Users/Backup/Settings/Audit links, no unassigned modules, no hospital-wide figures (screenshot 29) |
| 14 | Direct URL/API access blocked | ✅ | 9 pages → Access denied, 3 APIs → 403, forged POST rejected and audited |
| 15 | Audit log records activities | ✅ | Setup, logins, failures, locks, logout, timeouts, user/permission/department/role changes, settings with before/after, licence, backups, denied access; DB triggers block UPDATE/DELETE/TRUNCATE |
| 16 | 30-day trial starts | ✅ | Trial row created at setup, 30 days, "30 day(s) remaining" |
| 17 | Licence status displays correctly | ✅ | Trial / expired / annual / lifetime / tampered states tested; foreign-installation, forged and expired keys rejected |
| 18 | Manual backup works | ✅ | pg_dump backup, size + SHA-256 recorded, file present, download works |
| 19 | Restore workflow protected | ✅ | Permission, password, typed `RESTORE`, checksum, automatic safety backup, single-transaction restore; ordinary user blocked |
| 20 | Two or more computers via LAN | ✅* | Server binds `0.0.0.0:<port>`; multiple independent sessions tested concurrently; Windows firewall rule verified. *Physical multi-PC test must be done on site (see below). |
| 21 | Changes on one computer appear on another | ✅ | `Concurrent_edits_from_two_computers_are_detected`: PC2 immediately sees PC1's change; simultaneous edit detected |
| 22 | Works on LAN without internet | ✅ | No CDN/external fonts/scripts; CSP `default-src 'self'`; offline licence activation |
| 23 | Data safe after restart | ✅ | `Data_survives_application_restart` (new app instance, same DB: users and licence intact); Windows service restart |

Additional checks: idle session timeout and that background dashboard refresh does not keep idle sessions alive; account lockout and auto-unlock; forced password change after reset; self-permission change and privilege escalation blocked; primary administrator protected; setup cannot be re-run except by the primary administrator (data kept); licence expiry ⇒ read-only, data untouched, backup still allowed; security headers present.

## On-site acceptance test (to do at the hospital)
1. Install on the server, note its IP.
2. From 2–3 different computers open `http://<ip>:5000`, log in as different users at the same time.
3. Create a user on PC 1 → refresh the user list on PC 2 → it appears.
4. Unplug the internet (WAN) cable from the router → everything keeps working.
5. Print the sample report and receipt on the real printers (Settings ▸ Printing ▸ Preview).
6. Create a backup, copy it to USB, restore it once on a test installation.

## Known limitations of Phase 1
- Clinical modules (OPD, Emergency, Laboratory, Pharmacy, X-Ray, Ultrasound, doctors, finance, reports) are not built yet – only their permissions and configuration exist.
- The interface is in English; the *Urdu* language preference is stored for a later phase.
- Scheduled automatic backups are prepared but not yet active (manual backups only).
- Printing uses the browser's print dialog (the saved printer name is a reminder; browsers cannot select a printer silently).
- HTTPS is optional and requires a certificate configured by the hospital (see INSTALLATION.md).
- Online activation / remote access / cloud backup are planned for later phases.
