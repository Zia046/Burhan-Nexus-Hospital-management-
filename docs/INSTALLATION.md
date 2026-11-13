# Installation – central server and LAN computers

Burhan Nexus HMS runs on **one server computer**. All other computers only need a web browser.

```
            ┌──────────────── Hospital LAN (cable or Wi-Fi) ────────────────┐
 Reception PC ──┐                                                          │
 Laboratory PC ─┤   http://192.168.1.100:5000   ┌──────────────────────────┐
 Pharmacy PC ───┼──────────────────────────────▶│ SERVER  192.168.1.100     │
 X-Ray / US PC ─┤                               │  BurhanNexusHMS service   │
 Admin laptop ──┘                               │  PostgreSQL database      │
                                                └──────────────────────────┘
```

## 1. Server requirements

| Item | Minimum | Recommended |
|------|---------|-------------|
| OS | Windows 10/11 Pro or Windows Server 2019+ (64-bit) | Windows Server 2022 / Windows 11 Pro |
| CPU / RAM | 4 cores / 8 GB | 6+ cores / 16 GB |
| Disk | 100 GB SSD | 256 GB+ SSD, second disk or NAS for backups |
| Network | Fixed (static) IP address on the LAN | Cable connection, UPS power backup |

> Give the server a **static IP** (e.g. `192.168.1.100`) in Windows network settings or reserve it in the router (DHCP reservation). Client computers use this address.

## 2. Install PostgreSQL (once)

1. Download PostgreSQL **16 or newer** for Windows (EDB installer) and run it.
2. Keep the default port **5432**. Choose a strong password for the `postgres` administrator and write it down safely.
3. Components: *PostgreSQL Server* and *Command Line Tools* are required (pgAdmin is optional).

PostgreSQL only needs to accept connections from the server itself – do **not** open port 5432 in the firewall.

## 3. Install Burhan Nexus HMS

1. Extract the server package `BurhanNexus-HMS-Server-win-x64.zip` (e.g. to `C:\Setup\BurhanNexusHMS`).
2. Open **PowerShell as Administrator** in that folder and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\windows\Install-Server.ps1 -PostgresPassword "<postgres password>"
```

Optional parameters: `-Port 5000`, `-InstallDir C:\BurhanNexusHMS`, `-DbName burhan_nexus_hms`, `-DbUser bnhms_app`.

The script:
- creates the database `burhan_nexus_hms` and a dedicated login `bnhms_app` with a generated strong password,
- copies the application to `C:\BurhanNexusHMS`,
- writes `appsettings.Production.json` (readable only by Administrators/SYSTEM),
- registers the **BurhanNexusHMS** Windows service (automatic start, auto-restart on failure),
- opens TCP port 5000 in Windows Firewall for *Domain* and *Private* networks only,
- prints the LAN addresses to use.

3. Open `http://localhost:5000` on the server. The **Initial Setup Wizard** appears (see the user guide).

## 4. Connect other computers

On every reception / laboratory / pharmacy / X-Ray / ultrasound / admin computer:

1. Connect to the same LAN (cable or Wi-Fi).
2. Open Google Chrome, Microsoft Edge or Firefox.
3. Go to `http://<server-ip>:5000` – for example `http://192.168.1.100:5000`.
4. Bookmark it or create a desktop shortcut (Edge/Chrome ▸ *More tools* ▸ *Create shortcut* ▸ *Open as window*).
5. On the login page open **"This computer"** and enter a name such as `Reception-1`. It is remembered on that computer and shown in the audit log and login history.

Nothing is installed on client computers and **no data is stored on them** – everything is saved in the central database, so a change saved on one computer is immediately available on all others.

> If the Windows network profile of the server is **Public**, change it to **Private** (Settings ▸ Network ▸ properties), otherwise the firewall rule does not apply.

## 5. Configuration reference (`C:\BurhanNexusHMS\appsettings.Production.json`)

```json
{
  "ConnectionStrings": { "HmsDatabase": "Host=localhost;Port=5432;Database=burhan_nexus_hms;Username=bnhms_app;Password=..." },
  "Kestrel": { "Endpoints": { "Http": { "Url": "http://0.0.0.0:5000" } } },
  "Hms": {
    "DataDirectory": "data",              // encryption keys – back this folder up once
    "BackupDirectory": "backups",         // can be another disk, e.g. "D:\\HMS-Backups"
    "PostgresBinDirectory": "C:\\Program Files\\PostgreSQL\\16\\bin",
    "AutoMigrate": true,
    "LoginRateLimitPerMinute": 30
  }
}
```

After changing the file: `Restart-Service BurhanNexusHMS`.

### Changing the port or address
Change `Kestrel:Endpoints:Http:Url` (e.g. `http://0.0.0.0:8080`), add a firewall rule for the new port and restart the service.

### Optional HTTPS on the LAN
1. Obtain a certificate (`.pfx`) for the server name/IP (e.g. from the hospital IT or `New-SelfSignedCertificate`) and install it as trusted on client computers.
2. Add an endpoint:
```json
"Kestrel": { "Endpoints": {
  "Http":  { "Url": "http://0.0.0.0:5000" },
  "Https": { "Url": "https://0.0.0.0:5001", "Certificate": { "Path": "C:\\BurhanNexusHMS\\data\\server.pfx", "Password": "..." } }
} }
```
3. Open port 5001 in the firewall and use `https://192.168.1.100:5001`.

## 6. Updating to a new version

1. Create a backup (Backup & Restore ▸ *Create backup now*).
2. Extract the new package and run `deploy\windows\Update-Server.ps1` as Administrator.
Data, backups and configuration are kept; database changes are applied automatically at start-up.

## 7. Uninstalling

`deploy\windows\Uninstall-Server.ps1` removes the service and firewall rule. The database, `data` and `backups` folders are **kept** on purpose.

## 8. Important server folders – include in your IT backup plan

| Folder | Content |
|--------|---------|
| `C:\BurhanNexusHMS\backups` | Database backups (copy regularly to USB/NAS) |
| `C:\BurhanNexusHMS\data\keys` | Encryption keys for sessions and licence data. Back up once after installation. If lost, users simply log in again and the licence may need re-activation – hospital data is never affected. |
| `appsettings.Production.json` | Server configuration incl. database password |
