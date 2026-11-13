# Architecture & database

## Layers
```
Browser (any LAN computer) ──HTTP/HTTPS──▶ BurhanNexus.HMS.Web  (Razor Pages, API, middleware)
                                               │ uses interfaces
                                               ▼
                                   BurhanNexus.HMS.Infrastructure (services, EF Core, licence, backup)
                                               │
                                               ▼
                                   BurhanNexus.HMS.Core (entities, permission catalogue, contracts)
                                               │
                                   PostgreSQL – one central database
```
- **Core** has no dependencies. New modules add their entities and permission catalogue entries here.
- **Infrastructure** implements all business rules (validation, permission rules, auditing, concurrency) so the same rules apply to pages and APIs.
- **Web** is thin: authentication, authorization, UI.

## Request pipeline
`SecurityHeaders → StaticFiles → Routing → RateLimiter → Authentication (cookie validated against DB on every request) → SetupMiddleware (forces wizard until configured) → Authorization (fallback = authenticated; [HasPermission] policies) → RequestPolicy (forced password change, licence read-only) → Razor Pages / API`

## Security model
- Passwords: PBKDF2-HMAC-SHA512 (ASP.NET Core Identity v3 hasher), random salt, automatic re-hash.
- Sessions: HttpOnly, SameSite=Strict cookie containing user id, session id and security stamp; every request checks the session row in `LoginHistory`, account status and idle timeout in the central database. Logout/deactivation/password reset end sessions on all computers immediately.
- CSRF: antiforgery tokens on every form; SameSite=Strict cookies.
- Authorization: permissions are loaded per request from the database (responsibilities ∪ grants − denials). Pages/APIs declare `[HasPermission("Module.Action")]`; individual handlers re-check (e.g. *Edit*, *Restore*). The `bn-permission` tag helper removes UI elements the user may not use.
- Self-modification and privilege-escalation rules are enforced in `UserService`/`RoleService`.
- CSP `script-src 'self'`, X-Frame-Options DENY, nosniff, no-store for signed-in pages.
- Login brute-force: per-account lockout + per-IP rate limit.
- Audit log: append-only, enforced by PostgreSQL triggers (UPDATE/DELETE/TRUNCATE rejected).

## Multi-computer behaviour
- All state is in PostgreSQL; nothing important is kept in browser storage.
- Optimistic concurrency with PostgreSQL `xmin` on users, departments, responsibilities and settings: if two computers edit the same record, the second save is rejected with a clear message.
- Unique constraints (usernames, department names, responsibility names, singleton settings rows) prevent duplicates even under simultaneous requests; setup completion runs in a serializable transaction.
- Backup/restore are serialized server-wide.

## Database (PostgreSQL, EF Core migrations)

| Table | Purpose / key columns |
|-------|-----------------------|
| `HospitalSettings` | Single row (Id = 1, check constraint). Name, registration no., address, contacts, logo (bytea + content type), receipt/report header & footer, timestamps |
| `HospitalConfiguration` | Single row. Hospital type, setup completed (+ when/by), currency, time zone, date/time format, language, printer settings, operational rules (pharmacy pricing, retail, profit, government stock, patient charges) |
| `Users` | Full name, username + unique normalized username, designation, contacts, active, system-administrator flag, created/updated/last-login |
| `UserCredentials` | 1:1 with user. Password hash, password changed, must-change flag, failed count, lockout end, security stamp |
| `Departments` | Unique name, unique optional code, description, active |
| `UserDepartments` | User ↔ department (many-to-many) with assigned by/at |
| `Permissions` | Catalogue of `Module.Action` keys (unique), synchronised at start-up |
| `Roles` | Responsibilities (unique name, description, active) |
| `RolePermissions` | Responsibility ↔ permission |
| `UserRoles` | User ↔ responsibility with assigned by/at |
| `UserPermissions` | Per-user override: IsGranted true = grant, false = deny |
| `LicenseInformation` | Single row. Installation ID, type, licensee, trial start, activation, expiry, key fingerprint, sealed state, highest seen time |
| `LoginHistory` | Every attempt: user, username, time, success, reason, IP, workstation, user agent, session id (unique), last activity, logout time/reason |
| `AuditLogs` | Time, user, action, entity type/id, description, old/new values (jsonb), IP, workstation, user agent, success. Immutable |
| `SystemSettings` | Key/value settings (session timeout, lockout policy…) |
| `BackupHistory` | File name (unique), size, SHA-256, status, kind, start/end, created by, error, notes, last restored |

All timestamps are stored in UTC (`timestamp with time zone`) and displayed in the hospital's time zone. Indexes exist on all foreign keys, audit/login timestamps, users' active flag and audit actions.

Migrations live in `src/BurhanNexus.HMS.Infrastructure/Persistence/Migrations` and are applied automatically at start-up (`Hms:AutoMigrate`). Migrations only add or alter – existing data is preserved.

## Extending in later phases
1. Add entities in Core and a migration in Infrastructure.
2. Mark the module `IsAvailable = true` in `PermissionCatalog` with its URL (its permissions already exist, so existing grants keep working).
3. Add pages with `[HasPermission(...)]` and use `HospitalConfiguration` rules (e.g. `PharmacyPricingEnabled`) instead of checking the hospital type.
4. Use `HospitalProfile` + `print.css` for all printed documents so branding is automatic.
