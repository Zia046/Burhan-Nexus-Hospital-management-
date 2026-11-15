# Phase 6 – Radiology (X-Ray & Ultrasound)

The radiology module handles X-Ray and Ultrasound work:

- booking from the doctor's handwritten advice (no doctor login or digital order needed);
- separate X-Ray and Ultrasound queues;
- examination steps, reporting, verification and release;
- A4 reports with print, PDF and reprint;
- image / document attachments;
- statistics.

All money goes through the Phase 3 cashier.

Menu **Radiology** has these screens:

- Dashboard · New Booking · X-Ray Worklist · Ultrasound Worklist · Bookings
- Reporting · Report Verification · Patient History
- Radiology Reports · Services & Templates · Radiology Settings

## 1. Services & templates (no hard-coded catalogue)

- A service has:
  - code, name and modality (X-Ray / Ultrasound);
  - body part / examination type, description and preparation instructions (shown at booking);
  - estimated duration, report heading and a default report template;
  - fee, active / inactive status and sort order.
- Every service is also a Phase 3 service (`RAD-<code>`, kind X-Ray or Ultrasound). Its fee history is kept there.
- A new service starts free. Changing the fee needs **Manage Radiology Fees**. Bookings keep the fee that applied when they were made.
- A starter catalogue is created **inactive and free**:
  - X-Ray: Chest PA / AP, Abdomen, Pelvis, Skull, Cervical / Lumbar spine, KUB, Knee, Shoulder;
  - Ultrasound: Abdomen, Pelvis, KUB, Obstetric, Pregnancy profile, Thyroid, Breast, Soft tissue.
  - The hospital sets the fees and activates the services it offers.
- Report templates are editable texts per modality: technique, findings, impression and recommendations. A template only pre-fills a new report. Changing a template later never changes saved reports.

## 2. Booking (*New Booking*)

1. Search the patient by Patient ID, name, phone or CNIC. A new patient is registered only through the normal patient registration (*Register new patient* returns to the booking), so no duplicate records are created.
2. Choose the patient's OPD / Emergency visit. The visit's department and doctor are proposed as the referral.
3. Tick one or more examinations. X-Ray and Ultrasound can be mixed in one booking.
4. Enter the date / time (today or up to 90 days ahead), the referring department, the referring doctor (hospital or external), the priority and the clinical indication.
5. Save. The booking gets:
   - an order number `RAD-2026-000001`;
   - a daily **token per modality**, shared by the examinations of one order (the patient queues once);
   - one Phase 3 charge per examination.

Rules:

- **Government hospital (default):** bookings only against an OPD / Emergency visit of the last *N* days (setting, default 7).
- **Private hospital (default):** other authorised bookings are also allowed (e.g. referred patients without a visit).
- This rule is a radiology setting and is reset to the default when the hospital type changes.
- Fees follow the Phase 3 service fees of the hospital. A Government hospital can keep services free.
- Discounts need the Phase 3 *Give Discounts* permission and a reason.
- A double click never creates two orders.
- Tokens and order numbers are unique even when several counters book at the same moment.

## 3. Queue & examination

The X-Ray and Ultrasound worklists show:

- token, patient and Patient ID;
- examination, booking date / time, referring department, priority and status.

Open work from earlier days stays visible.

| Status | Meaning |
|---|---|
| Booked | booked, patient not yet here |
| Waiting | patient arrived |
| In progress | examination started (technician recorded) |
| Examination completed | finished; technician name and technical notes recorded |
| Report pending | sent back by the verifier or re-opened for amendment |
| Report entered | draft report saved |
| Awaiting verification | report submitted |
| Verified | approved by a verifier |
| Released | final report released / printed |
| Cancelled | cancelled with a reason – stays in history |

- An examination that has not started can be rescheduled. A new date gets a new token.
- Cancelling needs a reason. An unpaid fee is cancelled automatically. A paid fee is refunded from the receipt through Admin refunds.
- An examination with a verified report cannot be cancelled.

## 4. Reporting, verification & release

- **Report entry** has these fields:
  - patient details, order number, examination and examination date;
  - clinical indication, technique, findings, impression and recommendations;
  - reporting doctor and designation (defaults come from the settings).
- You can load a template, save a draft, edit the draft and submit it for verification.
- Only what every report needs is mandatory on submit: findings or impression, and the reporting doctor's name.
- **Verification queue** (*Report Verification*). The verifier approves, or rejects with a reason.
  - A rejected report goes back to *Report pending*, and the rejected text is kept as a revision.
  - The corrected report is verified again.
  - The reporter and the verifier, with their times, are recorded.
- **Self-verification** is refused unless the settings allow it *and* the user has *Verify Own Report*.
- **Release.** Only a verified report can be released as final. This is done with *Release final report* or *Release & print*. Optionally, release can require full payment.
- **Amendment of a verified report.** It needs *Amend Verified Report* and a reason.
  - The verified text is stored as a revision and the report returns for correction and re-verification.
  - The database refuses any direct change of a verified report's text.

## 5. A4 report, PDF & reprint

`/Print/RadiologyReport/{exam}` contains:

- the hospital logo, name, address and contact, plus an extra header line;
- patient details, Patient ID, order number, examination and examination date;
- referral, indication, technique, findings, impression and recommendations;
- the reporting doctor and designation (optional signature image), *verified by* and the footer.

Printing and copies:

- Every print / PDF is recorded as a copy (copy 1, 2 …).
- Reprints need *Reprint Previous Report* and **never create a new order or report**.
- For a PDF, choose *Save as PDF* in the browser's print dialog.

**Pre-printed letterhead:** untick *Print the hospital header* in the settings. The report then leaves a configurable space at the top (default 45 mm).

## 6. Images & documents

- On the examination page you can upload JPG, PNG, PDF or DICOM (`.dcm`), up to the configured size (default 25 MB).
- The file type is checked from the file's own bytes.
- Files are stored on the server under a random name in the folder `Hms:RadiologyStorageDirectory` (appsettings.json, default `radiology-files` next to the application). Only the metadata is in PostgreSQL: patient, examination, size, SHA-256, uploader and time.
- Opening and downloading files needs *View Attachments* and is audited. Uploading and removing needs *Manage Attachments*.
- Removing hides a file (with a reason). The file and its record are never deleted.
- DICOM files are stored and downloadable. **This is not a PACS and contains no DICOM viewer.**
- **Backup:** every system backup also writes `<backup>.radiology-files.zip` with the storage folder. A restore puts the files back (existing files are never overwritten).
- Everything works on the hospital LAN without internet.

## 7. Billing (Phase 3)

- Each examination creates one Phase 3 charge (`Charges.RadiologyOrderId`).
- Payment, partial payment (if the service allows it), outstanding balance, receipt numbers, cashier attribution and refunds all stay in Phase 3.
- *Collect payment* on the order / examination opens the cash counter with the balances filled in.
- Radiology services cannot be added by hand at the cash counter, so they cannot be billed twice. They are billed when the examination is booked.
- Radiology never changes cashier balances itself. Revenue figures come only from receipts.

## 8. Dashboard, history & reports

- **Dashboard** (*All / X-Ray / Ultrasound*). Users with only one worklist permission see only their modality. It shows:
  - X-Ray and Ultrasound bookings, pending examinations, patients waiting, in progress;
  - reports pending, awaiting verification, verified / released, cancelled;
  - this month's figures;
  - collection, only with *View Financial Information*;
  - plus queue, reporting and verification lists.
- **Patient history:**
  - all X-Ray and Ultrasound examinations, with dates, order numbers, report status, verifier, number of files and print copies;
  - report / reprint links;
  - a radiology card on the patient profile.
- **Reports** (date filters; daily / monthly / yearly; print / PDF, Excel, CSV; exports audited):
  - daily X-Ray examinations, daily Ultrasound examinations, examination statistics;
  - service-wise counts, department-wise referrals, doctor-wise referrals;
  - pending examinations, pending reports, verified reports, cancelled bookings;
  - revenue (Phase 3 receipts, only with *View Financial Information*).

## 9. Permissions (role editor → *Radiology Management*)

Permissions (27):

- View Radiology Dashboard · View X-Ray Worklist · View Ultrasound Worklist
- Create Booking · Edit Booking · Cancel Booking
- Start Examination · Complete Examination
- Enter Report · Edit Draft Report · Submit Report
- Verify Report · Verify Own Report · Release Report · Amend Verified Report
- Print Report · Reprint Previous Report · Export PDF
- View Patient Radiology History · View Attachments · Manage Attachments
- Manage Radiology Services · Manage Report Templates
- View Statistics · View Financial Information · Manage Radiology Fees · Manage Settings

How they are enforced:

- Every page and every action is checked on the server, including access to the other modality.
- Menu items and buttons the user may not use are hidden.
- Important actions are written to the audit log.

## 10. Database

**New tables:**

- `RadiologyServices`, `RadiologyTemplates`
- `RadiologyOrders`, `RadiologyExams`
- `RadiologyExamEvents`, `RadiologyReportRevisions`, `RadiologyReportPrints`
- `RadiologyAttachments`, `RadiologySettings`

**Other changes:**

- `Charges` gained `RadiologyOrderId`.
- New sequences `radiology_order_seq` and `radiology_service_code_seq`.

**Migration:** `Phase6Radiology` only adds tables and columns.

**Protection:**

- Events, revisions and print copies are insert-only.
- Services, orders, examinations and attachments cannot be deleted.
- A verified report cannot be changed directly.
- A cancelled examination cannot be re-opened.
- An attachment record cannot be rewritten.
- Order numbers are unique, and booking is idempotent.
- Tokens are serialised per modality and day.
- Money is stored as decimals.
