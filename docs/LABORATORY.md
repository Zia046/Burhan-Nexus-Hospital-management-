# Phase 4 – Laboratory Management System (LMS)

This guide describes the laboratory module of Burhan Nexus Hospital Management. It covers the test catalogue, patient
booking, samples, result entry, verification, A4 reports, laboratory reports and how laboratory fees go through the existing
Phase 3 cashier. The module works in both Government and Private hospital mode. It does not change the OPD, Emergency,
appointment, token, receipt or thermal-printing workflows.

Menu: **Laboratory** (sidebar) → Lab Dashboard · New Lab Visit · Sample Collection · Pending Tests · Verification ·
Lab Results · Lab Reports · Test Catalogue · Lab Settings. Each user sees only the items they have permission for.

## 1. Main rules

| Rule | How it is enforced |
|---|---|
| Every lab visit has a unique number (`LAB-2026-000123`) and every sample a sample number (`SMP-2026-000045`) | PostgreSQL sequences + unique index |
| A double-click never creates two orders | one-time key per booking form; unique index `UX_LabOrders_Idempotency`; a repeat opens the first order |
| Changing a test later never changes old orders | at booking, the test name, method, category, sample type, parameters, units and the **reference range for that patient** (gender and age) are copied into the order |
| Unverified results are never released as a final report | printing accepts only *Verified/Released* tests; unverified tests are listed as “pending” and are not printed; the preview has a **PREVIEW** watermark until the copy is recorded |
| A verified value cannot be changed silently | database trigger `bnhms_lab_value_guard` refuses changes to values, units and ranges of verified/released results; a correction is a **withdrawal with a reason**. The old version is stored as a revision, and the test is entered and verified again |
| Lab history is never deleted | orders, tests, samples and values cannot be deleted; events, revisions and print history are insert-only (database triggers) |
| The person who entered a result does not verify it | unless the hospital allows self-verification in *Lab Settings* **and** the user has *Verify Own Results* |
| Lab fees use the Phase 3 cashier | each test has a Phase 3 service (`LT-<code>`) with fee history; booking creates one charge per test; payment is collected at the cash counter; receipts, refunds and the cashier ledger are the Phase 3 ones. There is **no separate laboratory ledger** |
| Everything is audited | booking, edits, cancellations, sample collection/rejection, result save, verification, rejection, withdrawal, printing, reprinting, settings and exports |

## 2. Test catalogue (*Test Catalogue*)

* **Categories** (Haematology, Biochemistry, Serology, Urine, Stool, Hormones …) and **sample types** (Blood EDTA, Serum,
  Plasma fluoride, Urine, Stool, Other) can be added or deactivated.
* **Test**: code, name, category, sample type, method, turnaround time, collection/patient instructions, default comment,
  active/inactive, display order. **Price** (free or paid, effective date, partial payment allowed) needs *Manage Laboratory Fees*.
  The price is kept as a Phase 3 service fee, so old receipts keep their fee.
* **Parameters** (*Manage Test Parameters*): name, unit, result type (**Numeric**, **Text**, **Positive/Negative**, **Options**
  e.g. `Reactive|Non-reactive`), decimals, default value, optional/active, order.
* **Reference ranges** (*Manage Reference Ranges*): for all patients, or for male/female only, for an age band (years, months or days).
  Each range has low/high limits, critical low/high limits and a normal text (e.g. “Negative”). The most specific range is used
  (gender first, then the narrower age band).
* A starter catalogue (CBC, Hb, blood sugar random/fasting, HbA1c, LFT, RFT, lipid profile, urine and stool routine, TFT) is
  created **inactive and free**. The laboratory reviews the ranges, sets prices and activates the tests.
* Every change increases the test's *definition version*. Each booked test shows the version it was booked with.

## 3. Booking a lab visit (*New Lab Visit*)

1. Search the patient by Patient ID, name, phone or CNIC. If the patient is not registered, use **Register new patient**.
   This needs *Patients → Add* or *Register Laboratory Patient*. After registration the screen returns to the booking page.
   The permanent Patient ID is always used.
2. Tick the tests. The filter box helps with long lists. The total is shown to users with financial permission. Discounts per
   test need *View Laboratory Financial Information* + Phase 3 *Give Discounts*.
3. Referral: none, hospital doctor, or external doctor. Add clinical notes, and mark *urgent* if needed.
4. **Save**. The visit opens with the order number, samples (one shared sample per sample type), payment status and an
   **order slip** (80 / 58 mm). Tests can be added or cancelled later; cancelling needs a reason. An unpaid charge is cancelled,
   and a paid one must be refunded at the cashier.

Previous lab visits of the patient are listed on the booking page and under *Previous results*.

## 4. Payment (Phase 3 cashier)

*Collect payment* on the lab visit opens the Phase 3 *Collect Payment* screen with the lab balance already filled in. Free,
paid, discounted, partial (only if the test allows it) and full payments, refunds and receipts all work exactly as in Phase 3.
The same charge cannot be paid twice: the rows are locked and every payment uses a one-time key.
Optional policy (*Lab Settings*): **release reports only for fully paid tests**.

## 5. Samples (*Sample Collection*)

Status flow of a test:

`Sample pending → Sample collected → Sample received → In progress → Awaiting verification → Verified → Released`
(or `Cancelled`).

*Collect & receive* (or *Collected only* followed by *Receive*) updates every test that shares the sample. To reject a sample,
you must give a reason. A replacement sample number is created automatically, and the tests go back to *Sample pending*.

## 6. Fast result entry (*Pending Tests*)

* The pending list shows only tests that still need work, grouped by patient: urgent first, amber = sample not collected,
  red = sent back for correction. As soon as a result is saved, the test leaves this list.
* The entry screen shows all tests of the visit as tabs. Each test has its parameters, units and reference ranges. The technician
  only types the values: **Enter** jumps to the next box, and values outside the range turn red while typing.
* **Save draft** keeps the values without sending them. **Save result** sends the test for verification. **Save & next test**
  sends it and opens the next open test of the visit.
* The server checks numbers and options and sets the flag automatically: H, L, HH/LL (critical) or * (abnormal).
* A result that is already submitted, or was sent back, can only be changed by users with *Edit Unverified Results*.

## 7. Verification (*Verification*)

* The queue lists patient visits with results awaiting verification.
* The patient-based screen shows every result with flags, units, ranges and who entered it.
  * **Verify** – one test (*Verify Results*).
  * **Verify all completed results** – the whole visit at once (*Approve Final Reports*).
  * **Send back** with a reason – the test returns to the technician's pending list, showing the reason.
* The verifier's name and time are stored and printed (optional).

## 8. Results, previous results and corrections

* *Lab Results*: all visits for a period, or a search across all dates by visit number, Patient ID, name or phone. Each row
  shows the verification and report status.
* *Previous results* (patient history): every earlier visit with the values exactly as reported. A **results-over-time** table
  shows each parameter by date.
* **Withdraw result** (*Delete/Correct Results*, reason required) on the visit page: the old result (values, ranges, flags,
  who/when) is stored as a revision. The test goes back to *In progress* with the values pre-filled for correction. It must
  then be verified again. Revisions are shown on the visit page and cannot be deleted.

## 9. A4 laboratory report

*Report* opens the A4 report. It includes:

* the hospital/laboratory logo and branding;
* patient details (Patient ID, age/gender, referring doctor) and sample numbers;
* collected/reported/printed dates and each test grouped by category;
* parameters, results, H/L flags, units, reference ranges and comments;
* “verified by” and the signatory with an optional signature image, plus the footer.

* Tick the tests to print (**selected tests**). Only verified tests can be selected. *Preview selection* shows the page.
* **Print final report** records the copy and releases the tests. **Reprint** later needs *Reprint Laboratory Reports* and is
  counted as copy 2, 3 …
* **PDF**: choose *Save as PDF* in the print dialog.
* Sample: [docs/samples/sample-lab-report-A4.pdf](samples/sample-lab-report-A4.pdf) · order slip:
  [docs/samples/sample-lab-slip-80mm.pdf](samples/sample-lab-slip-80mm.pdf).

## 10. Lab settings

The laboratory name, hospital name, address, contacts and email override the hospital profile on reports. You can also set:

* the header line and footer, and the layout (standard/compact);
* the signatory name and designation, and the logo and signature images (PNG/JPEG ≤ 1 MB);
* whether to show “verified by”;
* **allow self-verification** and **release only fully paid tests**.

The **Inventory items** list (reagents, consumables, test kits, unit, supplier, reorder level, expiry tracking) is the
foundation for a later inventory phase. It does not track stock movements yet.

## 11. Dashboard and reports

* **Lab Dashboard**: Today / Yesterday / This week / This month / This year / Custom range. It shows counts of patients, tests,
  sample pending, collected, in progress, awaiting verification, verified, released, cancelled, rejected samples and urgent
  tests. Collection is shown only with financial permission. Three work lists (pending tests, verification, ready to print)
  always show open work, including work from earlier days.
* **Lab Reports**: daily, monthly and yearly activity. Breakdowns by test, category and referring doctor. Lists of cancelled
  tests, rejected samples and verification by user. Laboratory collection comes from the Phase 3 receipts (financial permission
  only). **Excel**, **CSV** and **Print/PDF** are available, and every export is audited.

## 12. Permissions (role editor → *Laboratory Management*)

| Permission | Allows |
|---|---|
| View Laboratory Dashboard | dashboard and work lists |
| Register Laboratory Patient | register new patients from the laboratory |
| Book Laboratory Tests | new lab visit, order slip |
| Edit Laboratory Orders / Cancel Laboratory Tests | add tests, change referral/notes / cancel a test with reason |
| View Laboratory Results | results list, visit page |
| Manage Test Catalogue / Test Parameters / Reference Ranges | catalogue, parameters, ranges |
| Collect Samples / Reject Samples | sample collection and receipt / rejection with reason |
| Enter Test Results / Edit Unverified Results | result entry / change submitted or sent-back results |
| Verify Results / Verify Own Results / Approve Final Reports | single verification / self-verification (if allowed by settings) / bulk visit verification |
| Print / Reprint Laboratory Reports / Export Report PDF | first print / later copies / PDF preview |
| View Previous Results | patient lab history |
| Delete/Correct Results | withdraw a verified result with reason (revision kept) |
| Laboratory Reports / Manage Laboratory Settings | statistics and exports / settings |
| View Laboratory Financial Information / Manage Laboratory Fees | prices, payment status, collection / set prices |

All permissions are checked on the server for every page and every action. Hiding a button is only a convenience.

## 13. Database

New tables: `LabCategories`, `LabSampleTypes`, `LabTests`, `LabTestParameters`, `LabReferenceRanges`, `LabOrders`,
`LabOrderItems`, `LabResultValues`, `LabSamples`, `LabItemEvents`, `LabResultRevisions`, `LabReportPrints`, `LabSettings`,
`LabInventoryItems`. Sequences `lab_order_number_seq`, `lab_sample_number_seq`. Phase 3 table `Charges` gained the nullable
column `LabOrderId`. Migration: `20261114013257_Phase4Laboratory`. It only adds tables and columns; existing data is untouched.
