# Update 11 – workflow improvements

All changes modify the existing system; no module was rebuilt and no data is deleted. One additive migration (`Update11`).

## 1. Laboratory – New Lab Visit
- No category tree. The search bar is followed by the **frequently used tests** (top tests of the last 90 days, filled up from CBC, RBS, FBS, UCE, LFT, RFT, HBsAg, Anti-HCV, MP, Widal, ESR, Lipid profile).
- Any active test is found by name or code; a selected test stays visible in the "Selected" line.
- Every test appears once on the page; the server refuses a test selected twice.
- Categories remain in the catalogue / settings, and printed reports still group by category.

## 2. Patient search in routine boxes
- OPD, Appointment, Laboratory, Radiology, Pharmacy issue and Emergency boxes list only the patients of the current hospital date (registered or with a visit today).
- Night rule: from 00:00 to 02:00 (hospital time zone, Asia/Karachi), patients registered from 22:00 the evening before are still suggested – one continuous window over midnight.
- Admitted patients are always listed.
- Older patients are opened from **Patients** (profile → new visit). Nothing in the data changes; this is visibility only.

## 3. Admission form
- Separate **date** and **time** fields side by side, defaulting to the current hospital time; the date picker closes when a date is picked.
- Validation: not in the future (5-minute tolerance), not more than 7 days back.
- The exact value is saved; the admission audit line records the time and, when it was entered by hand, the user.

## 4. Beds
- Discharge, transfer and NICU return release the bed straight to **Available (vacant)** – visible at once in the bed list, ward dashboard and bed map. The status change is logged.
- An occupied bed is never offered and the allocation is refused on the server (row lock + unique open-bed index).
- Bed selection: **Ward → rooms of that ward → vacant beds of that room**. The bed-type filter ("All bed types" / "Standard") is removed from the screens; the bed type stays in the database.

## 5. Pharmacy – Ward Requests alert
- The Ward Requests link (sidebar, admin bar, pharmacy tabs) turns **red with the pending count** while any request is Pending / Approved / Partly issued. Viewing does not clear it; it clears only when all are issued, rejected or cancelled.
- The count refreshes every 20 seconds without reloading the page (`/api/alerts`, permission-filtered).

## 6. IPD medicine issuance – payment
- Issuance stays linked to the admission and its account (medicine, quantity, price, total, date/time, user) and deducts stock once (idempotent).
- At issue (ward request or patient issue for an admission) the pharmacist chooses:
  - **No payment now** – the amount stays outstanding on the IPD account and the discharge statement.
  - **Pay now** – a Phase 3 receipt against exactly that issuance's charges; partial amounts allowed.
- An unpaid issuance is never marked paid. Permission: *Finance → Receive Inpatient Payments* (Private mode).

## 7. Admitted patients count
- Admin dashboard and IPD dashboard show the admitted count; clicking opens the list (name, ID, admission number, ward, room, bed, date/time, doctor, status), filtered by ward permission (admin sees all). Each row opens the admission (normal permissions).
- Ward rows on the IPD dashboard open the list of that ward.

## 8. Doctor account
- From *Doctor-wise Lists* each doctor has a **Doctor account** link.
- Account page: today's (or any day's) OPD list – token, patient ID, name, fee, paid, payment status – with booked, collected, outstanding, doctor's share, handed over and payable remaining.
- **Pay / hand over**: amount, method, reference, note, date; *Hand to doctor* (voucher `DPY-…`) or *Hand to Admin* (voucher `DHA-…`, separate entry type, "received by"). No admin approval step. Both reduce the payable; whatever is not handed over is carried to the next day.
- Collected / doctor's share / handed to doctor / handed to Admin / payable are separate figures; the payable is the ledger sum (no double counting); each transaction has its own number and audit line.
- Prints: daily OPD list, settlement report (period) and handover voucher.
- A doctor user with *View Own Doctor Account* (linked to the doctor record) sees only his own account and prints; he cannot hand over or change the arrangement.

## 9. Supplier invoice (Pharmacy → Receive stock)
- Header: supplier, invoice no., invoice date, receiving date, notes.
- Continuous rows: medicine search adds a row; purchase and sale price are filled from the medicine (editable; the sale price only for *Manage Medicine Prices*); batch number optional (without one the line is kept as the batch of its expiry date); remove a row with ✕; "Add another item".
- Item count, total quantity and purchase total update live.
- One transaction – all lines or none; double submit returns the saved invoice; the same supplier invoice number (any letter case) is refused.
- History: *Invoice history* → Documents → Stock receiving (view / print). Corrections only through an authorised stock adjustment with a reason.

## 10. Permissions (new)
| Permission | Granted once to holders of |
|---|---|
| Finance.ViewOwnDoctorAccount | – (assign to doctor users) |
| Finance.HandDoctorMoneyToAdmin | Finance.RecordDoctorPayments |
| Finance.PrintDoctorReports | Finance.ManageDoctorAccounts |

Existing permissions used: Pharmacy.ProcessWardRequests (alert), Finance.ReceiveInpatientPayments (IPD medicine pay now), Ipd.ViewPatients / Ipd.ViewAllWards (admitted list), Pharmacy.ReceiveStock / ManagePurchases / ManagePrices (invoice).

## 11. Database
Migration `Update11` (additive): `DoctorSettlements.Kind` (text, default `ToDoctor`) and `DoctorSettlements.ReceivedBy`. New enum value `DoctorEntryType.HandedToAdmin` (stored as text – no schema change). Money stays `numeric`; backups made before Update 11 restore normally (the migration runs on start).

## 12. Audit actions (new)
`Ipd.MedicineIssuePaid`, `Finance.DoctorHandedToAdmin`; admission audit text now includes the admission time and who entered it.
