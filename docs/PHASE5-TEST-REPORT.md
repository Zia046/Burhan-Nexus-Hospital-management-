# Phase 5 – testing report (Pharmacy & medicine inventory)

**Result:** 66 / 66 automated tests passed on a real PostgreSQL 16 database (GitHub Actions). The total includes all Phase 1–4 tests, so earlier phases are not affected. The browser walkthrough completed with no errors.

## 1. Automated tests (`tests/…/PharmacyTests.cs`)

| Test | What it proves |
|---|---|
| Government pharmacy hides prices and issues only against registered visits | Price fields are absent from screens and refused when posted. Issuing without a visit, or with an old one, is refused. Retail sale is 404 and blocked by a database trigger. Supplier payments are blocked. The issue appears on the patient profile. |
| Expired stock is never issued and FEFO override needs permission | Expired batches are not available. FEFO picks the earliest expiry and splits across batches. Choosing another batch needs *Select Another Batch* and is audited. Receiving expired stock is refused. |
| Private purchase, retail sale, receipt, returns and supplier account | Purchase with cost and invoice discount. The supplier balance increases. A sale with an authorised discount produces a Phase 3 receipt and stock moves in one transaction. A double submit charges once. A partial return creates a refund request, and stock comes back only for undamaged items. Supplier returns and payments reduce the balance; overpayment is refused. |
| Private patient issue is charged at the cash counter | Each line is a Phase 3 charge, and the cash counter opens with the balance filled in. A return before payment cancels the charge and re-bills the kept quantity. |
| Minor OT transfer moves stock exactly once | Main stock decreases and OT stock increases once, even with a repeated submit. No patient deduction is made from OT. Transferring more than is available is refused. |
| Stock adjustments need a reason and approval | The reason and note are mandatory. A request waits for approval. Approve and reject (with a note) both work. A database update or delete on inventory history fails. Negative stock is impossible (check constraint). |
| Permissions, duplicate warning and price control | Users without a permission are refused on the server, not only hidden in the UI. A similar medicine produces the warning, and nothing is merged. Prices change only with *Manage Medicine Prices*, and the history is kept. |
| Pharmacy reports open; previous phases still work | All 28 reports open and export. Financial reports are Private-only. OPD, Emergency, cashier and laboratory flows still work. |

The existing catalogue test now checks that the Pharmacy module has its 25 permissions.

## 2. Browser walkthrough (`tools/ui-check/phase2.mjs`)

The walkthrough first runs in **Government** mode:

- catalogue, duplicate warning, receiving and receiving note;
- patient issue against an OPD visit, and the issue document;
- transfer to Minor OT, batch stock, medicine history, adjustments;
- Minor OT stock report and dashboard.

It then switches to **Private** mode:

- purchase with prices, and a retail sale with a discount;
- sale document, gross-profit report and dashboard with money figures.

Screenshots `p5-01 … p5-17` are on the `ci-results` branch. Printed samples are in `docs/samples/`:

- `sample-pharmacy-issue-slip-80mm.pdf`
- `sample-pharmacy-transfer-slip-80mm.pdf`
- `sample-pharmacy-sale-receipt-80mm.pdf`

## 3. Security and integrity checks

- Every pharmacy page and handler checks permissions on the server.
- Government mode is enforced in three places: in services, by CHECK constraints and by triggers.
- Stock changes only through an atomic `UPDATE … WHERE Quantity + change >= 0`, together with an inventory transaction in the same database transaction. `CHECK (Quantity >= 0)` is the final guard.
- Idempotency keys with unique indexes on every document stop double posting.
- Inventory history, receiving notes, transfers, returns, supplier ledger and payments, and price history are immutable (database triggers). Medicines, batches, issues, sales and adjustments cannot be deleted.
- Money is recorded only through Phase 3 (charges, receipts, refunds with Admin approval). There is no separate cashier ledger.
- All actions are audited, including duplicate blocks, FEFO overrides, exports and slip prints.

## 4. Known limitations

- Pharmacy mode follows the hospital type. The legacy setup flag `PharmacyPricingEnabled` is not used by Phase 5.
- Department stock (e.g. Minor OT) is not consumed per patient. This is by design, because OT staff do not use the software. Differences are corrected with adjustments.
- Supplier payments are recorded as hospital payments, like doctor settlements, and not from a cashier drawer.
- Barcode scanning and purchase orders are not part of this phase.
- The rest of the Phase 5 prompt (from section 17) was not received. Any extra items from it will be added after review.
