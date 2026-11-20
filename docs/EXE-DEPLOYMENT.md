# Burhan Nexus HMS – Windows EXE deployment (Server Setup / Client Setup)

Version 2.2.0 (Update 13). These test builds are unsigned, so Windows shows "Windows protected your PC" until code signing is added.

## Architecture

One server computer + one PostgreSQL database + many client computers on the hospital LAN.

- **Server computer**
  - Runs two Windows services: `postgresql-x64-16` (the database, listening on localhost only) and `BurhanNexusHMS` (the HMS web application on port 5000).
  - Both services start automatically with Windows.
  - The HMS service restarts itself after a failure (after 5 s, 10 s and 30 s).
- **Client computers**
  - Run *Burhan Nexus HMS* (`BurhanNexus.HMS.Client.exe`), a WebView2 window that shows the server's existing web pages.
  - They have no database, no service and no hospital data.
  - Any browser still works too, at `http://SERVER-IP:5000`.

| Package | Install on | Contents |
|---|---|---|
| `BurhanNexus-HMS-Server-Setup-2.2.0.exe` (~191 MB) | the one server | HMS server (self-contained .NET 10), PostgreSQL 16 (engine only), VC++ runtime, client window, server tools |
| `BurhanNexus-HMS-Client-Setup-2.2.0.exe` (~49 MB) | every other PC | client window; Microsoft's WebView2 bootstrapper (installed only if missing) |

## Server Setup

The wizard asks for:

- the program folder (default `C:\Program Files\Burhan Nexus HMS\Server`)
- the **hospital data folder** (default `C:\ProgramData\BurhanNexusHMS`)
- the PostgreSQL superuser password (default `BCGH`)
- the port (default 5000)

It then runs the existing install scripts (`Easy-Install.ps1` and `Install-Server.ps1`) with the following steps:

1. Install the VC++ runtime.
2. Unpack PostgreSQL and initialise the database files with `initdb` in `<data>\pgdata`.
3. Register the PostgreSQL service.
4. Create the database `burhan_nexus_hms` and the login `bnhms_app`.
5. Write `appsettings.Production.json`. It holds absolute paths to `<data>\data`, `<data>\backups` and `<data>\radiology-files`, and is readable only by Administrators and SYSTEM.
6. Run `--migrate-only`.
7. Register and start the `BurhanNexusHMS` service.
8. Add a firewall rule for TCP 5000, limited to the Private and Domain profiles.
9. Set the network to Private.
10. Save the server address for the client window.

The installation log is written to `<data>\logs\install-log.txt`. If the installation fails, Setup also writes `INSTALL-FAILED.txt` there.

- **Refuses existing installations.** If the `BurhanNexusHMS` service or `C:\BurhanNexusHMS` exists, Setup stops and changes nothing. Upgrading an existing installation is a separate step that always starts with a verified backup.
- **Uninstall** removes only the HMS service, its firewall rule and the program files. PostgreSQL, the database and the data folder are kept. Installing again reuses the same database.
- **Start menu → Burhan Nexus HMS → Server tools:**
  - Server status (services, `/health`, addresses for clients)
  - Restart, Stop and Start HMS server
  - Backups folder
  - Installation log

## Client Setup

- **Server address page.** The address is checked with `BurhanNexus.HMS.Client.exe --check-server`. If the server does not answer, Setup warns but still lets you continue.
- **WebView2.** Installed from Microsoft's bootstrapper only when it is missing. This step needs internet once.
- **Saved address.** The address is written to `%ProgramData%\BurhanNexusHMS\client.json`. Each Windows user can change it later from the window menu (right-click the title bar → Server settings…), which saves it in `%LOCALAPPDATA%`.
- **What it installs.** Desktop and Start-menu shortcuts only. It creates no service and no database.

## Client window

- **Server not reachable.** The window shows a reconnect screen and retries every 5 seconds. If the connection drops while a page is open, the page is kept, so unsaved entries are not lost.
- **Links.** Only the HMS server's pages open inside the window. Other web addresses open in the default browser.
- **Printing.** The page's own Print button goes through the app:
  - **Thermal receipts and slips** (58 or 80 mm) print directly to the chosen receipt printer. The page is exactly roll width × slip height, with no margins, so no paper is wasted.
  - **A4 and A5 documents** open a small printer/copies window and print on the page's own paper size, without the browser's date and URL header and footer.
  - **Preview / more options…** opens the normal browser print preview.
  - **Printers…** in the window menu changes the receipt and A4 printers.
- **Thermal pages in the HMS itself.** The lab receipt, lab slip and pharmacy slip now size their page to the slip in every browser (`lab-report.js`). Before, they inherited A4 from `lab-report.css`.

## LAN checklist

- **Fixed server IP.** Use a DHCP reservation on the router, or a static IP. If the IP changes, every client needs the new address.
- **Private network.** The server's network profile must be Private. Setup sets it, but check again after changing the router or cable.
- **No sleep.** Set Sleep = Never on the server, and put it on a UPS.
- **Ports.** Only port 5000 is opened. Port 5432 (the database) is never opened to the LAN.

## Backup and recovery

Automatic daily backup is **not** part of this stage. It is a separately approved stage.

- **Manual backup:** HMS → Backup & Restore → *Create backup now*. The file goes to `C:\ProgramData\BurhanNexusHMS\backups`; its size and SHA-256 are recorded.
- **Off-site copy:** copy `backups` and `data\keys` to an encrypted (BitLocker) USB drive every week. `data\keys` holds the server's data-protection keys: the sealed licence state and the login cookies.
- **Verify a backup:** run `pg_restore --list <file>`, which must read the whole archive, and `Get-FileHash <file> -Algorithm SHA256`, which must match the value shown in the HMS.
- **Restore:** use Backup & Restore. The HMS takes a safety backup before restoring.
- **New server computer:**
  1. Install with Server Setup.
  2. Put the `keys` folder back.
  3. Restart the server.
  4. Restore the backup.
  5. Give the clients the new address.

## Tests (CI, `.github/workflows/ci.yml`)

All tests run on a fresh `windows-2022` machine with WebView2 131.

- **`windows-install` job**
  - **Classic installer.** The `INSTALL.cmd` / `Easy-Install.ps1` path still works.
  - **Client window**
    - Opens the server from the saved address and from the LAN IP.
    - Two windows can run at once.
    - Shows the reconnect screen when the server is down, and reconnects automatically after the service restarts.
    - Print test: the receipt page is 80 × 124 mm, the A4 page is 210 × 297 mm, and there is no header or footer.
  - **Client Setup**
    - Installs silently with `/SERVER=`.
    - Creates the shortcuts and saves the address.
    - Creates no services.
    - The installed client opens the server.
    - A wrong address is reported as not answering.
    - Uninstalling leaves the server running.
  - **Print Agent.** Raw ESC/POS printing still works.
- **`server-setup` job**
  - **Silent install.** Health check passes and the setup wizard appears.
  - **Services.** Both are Automatic, with recovery actions set.
  - **Data location**
    - The data, backups, radiology and keys folders are in ProgramData.
    - PostgreSQL's data directory is in `<data>\pgdata`.
    - Nothing is left in the program folder.
  - **Shortcuts and firewall rule** are created.
  - **Client window on the server** opens the HMS.
  - **Recovery.** The server comes back after a restart, and after its process is killed.
  - **Refusal.** A second Setup run is refused (exit code 1) and the existing install keeps running.
  - **Uninstall.** The service and firewall rule are removed; the database tables and data are kept.
  - **Reinstall** reuses the same database.
  - **Multi-user LAN test** (`tools/ci/MultiUser-Test.ps1`)
    1. Completes the setup wizard.
    2. Logs in on two "computers" (Reception-PC and Lab-PC) at the same time.
    3. Registers 10 patients from each in parallel.
    4. Checks that all 20 patients are in the one database with 20 distinct Patient IDs.
    5. Checks that each computer can see the other computer's patients.
  - **After setup,** the client window shows the hospital's login page.

**Not testable in CI** (needs the hospital's hardware): physical Windows 10 PCs, real thermal and A4 printers, the real LAN and router. See the checklist in the Urdu guide.

## Files

- **New:**
  - `tools/BurhanNexus.Launcher/*` (client window)
  - `deploy/installer/Server.iss`, `deploy/installer/Client.iss`
  - `deploy/installer/server-tools/*.cmd`
  - `tools/ci/MultiUser-Test.ps1`
  - this document
- **Changed:**
  - `deploy/windows/Easy-Install.ps1`, `Install-Server.ps1`: optional parameters added; the defaults keep the classic behaviour.
  - `src/BurhanNexus.HMS.Web/wwwroot/js/lab-report.js`: thermal page size.
  - `.github/workflows/ci.yml`
- **Not changed:** HMS pages, services, database schema, licensing.
