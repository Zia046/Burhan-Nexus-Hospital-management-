# Private hospital appointments & thermal token slips

This guide covers the private-hospital reception workflow added in the Phase 2 correction: doctor-wise daily
tokens, the compact thermal appointment slip, and how to connect thermal printers.
Government hospitals are **not affected** – they keep the A5/A4 OPD and emergency slips.

## 1. Reception workflow (private hospital)

1. **Appointments → New Appointment** (or *New OPD visit* on a patient profile – in private mode it opens the same screen).
2. Search the patient by Patient ID, name, mobile or CNIC – or click **New patient**; after saving, the system returns to booking.
3. Select the **doctor**. Each doctor card shows the next token number, tokens already issued today and the fee (if you may see fees).
4. Optional: date (today or up to *Advance booking* days ahead), time (if enabled), complaint and notes.
5. **Save & print token** – the token is assigned at save time and the thermal slip opens (and prints, if auto-print is on).

### Token rules

| Rule | How it is guaranteed |
|---|---|
| Every doctor has his own series starting at **1 every day** (hospital time zone, e.g. Asia/Karachi) | token = highest token of *that doctor on that date* + 1 |
| No duplicates, even when several computers book at the same second | PostgreSQL transaction + per-doctor/per-date advisory lock, plus unique index `UX_Appointments_Doctor_Date_Token` (DoctorId, AppointmentDate, TokenNumber) |
| Cancelled tokens are **never reused** | cancelled rows stay in the table and are counted in the "highest token" |
| Edit / reprint never changes the token | edit only changes time, complaint, notes; reprint reads the stored token |
| Previous dates are never touched | a new date simply has no rows yet → starts at 1 |

### Lists and reports

* **Doctor-wise Lists** (`/Appointments`) – per date, one table per doctor with token, patient, status (Waiting / Seen / Cancelled), slip count.
* **Appointment Reports** – daily, monthly and yearly totals per doctor, day-wise / month-wise history, and the cancelled-appointment history with reasons. *Print / PDF* uses the browser's "Save as PDF".
* **Appointment details** – edit, mark as seen, cancel (reason required), print history (who, when, which computer, paper).

## 2. The thermal slip

Content: hospital name, logo (optional, browser printing only), doctor full name, qualification, specialization,
**large token number**, patient name, Patient ID, age/gender, date, time (if enabled), consultation fee (if enabled),
appointment number, the message (default *"Please wait for your turn."*), reception contact (if set), print date and a
"(REPRINT)" mark on copies.

*Settings → Appointment slips* controls: message, reception contact, show fee, record time, advance-booking days.

First print requires **Print Appointment Slip**, every later copy requires **Reprint Appointment Slip** (checked on the server).
Opening the preview is not counted as a print – a print is recorded when you press *Print*.

## 3. Printers – what actually works

Each reception computer can have its own printer profile (*Settings → Thermal printers*). The computer is identified by
the **computer name entered on the login page**; profile `*` is the default for all computers.

| Method | Printers | Dialog? | How it works | Tested |
|---|---|---|---|---|
| **Browser print** | any printer installed in Windows with its driver – USB, LAN or shared (Xprinter XP-Q200/XP-80/XP-58, Rongta RP80/RP58, Epson TM-T20/T82, HPRT TP80 …) | yes (browser print dialog) | page is sized to 58/80 mm × slip height | automated: 58 mm and 80 mm PDFs rendered by Chromium in CI |
| **LAN printer (direct)** | network thermal printers with ESC/POS on port 9100 | **no** | the HMS server sends ESC/POS bytes to `IP:9100` | automated: ESC/POS bytes received by a test TCP printer (token, cut command) |
| **Print Agent (USB, silent)** | any Windows printer queue (USB thermal printers with their driver, or the Windows *Generic / Text Only* driver) | **no** | the page sends ESC/POS to the agent on `127.0.0.1:9123`; the agent writes it RAW to the Windows queue | automated on Windows Server 2022 CI: agent installed as a service, RAW job spooled to a printer queue, output file checked |

Honest limits:

* A web page **cannot** print silently to a USB printer by itself – that is why the Print Agent exists. Without the agent, use
  Browser print (one click on *Print* in the dialog). Chrome/Edge can also be started with `--kiosk-printing` to skip the dialog.
* All three methods were tested automatically (see above), but **not on physical Xprinter/Rongta/Epson/HPRT hardware** in this
  project. These printers all understand the standard ESC/POS commands used (ESC @, ESC a, ESC E, GS !, ESC d, GS V), so they are
  expected to work; run *Test print* once on each printer when installing.
* ESC/POS printing is text-only (no logo) and ASCII (Urdu text is printed as `?`; use Browser print for Urdu messages).
* The Print Agent only accepts requests from the HMS server address configured at install time (or, if none was given, from
  addresses on the local network) and only listens on 127.0.0.1.

### Setting up a LAN thermal printer

1. Print the printer's self-test page (hold FEED while switching on) to see its IP address; give it a fixed IP in the router.
2. *Settings → Thermal printers*: computer name, **LAN printer (direct)**, IP, port `9100`, paper width.
3. Save, then **Test print**.

### Setting up a USB thermal printer (silent)

1. Install the printer's Windows driver; note the name in *Windows Settings → Printers & scanners* (e.g. `XP-80C`).
2. On that computer, from the server package, run as Administrator:
   `powershell -ExecutionPolicy Bypass -File .\deploy\windows\Install-PrintAgent.ps1 -HmsUrl "http://192.168.1.10:5000"`
   (use the address you type in the browser to open HMS). The script lists the printers it can see.
3. *Settings → Thermal printers*: computer name, **Print Agent**, Windows printer name exactly as listed, paper width.
4. Save, then **Test print**.

If the agent is not running, the slip page says so and offers **Print with browser dialog**.

## 4. Permissions

| Permission | Allows |
|---|---|
| Appointments – View | doctor-wise lists, appointment details |
| Appointments – Add | booking |
| Appointments – Edit | edit time/complaint/notes, mark as seen |
| Appointments – Cancel | cancel with reason |
| Appointments – Print | first slip print |
| Appointments – Reprint Appointment Slip | further copies |
| Appointments – Doctor-Wise Appointment Reports | reports page |
| Appointments – Manage Printer Settings | printer profiles and test prints |
| Appointments – Financial Access | see consultation fees in lists |
| Appointments – Settings Access | appointment slip settings |

All of them are hidden in the menu/buttons and enforced on the server.

## 5. Audit

Booking, edit, cancel, seen, first print, every reprint (with copy number, paper and method), failed LAN prints,
printer-profile changes and test prints are written to the audit log.
