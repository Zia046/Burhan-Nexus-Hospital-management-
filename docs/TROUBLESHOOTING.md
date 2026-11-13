# Troubleshooting

| Problem | What to check |
|---------|---------------|
| Other computers cannot open `http://<server-ip>:5000` | 1) Server is on and the **BurhanNexusHMS** service is *Running* (`services.msc`). 2) `http://localhost:5000` works on the server itself. 3) Server network profile is **Private**, firewall rule *Burhan Nexus HMS (TCP 5000)* exists. 4) Both computers are on the same network (`ping <server-ip>`). 5) Correct IP – the dashboard shows the server's LAN addresses under *Connect other computers*. |
| Server IP changed after a restart | Give the server a static IP or a DHCP reservation in the router. |
| Service does not start | Event Viewer ▸ Windows Logs ▸ Application (source *BurhanNexusHMS* / *.NET Runtime*). Common causes: PostgreSQL service stopped, wrong password in `appsettings.Production.json`, port already used by another program (change the port). |
| `/health` shows *Unhealthy* / dashboard shows *Database Unavailable* | Start the `postgresql-x64-16` service; check the connection string. |
| Login: *Invalid username or password* | Username is not case-sensitive; password is. Ask the administrator to reset the password. |
| Login: *account is locked* | Wait the lock time (default 15 min) or ask the administrator to press **Unlock account**. |
| Login: *account is inactive* | The administrator deactivated the account – ask them to activate it. |
| *Too many attempts* (429) | Too many logins from one computer within a minute – wait one minute. |
| Logged out unexpectedly | Idle timeout (default 30 min, Settings ▸ Security), password reset, deactivation, database restore, or the server was restarted after its `data\keys` folder was removed. |
| A menu item is missing for a user | The user lacks the permission. Check *Users ▸ Permissions* (green dot = allowed, red *Deny* overrides responsibilities). |
| *Access denied* page | Same as above; the attempt is recorded in the audit log. |
| *This record was changed by another user…* | Someone saved the same record on another computer first. Reload the page and repeat your change. |
| *Read-only mode* / *Trial expired* | Activate a licence on Settings ▸ License. Data is safe; backups still work. |
| *Licence verification required* | Licence data could not be verified on this server (e.g. moved to a new server without `data\keys`, or the database licence row was edited). Enter the activation key again. |
| *Server clock appears to have been set back* | Correct the server's date/time (enable automatic time). |
| Backup: *PostgreSQL tools were not found* | Set `Hms:PostgresBinDirectory` to e.g. `C:\Program Files\PostgreSQL\16\bin` and restart the service. |
| Backup fails with version mismatch | `pg_dump` must be the same or newer major version than the PostgreSQL server – point `PostgresBinDirectory` to the matching version. |
| Logo upload refused | Use PNG, JPEG, GIF or WebP smaller than 2 MB (SVG is not accepted for security). |
| Printing layout wrong | Settings ▸ Printing: choose the paper size; in the browser print dialog select the correct printer, *Margins: Default*, *Scale: 100%*, and enable *Background graphics* for the logo. |

Logs: the service writes to the Windows Event Log. To run in a console for diagnosis: stop the service, open PowerShell in `C:\BurhanNexusHMS` and run `.\BurhanNexus.HMS.Web.exe` – errors are printed directly.
