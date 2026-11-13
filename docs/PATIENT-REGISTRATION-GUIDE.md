# Patient registration guide (Phase 2)

## The Patient ID
- Every patient receives **one permanent Patient ID** automatically, e.g. `BNH-000025`.
- The number comes from a PostgreSQL sequence, so two reception computers registering at the same second can never get the same ID.
- The Patient ID **never changes** and is **never re-used**. A patient who returns after days, months or years keeps the same ID.
- Each OPD or Emergency visit gets its **own visit number** (`OPD-2026-000123`, `ER-2026-000045`); the Patient ID stays the same.
- Format: *Hospital Settings ▸ Patient ID & slips* (prefix, number of digits). Changes apply to new patients only.

## Registering a new patient (Patients & Visits ▸ Register Patient)
1. Type the **name**, choose **gender**, relation (S/O, D/O, W/O) and **father's/husband's name**.
2. Enter the **date of birth** if known – the age is calculated automatically (years, months or days for infants).
   If the date of birth is unknown, type the **age** and choose *Years / Months / Days*.
3. Optional: mobile, alternative contact, CNIC (dashes are added automatically), blood group, notes.
4. Address and city/village.
5. Press one of:
   - **Register & OPD visit** – goes straight to the OPD visit screen (fastest for reception),
   - **Register & Emergency**,
   - **Register** – opens the patient profile.

Registration date and time are recorded automatically, together with the user and computer.

## Duplicate warning
While you type, the system checks for possible existing patients by **name, father's/husband's name, CNIC, mobile number and Patient ID**.
If something similar exists you see:

> **Possible existing patient found. Please verify before creating a new registration.**

with the matching patients and the reason (*Same CNIC*, *Same mobile number*, *Similar name*, *Same father's name*). You can:
- **Use – OPD / Use – Emergency** the existing patient (recommended when it is the same person), or
- tick **"I have checked the list – this is a different person"** and register again. This decision is recorded in the audit log.

Records are never merged automatically.

## Finding a patient (Patient Search)
Type any of: Patient ID (`BNH-000025`, or just `25`), name, mobile, CNIC, father's/husband's name or a visit number.
An exact Patient ID opens the profile directly. Filters: registration date, gender, OPD patients, Emergency patients, active / archived / merged records.
*Export CSV* (permission *Export Patient Records*) downloads the current result list; every export is audited.

## Patient profile
Shows the Patient ID, all details, age (always current), registration date and the **complete visit history** (OPD and Emergency, filter by type and date). Each visit opens its details and its printable slip. Later phases add laboratory, pharmacy, X-Ray and ultrasound history to the same profile.

## Correcting information
*Edit* (permission *Edit Patient Information*) changes details; the Patient ID cannot be changed. Every change is stored in the audit log with the previous and new values.

## Archive instead of delete
Patient records are never deleted. *Archive* (with a reason) hides a record from normal search and blocks new visits; *Restore* brings it back.

## Correcting a duplicate registration (administrators)
If the same person was registered twice: open the duplicate profile ▸ **Correct duplicate registration** ▸ enter the correct Patient ID ▸ reason ▸ type `MERGE`.
- All visits of the duplicate move to the correct patient (the original patient reference is kept on each visit).
- The duplicate's Patient ID keeps working in search and points to the correct patient.
- The duplicate record stays in the database with status *Merged*; nothing is deleted; the correction is audited.

## Permissions (Users ▸ Permissions ▸ Patient Management)
| Permission | Allows |
|------------|--------|
| Search Patients | Patient search page and search API |
| View Patient Profile | Open profiles |
| Register Patients | Registration screen |
| Edit Patient Information | Correct details |
| View Previous Visits | Visit history on profile |
| Print Patient Record | Printable patient summary |
| Export Patient Records | CSV export |
| Archive / Restore | Archive and restore |
| Correct Duplicate Registrations | Merge workflow |
| Registration Settings | Patient ID / visit number / slip settings |
