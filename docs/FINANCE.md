# Phase 3 – Financial, cashier & receipt management

This guide describes the financial module of Burhan Nexus Hospital Management: counters, cashier accounts,
receipts, deposits, refunds, doctor consultation accounts, daily closing and reports.
It works in both Government and Private hospital mode and does not change the existing OPD, Emergency,
appointment, doctor-wise token or thermal-slip workflows.

## 1. Main rules

| Rule | How it is enforced |
|---|---|
| Every payment gets a unique receipt number (`RCP-2026-000123`) that is never reused | PostgreSQL sequence + unique index `UX_Receipts_Number` |
| A double-click or repeated request never charges twice | every payment form carries a one-time key; unique index `UX_Receipts_Idempotency`; the repeat returns the first receipt |
| Two counters cannot over-collect the same outstanding balance | the charge rows are locked (`SELECT … FOR UPDATE`) inside the payment transaction |
| Money is never stored as floating point | `numeric(14,2)` / `decimal` everywhere |
| Financial history is never deleted or silently changed | database triggers: ledgers, settlements, adjustments, closings, print history and fee history are insert-only; receipts, charges, refunds and deposits can never be deleted; the amounts/number of an issued receipt or charge can never be changed |
| Corrections are new records | cancellation, refund, deposit, settlement and adjustment rows reference the original |
| Every sensitive action is audited | the audit log is immutable (Phase 1) |
| Dates follow the hospital time zone | business date of every transaction = hospital local date |

## 2. Counters

*Finance → Counters* (permission **Manage Counters**): create / rename / activate / deactivate counters, assign one or more
employees, **transfer** an employee (ends the old assignment, keeps history). The page also shows counter-wise collections,
outstanding cash and receipts (permission **View Counter Reports**).
A cashier can only collect at a counter he is currently assigned to (the system administrator may use any counter).
One counter, *Main Reception Counter*, is created automatically.

## 3. Services & fees

*Finance → Services & fees* (permission **Manage Service Fees**): add services (OPD registration, emergency registration,
consultation, laboratory, X-Ray, ultrasound, procedure, other), department, linked doctor, **free or paid**, fee,
**effective date** of a fee change (today or future – never back-dated), partial payment allowed, and which counters may collect it.

* Every fee change is stored in the fee history. Old receipts keep the fee that was charged.
* Free services produce a zero-amount receipt as proof of service (can be switched off in *Finance → Settings*).
* Existing OPD fee references from Phase 2 were taken over automatically with their current fee.

## 4. Collecting a payment

*Collect Payment* (permission **Collect Patient Payments**):

1. Find the patient (or *Walk-in* with the payer's name).
2. Add one or more services. The fee comes from the fee list; a discount needs the **Give Discounts** permission and a reason.
3. *Pay now* empty = full amount. A smaller amount is only allowed for services that allow partial payment; the remaining balance is
   listed under *Outstanding balances* for the next payment (each payment gets its own receipt).
4. Choose counter and payment method (Cash, Bank Transfer, Card or Admin-defined methods). Bank / card need a reference.
5. **Save & print receipt** – the receipt opens and prints.

From an OPD visit, emergency visit or private appointment, **Collect fee** opens this screen with the patient, visit and service filled in.
The appointment token and the thermal appointment slip are not touched.

## 5. Receipts and printing

Receipt content: hospital name, logo, address, contact, receipt number, patient name, Patient ID, age/gender, services, doctor,
fee, discount, net amount, paid amount, remaining amount, payment method, date and time, cashier, counter.

Formats: **58 mm and 80 mm thermal**, **A4**, **PDF** (browser *Save as PDF*). Thermal receipts use the same printer setup as the
appointment slips (*Settings → Thermal printers*: browser printing, LAN ESC/POS printer, or the Print Agent for USB printers).
First print needs **Issue (Print) Receipts**, every further copy needs **Reprint Receipts**; reprints keep the original number and are
listed in the print history.

## 6. Individual cashier accounts

Every receipt is credited to the cashier who issued it. *My Financial Account* shows only the user's own figures:
today's collection, collections for a period, deposits, outstanding balance, receipt history, payment-method breakdown and
cancelled receipts (if permitted). Nobody can open another person's account without **View All Financial Accounts**.

```
Outstanding (payable to Admin) = cash collections − cancelled cash receipts − cash refunds − deposits confirmed by Admin ± approved adjustments
```

Bank-transfer and card payments are recorded as collections of the cashier but go directly to the hospital's account, so they are not
part of the cash the cashier has to hand over (this is configurable per payment method: *Money stays with the cashier*).

## 7. Deposits (cash handed over to Admin)

1. Cashier → *My Financial Account* → **Deposit cash to Admin** (partial deposits allowed; at most the outstanding cash minus pending deposits).
2. The deposit gets a reference number (`DEP-2026-000001`) and stays **Pending** – the balance does not change yet.
3. Admin → *Cash Deposits* counts the cash, enters the **amount actually received** and remarks, and **confirms**, or **rejects** with a reason.
4. Only the confirmed (received) amount reduces the cashier's balance. A rejected deposit changes nothing. History is permanent.

Nobody can confirm or reject his own deposit – not even the administrator. Cashiers cannot reduce their own balance in any other way.

## 8. Receipt cancellation and refunds

* **Cancellation** (permission **Cancel Receipts**, reason required) is for a wrongly entered receipt. The receipt stays in the records
  marked *Cancelled*, its number is never reused, and reversal entries correct the cashier's and the doctor's accounts.
* **Refund** returns money to the patient and is a separate record (`RFD-…`). **Process Refunds** enters the refund (amount, method, reason);
  **Approve Refunds** completes it (a user with both may approve immediately). Full and partial refunds are supported; a refund can never
  exceed the paid amount minus earlier and pending refunds. The original receipt stays valid; the cashier's collection and the doctor's share are
  reduced proportionally. A receipt with a refund cannot be cancelled.

## 9. Doctor consultation accounts

*Finance → Doctor accounts* (permission **Manage Doctor Accounts**). Each doctor has a fee-sharing arrangement:

| Arrangement | Doctor's share of a consultation (net fee N) |
|---|---|
| Hospital keeps full fee (salaried) | 0 |
| A – full fee collected by hospital, paid to doctor | N |
| B – hospital commission p % | N × (100 − p) / 100 |
| C – doctor percentage p % | N × p / 100 |
| C – doctor fixed amount F | min(F, N) |
| C – hospital fixed amount H | max(N − H, 0) |

The arrangement in force when the fee is collected is stored with the charge, so a later change only affects new consultations.
The doctor earns the share of what is actually collected (partial payments earn proportionally, the last payment absorbs rounding).

The doctor account shows consultations, fees collected from patients, hospital share, doctor's share, amount paid to the doctor and the
remaining payable balance. **Record Doctor Payments**: full or partial payment, method, reference, notes and date; it can never exceed the payable
balance. Fees collected from patients and money paid to doctors are separate records.

## 10. Daily cash closing

*Finance → Daily closing*: cashiers see their own day (permission **Perform Daily Cash Closing**); Admin can also close any cashier,
any counter or the entire hospital. Figures: opening balance, cash and non-cash collections, receipts, cancelled receipts, refunds,
deposits submitted and confirmed, adjustments and closing outstanding balance – all computed from the transactions.
Saving creates a dated snapshot; nothing is reset or deleted. A discrepancy is corrected with an **adjustment** (*Finance → Adjustments*,
permission **Record Financial Adjustments**, reason required, never on one's own account).

## 11. Admin financial dashboard and reports

*Financial Dashboard* (permission **View Hospital Financial Dashboard**): today / yesterday / this week / month / year / custom range,
filters for department, counter, cashier, doctor, payment method and status; total collection, net after refunds, confirmed deposits,
outstanding cash with cashiers, valid and cancelled receipts, refunds, doctor consultation collections, pending doctor payments,
department-, counter-, user-, doctor-, method- and service-wise tables and a daily collection chart.
*Reports* lists every receipt line; **Excel (.xlsx)** and **CSV** export need **Export Financial Reports**; print / PDF through the browser.

## 12. Permissions (module "Financial Management")

View Own Financial Account · View Own Receipt History · Collect Patient Payments · Issue (Print) Receipts · Reprint Receipts ·
Give Discounts · View Cancelled Receipts · Submit Cash Deposit · Perform Daily Cash Closing · View Counter Reports · Manage Counters ·
Manage Service Fees · View All Financial Accounts · View Hospital Financial Dashboard · Confirm Cash Deposits · Reject Cash Deposits ·
Cancel Receipts · Process Refunds · Approve Refunds · Manage Doctor Accounts · Record Doctor Payments · Record Financial Adjustments ·
Export Financial Reports · Manage Financial Settings.

Menus and buttons are hidden without the permission **and** every page / action checks it on the server. Users can never change their own
permissions (Phase 1 rule).

A typical cashier responsibility: View Own Financial Account, View Own Receipt History, Collect Patient Payments, Issue Receipts,
Submit Cash Deposit, Perform Daily Cash Closing.

## 13. Database objects (migration `Phase3Finance`)

New tables: `Counters`, `CounterAssignments`, `PaymentMethods`, `ServiceFees`, `ServiceCounters`, `Charges`, `Receipts`, `ReceiptLines`,
`ReceiptPrints`, `Refunds`, `CashDeposits`, `CashierLedger`, `DoctorLedger`, `DoctorSettlements`, `FinancialAdjustments`,
`DailyCashClosings`, `FinanceSettings`. New sequences: `receipt_number_seq`, `charge_number_seq`, `refund_number_seq`,
`deposit_number_seq`, `settlement_number_seq`, `adjustment_number_seq`. New columns: `ServiceItems.Kind / DepartmentId / AllowPartialPayment /
Description`, `Doctors.ShareType / ShareValue`. The migration only adds objects; no existing data is changed or removed.

## 14. Known limitations

* The financial module covers fee collection; the full Laboratory, Radiology, Pharmacy, IPD and ward modules are later phases
  (only their services/fees can be billed now).
* PDF is produced with the browser's *Save as PDF*; Excel export is a plain workbook (no charts).
* Non-cash methods (bank / card) are trusted as entered with their reference; there is no bank reconciliation.
* Physical thermal printers: see `PRINTING-THERMAL.md` – tested through automated ESC/POS and Windows-spooler tests, not on physical hardware.
