# Update 10 – Fees & Services, simple laboratory, inpatient account, ward / NICU billing, discharge clearance

This update installs over the existing system with `Update-Server.ps1`. It does not rebuild anything and keeps all data. There are three migrations and all of them only add things: `Update10Fees`, `Update10Lab` and `Update10Ipd`. No table or column is dropped or changed.

There is still one billing system. Every fee, charge, payment, refund and advance goes through the Phase 3 records (charges, receipts, refunds, cashier ledger). The new tables only add detail, such as which billing day a charge is for, which equipment period, which package or which advance.

## 1A – Fees & Services (new main menu)
- **Categories.** The ten standard categories are OPD, Laboratory, Admission & IPD, Radiology, Emergency, OT, Minor Procedures, Ward & Patient Care, NICU Equipment and Other. There is also a Pharmacy category.
  - Each category comes with its common services, set to **fee 0** until the administrator sets the price.
  - Existing services are placed in the category that matches their type.
- **What each service holds:** code, category, department, charging method (per test / visit / admission / day / hour / session / procedure / item, or fixed), government / private applicability, effective date, status and description.
- **Separate government rate.** In Government mode the government rate is used wherever one is set.
- **Fee history.** Every fee change is kept. Old bills keep the fee that was charged at the time. Services are never deleted, only deactivated.
- **Pages:**
  - catalogue search (`/Fees`)
  - new / edit service (`/Fees/Service`)
  - categories (`/Fees/Categories`)
  - service-wise usage and revenue (`/Fees/Reports`)
- **One fee source.** Every module takes its fees from the same central function (`ServiceCatalogue.FeeOnAsync`).

## 1B – Simple laboratory workflow (default ON; Lab Settings → "Simple workflow")
1. **New Lab Visit:** choose the patient and the tests. Fees come from the catalogue and the total is calculated automatically.
2. **Payment:** "Payment received" records the payment in the same transaction as the booking, as a Phase 3 receipt.
   - The amount can be partial, where the tests allow it.
   - If the payment cannot be recorded, nothing is booked.
3. **Receipt:** prints in **two copies** (patient copy and laboratory copy) with the same receipt and visit numbers. Reprinting creates nothing.
4. **No sample step:** there is no sample collection screen, queue or button.
5. **Pending Lab Results:** search by name, Patient ID or visit number.
6. **Result entry:** dropdowns, units and ranges are shown and abnormal values are flagged.
7. **Save report = VERIFIED.** The person who saves is recorded as responsible. There is no verify button or queue, and incomplete reports are refused.
8. **Review report** (permission "Review / Edit Saved Results"):
   - correct a single test, with a reason
   - the earlier result is kept as a revision
   - the report stays VERIFIED and is marked **AMENDED**
9. **Separate permissions:** book, collect payments, print receipts, enter results, view, print, export PDF, review, and manage the catalogue. Printing never gives the right to edit.
10. **Inpatient tests** have no payment step at the lab. Their charges go to the patient's IPD account.

## 1C – Inpatient account (`/Ipd/Account/{id}`)
- **Charges by department:** admission fee, ward / bed, pharmacy, laboratory, radiology, OT, NICU equipment, packages and other services.
- **Each line shows** quantity, unit price, total, discount, paid amount, status and the user who posted it.
- **Also shown:** payments, refunds and advances, plus the bed-day, equipment and package history.
- **Pharmacy** charges only what is actually issued, with quantity and unit price. A repeated issue is never billed twice.
- **Patient statement:** `/Finance/Statement/{patientId}`.

## 1D – Admission fee and two-copy admission form
- **Configuration:** the admission fee is set in IPD Settings (the admission service) and shown when admitting the patient.
- **"Received now"** records the payment. **"Free admission"** needs the permission and a reason, and is audited. **"Not received"** leaves the fee on the patient's account.
- **Admission form:** prints a patient / attendant copy and a hospital copy, with the same admission number, fee and payment status.

## 1E – Ward, bed and NICU billing
- **Bed charging policy** (IPD Settings):
  - **Per night** – the earlier behaviour, and still the default
  - **Cutoff day** – a full day from the cutoff time (default 12:00)
  - **Half day** – short days are charged half (default under 6 hours)
  - **Hourly**
- **One charge per billing day.** This is guaranteed by the database. After a transfer the day goes to the ward used most that day, so periods never overlap.
- **NICU equipment** (`/Ipd/Equipment/{id}`):
  - start, stop and replace or transfer, each as a separate period
  - the rate and unit come from Fees & Services
  - charged when the equipment is stopped; per hour charges started hours, per day charges started days
  - one running period per equipment type and patient, and one patient per device at a time
  - a second stop is refused, so nothing is charged twice
  - running equipment stops automatically at the final bill or at discharge
- **Packages** (`/Ipd/Packages`):
  - the package price is a service, with a list of included services and a list of what is excluded
  - included services are shown on the bill at 0 ("Included in package …")
  - a package cannot be applied twice

## 1F – Advances, payments and discharge clearance (`/Ipd/Clearance/{id}`)
- **Advance deposits:**
  - recorded in the cashier's cash in hand when received
  - adjusted against the bill as a receipt with the system method ADVANCE, which is not counted as cash a second time
  - the unused part can be returned to the patient (needs a reason and the refund permission)
- **Clearance screen:**
  - shows the department totals, advances, payments, discounts, refunds and the remaining balance
  - **Prepare final bill** posts bed charges up to now and stops running equipment
  - **Receive payment** accepts partial payments; a double submit creates one receipt
- **Discharge policy:** discharge needs a zero balance. This is now ON for every hospital and can be switched off in IPD Settings.
  - The exception is an **override** by a user with "Override Financial Clearance". The override needs a reason and is written to the audit log.
  - Clinical care is never blocked by this rule.
- **Discharge slip:** a fully paid patient's slip shows no outstanding-payment warning.

## 1G – Reports and permissions
- **`/Finance/Revenue`** shows:
  - daily collections
  - revenue by source and by department
  - government vs private services
  - cashier-wise collections
  - patient-wise outstanding balances
  - admission-wise final bills
  - advances, refunds, discounts and free / waived services
- **Filters:** dates, department, user, service and payment status.
- **Reconciliation:** department-wise and cashier-wise collections are calculated from the same receipts, and the page checks that they agree.
- **New permissions:**
  - Fees & Services (6)
  - Laboratory: collect payments, print receipts, review results
  - IPD: ledger, statements, billing history, post charges, admission fee, admission payment, ward charges, NICU equipment, packages
  - Finance: inpatient payments, advances, view clearance, override clearance
- **How existing staff get them:** each new permission is given once to staff who already did that task. **"Override Financial Clearance" is never given automatically.**
- **Server checks:** every permission is checked on the server, not just by hiding buttons.

## After installing
1. In Fees & Services, set the fees (new services start at 0), including the admission fee, ward services, NICU equipment and laboratory tests.
2. In IPD Settings, choose the admission service and the bed charging policy, and check the discharge-clearance rule.
3. Assign cashiers, laboratory reception and admission staff to a cash counter (Finance → Counters). Without a counter they cannot record payments.
4. Give "Override Financial Clearance" only to the people who should have it.
