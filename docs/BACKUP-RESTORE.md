# Backup & restore

Open **System ▸ Backup & Restore** (requires *System Backup* permissions; ordinary users never see it).

## Create a backup
1. Optionally type a note (e.g. *End of month*).
2. Press **Create backup now**. A complete PostgreSQL backup (`pg_dump`, custom format, compressed) of the central database – users, permissions, settings, logo, audit log and all future module data – is written to the server folder `backups` (configurable).
3. The history shows date/time, file, type, size, status and who created it. Each file's SHA-256 checksum is stored to detect damage later.

Recommended routine:
- at least **once a day** (end of shift) and **before every update**,
- **Download** backups and keep copies on a USB drive / another computer / NAS (a backup on the same disk does not protect against disk failure or theft),
- keep several generations (e.g. last 7 daily + last 12 monthly).

Automatic scheduled backups are planned for a later phase; the history and file layout are already prepared for it.

## Download a backup
Press **Download** (permission *Download Backup*). The download is recorded in the audit log.

## Restore a backup (protected workflow)
Restoring **replaces all current data** with the content of the backup.

1. Press **Restore** next to the backup (permission *Restore Backup*).
2. Read the warning, tick *I understand…*, enter **your password** and type `RESTORE`.
3. The system then:
   - verifies the file checksum (damaged files are refused),
   - creates an automatic **safety backup** of the current data first (restore is cancelled if this fails),
   - restores in a **single transaction** (if anything fails, nothing is changed),
   - applies any newer database updates and re-protects the audit log,
   - records the restore in the audit log.
4. Everyone, including you, must log in again.

If the restore was a mistake, restore the *Safety (before restore)* backup shown at the top of the history.

Wrong passwords and cancelled restores are audited. Ordinary users can never restore or delete backups – the server refuses even direct requests.

## Restoring on a new server (disaster recovery)
1. Install PostgreSQL and Burhan Nexus HMS on the new server (see INSTALLATION.md) but **do not** complete the setup wizard.
2. Copy the `.backup` file into the new server's `backups` folder.
3. Restore from the command line (PowerShell, as Administrator):
```powershell
$env:PGPASSWORD = "<bnhms_app password from appsettings.Production.json>"
& "C:\Program Files\PostgreSQL\16\bin\pg_restore.exe" --clean --if-exists --no-owner --no-privileges --single-transaction `
  -h localhost -U bnhms_app -d burhan_nexus_hms "C:\BurhanNexusHMS\backups\<file>.backup"
Restart-Service BurhanNexusHMS
```
4. If you also copied the old server's `data\keys` folder, everything works immediately. Otherwise users log in again and the licence may ask for the activation key once more – hospital data is unaffected.
