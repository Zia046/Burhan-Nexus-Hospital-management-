# Phase 5 – Pharmacy management & medicine inventory

This guide describes the pharmacy module of Burhan Nexus Hospital Management. It covers:

- the medicine catalogue and suppliers;
- stock receiving and purchases;
- batches and expiry;
- patient issue and private retail sale;
- transfers to Minor OT and other departments;
- adjustments and returns;
- reports and permissions.

The module follows the hospital type chosen at setup (**Government** or **Private**). It uses the Phase 3 cashier for all money.
It does not change OPD, Emergency, appointments, laboratory, receipts or printing.

Menu **Pharmacy** has these screens: Dashboard · Patient Issue · Retail Sale (Private only) · Receive Stock · Stock Transfer · Stock & Batches · Medicines.
The pharmacy tab bar also has Adjustments, Documents, Suppliers, Reports and Settings.

## 1. Government and Private mode

| | Government hospital | Private hospital |
|---|---|---|
| Prices | **No** purchase price, sale price, profit or retail price anywhere – not on screens, slips or reports | Purchase price, sale price (per medicine and per batch), discounts, profit, stock value |
| Issue to patients | **Only** registered patients against an OPD / Emergency visit of the last *N* days (setting, default 7) | Registered patients, with or without a visit. Each line becomes a Phase 3 charge, paid at the cash counter |
| Walk-in sales | Not available (screen, server and database refuse it) | Retail sale without Patient ID, paid immediately on a Phase 3 receipt |
| Receiving | Source / supplier, batch, expiry, quantity, reference | Plus supplier invoice, cost, discount, additional charges, supplier account |
| Optional fee | A fixed Phase 3 service fee per issue (e.g. pharmacy slip fee) can be configured – no medicine prices are shown | – |

How the separation is enforced:

- The mode is read on the server for every request; page controls are only a convenience.
- Prices sent by a browser in Government mode are refused or ignored.
- Database triggers refuse retail sales and supplier accounts in Government mode.
- Database check constraints stop a Government receiving or issue from storing amounts.
- A Government issue must have a visit.
- Changing the hospital type still needs *Change Hospital Type* plus the administrator password, and is audited (Phase 1). Records created in Government mode never contain prices, so switching modes reveals nothing hidden.

## 2. Medicine catalogue (*Medicines*)

- A medicine has: Medicine ID (`MED-000001`), name, generic name, brand, manufacturer, category, dosage form, strength, pack size, stock unit, units per pack, minimum stock (low-stock alert), storage instructions and active/inactive.
- Private only: purchase price, sale price, whether discounts are allowed and the maximum discount %. Changing these needs *Manage Medicine Prices*, and every change is kept in the price history.
- **Duplicate warning:** if the name, generic name or brand is the same or similar, with the same strength and form, the screen lists the existing medicines. You can open one of them, or tick *Save anyway*. Records are never merged automatically.
- The medicine page shows the complete history: every batch at every location, every stock movement and every price change.
- *Categories & lists* manages categories, manufacturers, dosage forms, and suggestion lists for strengths, pack sizes and units.

## 3. Suppliers

You can add, edit, deactivate and search suppliers (name, contact person, phone, address, city, email, notes). Each supplier page shows its receiving history.

Private mode adds a supplier **account**:

- purchases increase the balance;
- payments and returns reduce it;
- *Record payment* accepts any payment method, but never more than the balance.

Supplier payments are paid by the hospital, like doctor settlements. They are not taken from a cashier's drawer, so no new cashier ledger is created.

## 4. Stock receiving / purchase (*Receive Stock*)

1. Select the supplier, or type the source (e.g. district health office or a donation). Add the reference / invoice number.
2. Search a medicine and add a line. Enter the batch number, expiry (month), manufacturing month and quantity in stock units.
3. Private, with *Manage Purchases*: also enter the cost per unit, line discount, invoice discount and additional charges. With *Manage Medicine Prices* you can also enter a batch sale price.
4. Save. This writes a permanent receiving note (`GRN-2026-000001`) and one inventory transaction per line. Stock is never increased without them.

Rules:

- Expired stock cannot be received.
- The same batch number of a medicine must keep the same expiry date.
- The same supplier invoice cannot be entered twice.
- A double click saves only once.

## 5. Batches, expiry and FEFO

- Stock is kept per **location and batch**. One medicine can have many batches with different expiry dates and quantities.
- **FEFO**: when no batch is chosen, the batch that expires first is used. If one batch is not enough, the quantity is split over several batches and each part is recorded separately.
- Choosing a batch other than the earliest-expiring one needs *Select Another Batch (FEFO override)*. It is marked on the document and recorded in the audit log.
- **Expired batches** (expiry date before today) are never issued, sold or transferred, and are not counted as available.
- **Near expiry** means within the configurable warning period (default 90 days).
- The dashboard, the Stock & Batches page and the expiry reports show both near-expiry and expired batches. Expired stock is removed with an *Expired* stock adjustment, or by returning it to the supplier.

## 6. Patient medicine issue (*Patient Issue*)

1. Search the patient by Patient ID, name, phone or CNIC. You can also use *Issue medicines* on the patient profile.
2. Choose the OPD / Emergency visit. This is required in a Government hospital.
3. Search the medicines. The FEFO batch is suggested and the available quantity is shown. Enter the quantity and the dosage / instructions (e.g. as written on the doctor's slip).
4. Save. Stock is deducted, the issue document (`ISS-2026-000001`) and an issue slip (80 / 58 mm) are produced, and the pharmacist and time are recorded.

Rules:

- No more than the available, non-expired stock can be issued.
- A repeated submission never deducts twice.
- Doctors do not need to enter prescriptions in the software; the handwritten workflow stays.
- Private: each medicine line is a Phase 3 charge. *Collect payment* opens the cash counter with the balance already filled in.
- The patient profile shows the last medicines issued, for users with *View Patient Medicine History*. *Pharmacy → Medicine history* shows them all.

## 7. Retail sale (Private hospital only)

1. Optionally enter the customer's name and phone. No Patient ID is needed.
2. Search medicines, choose the quantity and batch, and see the configured price, line totals and net total. Discounts need *Apply Discount*, must stay within the medicine's maximum % and need a reason.
3. Choose the counter and payment method, then click *Complete sale & receipt*.

The stock deduction and the Phase 3 receipt (receipt number, cashier ledger, counter) are saved in **one transaction**. If the payment fails, no stock is deducted. A double click charges once.

The pharmacy receipt shows:

- hospital / pharmacy name, receipt number, date and time;
- each medicine with quantity, unit price and discount;
- the total, the payment method and the cashier.

Expired medicines, negative stock, unauthorised prices and unauthorised discounts are all refused.

## 8. Transfers to Minor OT and other departments (*Stock Transfer*)

Minor OT staff do not use the software. The pharmacy hands over medicines and supplies and records the transfer:

- Choose the destination (Minor OT, or any other department store the administrator created).
- Search the medicines and batches, enter the quantities and a reference / requisition number, then save.
- The main pharmacy stock decreases and the department stock increases **exactly once** (two linked transactions, one transfer number, the user and the time).
- A transfer slip can be printed for the receiving staff to sign.

Department stock is **not** reduced per patient. Patient issues always come from the main pharmacy. Stock can be moved back from a department to the pharmacy with a transfer in the other direction. Physical differences in a department are corrected with an adjustment.

*Pharmacy Settings → Stock locations* (*Manage Department Stock Locations*) lets you add departments that keep stock (Emergency, Dressing Room, Ward …). A location can only be deactivated when it holds no stock. Users with *View Department Stock* see department balances but not the main pharmacy.

## 9. Stock adjustments

An adjustment needs:

- the location and batch;
- increase or decrease, and the quantity;
- a reason: physical stock difference, damaged, broken packaging, expired, lost, data-entry correction or other;
- a **mandatory explanation**.

A user with only *Adjust Stock* creates a request. The stock changes only when a user with *Approve Stock Adjustments* approves it (a rejection needs a note). A request by a user who may approve is applied immediately and recorded as approved by that user.

The record keeps the quantity before and after, and who requested and decided. Stock is never overwritten silently.

## 10. Returns

- **Customer return (Private):** against the original sale. Full or partial returns, never more than was sold. *Undamaged – back to stock* puts the medicine back into its batch. Damaged, opened or expired medicines are recorded only. The money is requested as a Phase 3 refund on the original receipt and paid only after approval in *Finance → Refunds*, so pharmacy staff cannot bypass the administrator.
- **Patient return:** against an issue, in both modes. Private: an unpaid charge is cancelled and the kept medicines are billed again; for a paid charge a refund is requested.
- **Supplier return:** batches from the main pharmacy go back to the supplier or source with a reason and the invoice reference. Private: the supplier account is credited at the batch cost.

## 11. Dashboard and reports

The dashboard shows:

- active medicines, units in stock, low-stock medicines, near-expiry and expired batches;
- patient issues, retail sales, receipts and transfers;
- adjustments waiting for approval, plus the low-stock and expiry lists and recent documents.

Private, with *View Pharmacy Financial Reports*: sales amount, purchases, estimated gross profit and stock value. Every figure comes from the inventory transactions and documents.

Reports have filters for period, location, category, medicine, batch and supplier, and can be exported to Excel or CSV or printed / saved as PDF (each export is audited):

- **Inventory:** current stock, batch-wise stock, low stock, near expiry, expired, receiving history, issuance history, transfers, adjustments, supplier returns, physical stock difference.
- **Patients:** patient-wise, OPD, Emergency, date-wise and medicine-wise issuance.
- **Departments:** department stock (incl. Minor OT), pharmacy → Minor OT transfers, department-wise transfers.
- **Financial (Private only):** daily and monthly sales, medicine-wise sales, purchases, supplier balances, sales returns, purchase returns, gross profit and margin, stock valuation.

## 12. Permissions (role editor → *Pharmacy Management*)

| Permission | Allows |
|---|---|
| View Pharmacy Dashboard | dashboard and alerts |
| View Medicine Catalogue / Add Medicine / Edit Medicine | catalogue |
| Manage Medicine Categories | categories, manufacturers, forms, strengths, pack sizes, units |
| Manage Suppliers | suppliers |
| Receive Stock | stock receiving |
| Manage Purchases | Private: purchase amounts, supplier accounts and payments |
| Issue Medicine to Patients | patient issue |
| Manage Retail Sales | Private: walk-in sales |
| Apply Discount | Private: discounts within the medicine limit |
| Select Another Batch (FEFO override) | choose a later-expiring batch |
| Transfer Stock | transfers |
| View Department Stock / Manage Department Stock Locations | department balances / configure departments |
| Adjust Stock / Approve Stock Adjustments | request / approve corrections |
| Process Returns | customer, patient and supplier returns |
| View Stock / Expiry / Low-Stock Reports | inventory reports |
| View Patient Medicine History | patient history and patient reports |
| View Pharmacy Financial Reports | Private: prices, profit and financial reports |
| Manage Medicine Prices | Private: change purchase and sale prices |
| Manage Pharmacy Settings | settings |

Permissions are checked on the server for every page and action. No user can delete inventory history.

## 13. Database

**New tables:**

- Catalogue: `PharmacyLookups`, `Medicines`, `MedicinePriceChanges`, `Suppliers`.
- Stock: `StockLocations`, `MedicineBatches`, `StockBalances`, `InventoryTransactions`.
- Documents: `StockReceipts` / `StockReceiptItems`, `PatientMedicineIssues` / `PatientMedicineIssueItems`, `PharmacySales` / `PharmacySaleItems`, `StockTransfers` / `StockTransferItems`, `StockAdjustments`, `MedicineReturns` / `MedicineReturnItems`, `SupplierReturns` / `SupplierReturnItems`.
- Supplier account and settings: `SupplierLedger`, `SupplierPayments`, `PharmacySettings`.

**Changes to Phase 3:** `Charges` gained `PharmacySaleId` and `PharmacyIssueId`, and the service kind *Pharmacy* was added. The Phase 3 service `PHM-MED` carries medicine charges.

**Migration:** `20261115085909_Phase5Pharmacy` only adds tables and columns.

**Protection:**

- A balance is changed only together with an inventory transaction, in one atomic SQL statement.
- `CHECK (Quantity >= 0)` prevents negative stock, even when two counters issue at the same moment.
- Inventory transactions, receiving notes, transfers, returns, the supplier ledger, supplier payments and price history are insert-only.
- Medicines, batches, balances, issues, sales and adjustments cannot be deleted.
- Retail sales and supplier accounts are refused in Government mode.
