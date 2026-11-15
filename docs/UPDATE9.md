# Update 9 – fee at booking, Free by Doctor, lab catalogue, nursing medicine demand

This update is installed over an existing system with `Update-Server.ps1`. It does not rebuild anything and keeps all data.
Two migrations add tables and columns only: `Update9Fees` and `Update9LabNursing`. Neither migration drops, renames or alters anything.

## 1. Signatures
- Patients and attendants no longer sign any printed form: admission, consent, discharge, NICU discharge or OT consent.
- The discharge slip has one signature line, "Doctor's signature".
- The doctor's printed name above that line is optional. Use the toolbar toggle "Hide doctor's name" (`?name=0`). The name stays in the database and in the audit trail.

## 2. OPD token = fee receipt
- **Booking flow:** register or search the patient → book → collect the fee → print the token.
- **Two buttons:**
  - "Fee received – save & print token" records the payment. It creates the Phase 3 charge and receipt in the same transaction as the appointment.
  - "Free by Doctor" needs the permission, a reason and the authorising doctor.
- **Doctors without a fee:** the appointment is saved as FREE.
- **Nothing is recorded as paid unless the money was received.**
- **The token slip** shows the fee, PAID / FREE / FREE (by doctor), the payment method and the receipt number.
- **No duplicates:**
  - Reprinting creates no transaction.
  - A double submit creates one appointment and one payment.
  - The cashier cannot collect the same consultation fee again.
- **"Mark as Seen by Doctor" is removed.** Old "Seen" appointments show as Booked, and no data is lost.

## 3. Free by Doctor (permission `Appointments.FreeByDoctor`)
- **Before payment:** the appointment is marked FREE (by doctor). No transaction is created.
- **After payment:**
  - A `FeeWaiver` entry reduces the doctor's share in the doctor account.
  - Tick "money returned" to also create a cash refund request. Its approval does not reduce the doctor's share a second time.
  - A second waiver of the same appointment is refused.
  - The receipt of a waived fee cannot be cancelled.
- **Every waiver records:** the user, the time, the original fee, the reason and the authorising doctor. All of it is audited.
- **Reports** (Appointments → Reports) show paid, free and free-by-doctor appointments, waivers by doctor and by user, and totals.
- **Doctor accounts** show fee waivers and earnings after waivers.

## 4. Laboratory
- **Standard catalogue:**
  - 27 disciplines and about 150 tests, with parameters, units and reference ranges by age, sex and pregnancy, plus qualitative normals.
  - It is added once (`LabSettings.StandardCatalogueVersion = 1`).
  - Existing tests, matched by code, name or earlier starter code, are never changed.
  - New tests are **active with price 0** and marked **Review**.
- **What the laboratory must do:**
  - Check the ranges against its own method or analyser and set the price.
  - Then tick "Ranges, units and price checked" on the test to remove the mark.
- **Result entry:**
  - Dropdowns for Positive/Negative, Reactive, Detected, Present/Absent, Normal/Abnormal and blood group.
  - Numeric fields show the unit and range.
  - Abnormal values are flagged.
- **Culture tests:** enter the organism (or "No growth") and the antibiotic sensitivity table (S / I / R, MIC or zone). These are printed on the report.
- **Pregnancy:** tick "Patient is pregnant" on lab booking. A patient with an active pregnancy record is treated as pregnant even if the box is not ticked. Pregnancy ranges are then used.
- **Range editor:** has a "Pregnancy" option.
- **Old reports keep their own ranges**, because ranges are snapshotted when the test is booked.

## 5. Nursing medicine demand
- **Permissions:**
  - `Ipd.RequestMedicines` (nurses). Granted once to users who could record medication.
  - `Pharmacy.ProcessWardRequests` (pharmacy). Granted once to users who could issue to patients.
- **Requesting (nurse):** Admission → "Request medicines". Choose the medicines and quantities, and mark the request Routine or Urgent.
  - Only for active admissions in the nurse's own ward.
  - The same medicine cannot be requested twice while an earlier request is still open.
- **Processing (pharmacy):** Pharmacy → Ward requests. For each medicine: approve, reject, mark not available, or issue fully or partly.
- **Stock** is deducted **only when issued**, through the normal patient issue linked to the admission. Batches are chosen first-expiry-first-out, and Private-mode charges are created.
- **Double submits** never deduct stock twice.
- **History** of every request is kept. It cannot be edited or deleted (database triggers).

## 6. Things to check after installing
- Receptionists who collect consultation fees must be **assigned to a cash counter** (Finance → Counters).
- Review **"Mark as Free by Doctor"** grants: no new permissions are given to receptionists automatically.
- Set **lab prices** and review the ranges of the tests you use (Laboratory → Catalogue, "Review" mark).
