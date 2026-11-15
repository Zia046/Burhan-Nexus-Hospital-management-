# Phase 6 – testing report (Radiology: X-Ray & Ultrasound)

**Result:** 72 / 72 automated tests passed on a real PostgreSQL 16 database (GitHub Actions).

- The total includes all tests from Phases 1–5, so the earlier phases still work.
- The browser walkthrough completed without errors.
- The Windows installer job passed.

## 1. Automated tests (`tests/…/RadiologyTests.cs`)

| Test | Areas checked (prompt module 16) |
|---|---|
| Private booking, queue, report, verification, release, print and billing | 1–7, 9–13, 16 |
| Rejection, correction, amendment and history are preserved | 7–9, 12, 14, 16 |
| Government bookings need a visit; permissions are enforced on the server | 3, 4, 14–16, 18 |
| Attachments are stored as files with permissions and survive backup archives | 15, 19 |
| Concurrent bookings get unique order numbers and tokens | 17 |
| Radiology reports, exports, and previous phases still work | 12, 15, 19 |

**Private booking, queue, report, verification, release, print and billing:**

- Services are created as Phase 3 services, and the starter catalogue and templates are seeded.
- One booking holds an X-Ray and an Ultrasound. Each modality gets its own token, and the second patient gets X-Ray token 2.
- A double submit creates no second order, and the attempt is audited.
- The X-Ray and Ultrasound worklists are separate.
- A report before the examination is refused.
- Arrive, start and complete record the technician.
- Draft, then submit. The author cannot verify their own report; another radiologist verifies it.
- The PREVIEW is shown before release. *Release & print* works, a reprint as PDF gives copy 2, and no new order or report is created.
- Letterhead mode works.
- The fee is collected at the Phase 3 counter (partial payment). Manual collection of the radiology service is refused.
- Dashboard, history, patient profile and revenue report all work.

**Rejection, correction, amendment and history are preserved:**

- The default template pre-fills the report. Submitting needs a conclusion.
- A rejection needs a reason. The rejected text is kept as revision 1, and the corrected report is verified again.
- A verified report cannot be edited: page, service and database trigger all refuse it. Deleting history fails.
- An amendment needs a permission and a reason. It is kept as revision 2, then verified again and released.
- Editing a template does not change the saved report.
- A verified report cannot be cancelled. A cancellation needs a reason, and the unpaid charge is cancelled.
- A cancelled examination cannot be re-opened at database level.

**Government bookings need a visit; permissions are enforced on the server:**

- Government default: a booking without a visit is refused, a valid OPD visit is accepted, and an old visit or another patient's visit is refused.
- An X-Ray technician:
  - can use only the X-Ray queue;
  - is refused (403) on the Ultrasound worklist and examination, booking, verify, cancel, report, settings and statistics;
  - does not see those menu items.
- Reception sees no fees and cannot start examinations or give discounts.
- A user without radiology rights cannot open radiology at all.

**Attachments are stored as files with permissions and survive backup archives:**

- A fake image (text) is refused. A PNG and a DICOM file are stored under random names with SHA-256, and their content is checked on download.
- View and manage are separate permissions, and the other modality is refused.
- Removal needs a reason; the record is kept, and the database refuses rewrite or delete.
- The storage folder is archived and restored, existing files are never overwritten, and path traversal is refused.

**Concurrent bookings get unique order numbers and tokens:**

- 8 bookings from 4 computers at the same moment give tokens 1–8 and 8 unique order numbers.
- Rescheduling gives a new token on the new day, and past dates are refused.

**Radiology reports, exports, and previous phases still work:**

- All 11 reports open. Excel / CSV exports work and are audited.
- Every radiology page and the Phase 1–5 pages open.
- An Ultrasound-only user cannot see X-Ray statistics or revenue.

`ResetAndCatalogTests` checks that Radiology is a real module with 27 permissions, and that the old placeholder modules XRay and Ultrasound are gone.

## 2. Browser walkthrough (`tools/ui-check/phase2.mjs`)

The walkthrough (Private mode):

1. Activate and price two starter services; set self-verification and the default radiologist.
2. Book Chest X-Ray and Ultrasound Abdomen against the OPD visit.
3. X-Ray and Ultrasound worklists.
4. Arrive, start, complete; upload an image.
5. Write the report from the template and submit it.
6. Verification queue, then verify.
7. Release & print, and save the A4 PDF.
8. Dashboard, statistics and patient history.

Screenshots `p6-01 … p6-14` are on the `ci-results` branch. The sample report is `docs/samples/sample-radiology-report-A4.pdf`.

## 3. LAN and migration

- The module uses only the application server and PostgreSQL. No internet or external service is called, and files are stored on the server's disk.
- The `Phase6Radiology` migration only adds:
  - 9 tables;
  - 1 nullable column (`Charges.RadiologyOrderId`);
  - 2 sequences;
  - indexes.
- It runs automatically at start-up. Existing data is not changed.
- Database triggers are re-applied at every start and after a restore.

## 4. Known limitations

- **No PACS / DICOM viewer.** DICOM files are stored and downloadable only. There is no modality worklist (DICOM MWL) and no connection to X-Ray / ultrasound machines.
- **Fees:** fees are set per service in Phase 3. A Government hospital that charges nothing keeps the services free.
- **Release and printing:** both happen in the browser (print dialog / *Save as PDF*). The PDF is not stored on the server, but every copy is recorded.
- **Attachment backups:**
  - They are written next to the database backup in the backup folder.
  - The *Download backup* button downloads only the database file. Copy the `.radiology-files.zip` from the server's backup folder as well when you move backups off the server.
