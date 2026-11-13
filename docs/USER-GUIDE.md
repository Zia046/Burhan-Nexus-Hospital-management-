# User & administrator guide (Phase 1)

## 1. First start – Initial Setup Wizard

The first time the server address is opened, the wizard appears instead of the login page. It cannot be skipped and appears only once.

| Step | What to enter |
|------|---------------|
| 1 Hospital | Name, registration no. (optional), address, city, province, phone, email, website (optional), logo (PNG/JPEG/GIF/WebP, max 2 MB) |
| 2 Type | **Government** (medicines from government stock, no prices) or **Private** (purchase/sale prices, retail sales, profit) |
| 3 Administrator | Full name, username, password (8+ characters, letters and numbers, must not contain the username), phone |
| 4 Preferences | Currency (PKR), time zone (Asia/Karachi), date & time format, language, receipt and report paper/printer |
| 5 Review | Check and press **Complete setup** |

A **30-day trial** starts automatically. The administrator created here is the **primary system administrator**: always has every permission and cannot be deactivated, so the hospital can never lock itself out.

To run the wizard again later (only the primary administrator): *Hospital Settings ▸ Re-run setup* (password + type `RESET`). Users and data are kept.

## 2. Logging in

- Username is not case-sensitive. Use the eye icon to show/hide the password.
- *Remember username* stores only the username on that computer (never the password).
- *This computer* – name the workstation once (e.g. `Lab-1`); it appears in the audit log.
- After **5 wrong passwords** the account is locked for **15 minutes** (configurable). An administrator can unlock it at once.
- After **30 minutes without activity** the session ends automatically; a warning appears one minute before.
- *Log out* from the user menu (top right). Deactivating a user or resetting their password logs them out on all computers immediately.

## 3. Users (Administration ▸ Users)

**Create a user:** *New user* ▸ name, designation, phone, email, username, password, departments ▸ *Create user*.
Tick *User must change this password at first login* (recommended) so only the employee knows the password.

On the user page you can:
- edit employee information,
- assign **departments** (several allowed),
- assign **responsibilities** (several allowed),
- **deactivate / activate** the account,
- **reset password** and **unlock** a locked account,
- open **Permissions** and **Login history**.

Every employee should have their own account – never share accounts; the audit log records who did what.

## 4. Departments (Administration ▸ Departments)

Ten common departments are created by setup (Administration, Reception, OPD, Emergency, Laboratory, Pharmacy, X-Ray, Ultrasound, Minor OT, Accounts). Add, rename or deactivate departments to match the hospital. Departments for future modules can be added at any time.

## 5. Responsibilities (Administration ▸ Responsibilities)

A responsibility is a reusable set of permissions – e.g. *Receptionist*, *Lab Technician*, *Pharmacist*, *Accountant*. Create it once, tick its permissions, then assign it to any number of users. Nothing is predefined; build them around your own staffing structure.

## 6. Permissions (Users ▸ Permissions)

Each module has actions such as **View, Add, Edit, Delete, Print, Export PDF, Verify, Approve, Cancel, Refund, Financial Access, Settings Access** (administrative modules have their own specific actions).

For each permission choose:
- **Inherit** – allowed only if one of the user's responsibilities includes it,
- **Grant** – allowed for this user,
- **Deny** – never allowed for this user, even if a responsibility grants it.

Rules enforced by the system:
- Anything a user is not allowed to use is **removed from their menus, dashboard and buttons**, and is also **blocked on the server** (direct URLs and API calls return *Access denied*, and the attempt is audited).
- Nobody can change **their own** permissions or responsibilities.
- An administrator who is not the primary administrator can only give permissions **they hold themselves**, and cannot change the primary administrator.
- All changes are recorded in the audit log with before/after values and apply immediately on every computer.

Phase 1 modules: Dashboard, User Management, Hospital Settings, System Backup, License Management, Audit Log.
Future modules (Patients, OPD, Emergency, Laboratory, X-Ray, Ultrasound, Pharmacy, Minor OT, Doctors, Finance, Reports) can already be assigned; they appear on the dashboard as *Coming in a future phase* and open nothing yet.

## 7. Dashboard

Administrators see users (total/active/inactive), licence, database and server status, active sessions, configuration summary, LAN addresses for other computers, recent activity and quick links. Figures refresh every 30 seconds. Normal users see only their own assigned modules – no hospital-wide or financial figures.

## 8. Hospital Settings

| Tab | Contents |
|-----|----------|
| Hospital & branding | Name, address, contacts, logo, receipt/report headers and footers, print preview |
| Preferences | Currency + symbol, time zone, date/time format, language |
| Printing | Receipt paper (80 mm/58 mm/A5/A4), copies, report paper (A4/Letter/Legal/A5), orientation, printer names |
| Hospital type & rules | Operational rules (government stock, pharmacy pricing, retail sales, profit, patient charges); changing Government ⇄ Private requires your password and typing `CHANGE` |
| Security | Idle timeout, failed attempts before lock, lock duration |
| License | Status, days remaining, Installation ID, activation |

All receipts, slips and reports of later modules automatically use the saved name, logo, headers and footers.

## 9. License

- Trial: 30 days from installation, all features available.
- To buy: send the **Installation ID** (License page) to Burhan Nexus; you receive an **Annual** or **Lifetime** activation key. Paste it and press *Activate* – no internet needed.
- When a trial/annual licence ends, the system becomes **read-only**: everyone can still log in, view records, create and download backups and activate a key. **No data is ever deleted or changed because of licensing.**

## 10. Audit log & login history

*Audit Log* lists every important action (logins, failed logins, account locks, user changes, permission changes, settings, licence, backups, access-denied attempts) with user, date/time, computer, IP address and before/after values. Filter by text, activity, result and date; export to CSV (Excel). Audit records cannot be edited or deleted by anyone – the database itself refuses.

*Login History* shows every login attempt, the computer used and how each session ended (logout, timeout, deactivated, password changed).
