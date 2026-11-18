# Update 12 – invoice-based bulk stock receiving

**Pharmacy → Supplier invoice** (`/Pharmacy/Receive`) is the only stock receiving screen. The old layout, where you searched above the table and added medicines one by one, is gone.

- **Header, entered once:** supplier (with contact details), invoice number, invoice date and received date. A source name such as "donation" is used only when there is no supplier record. Notes are optional.
- **One continuous table, one row per medicine:**
  - The medicine search sits inside the row: type 2 letters, use ↑/↓ to choose and Enter to pick.
  - Each row has quantity, expiry (month), an optional batch, purchase price and sale price, an optional adjustment and the line total.
- **Automatic prices:** picking a medicine fills the purchase and sale price from its record. Users with **Manage Medicine Purchases** can edit the purchase price, and only users with **Manage Medicine Prices** can edit the sale price; other users see it read-only. An unchanged sale price is not treated as a price change.
- **Keyboard:**
  - Enter moves to the next box in the row.
  - Enter in the last box of a row opens a new row.
  - Alt+N adds a row.
  - ✕ removes a row.
- **Missing medicines:** "New medicine" creates a catalogue record from inside the invoice, without leaving the screen, and adds it as a row. This needs **Add Medicine**.
- **Live totals:** medicines / items, total quantity, purchase total and invoice net (after the invoice discount and freight).
- **Save & receive complete invoice:** one transaction for the header, every row, the batches, the stock balances and the stock history. It saves all rows or none. Untouched empty rows are ignored. A row with entries but no medicine, or with a medicine but no quantity, stops the save and names the row. Clicking Save twice never adds stock twice, and the same supplier invoice number (in any letter case) is refused. One invoice can hold up to 500 rows.
- **Invoice history** (`/Pharmacy/Invoices`):
  - Search by invoice number, supplier or invoice date range.
  - Open the complete invoice to see all rows with batch, expiry, quantity, prices and totals.
  - Print it on A4 (`/Print/SupplierInvoice/{id}`).
  - Saved invoices are read-only. Corrections are made only through an authorised stock adjustment with a reason, which is audited.

There are no database changes. The existing `StockReceipt` / `StockReceiptItem` tables already hold one invoice header with its rows.

Acceptance:
- **Browser walkthrough:** 22 catalogue medicines plus 1 created from inside the invoice, entered under invoice 12322 for ABC Medical Supplier. Prices were checked automatically on every row. The invoice was saved once, giving 23 items and 300 units on one record, which appears in invoice history.
- **Automated test** `Update12_acceptance_supplier_invoice_12322_with_23_medicines_is_received_in_one_submission` checks:
  - 1 receipt with 23 items and 300 units
  - the stock of every medicine
  - sale prices kept
  - a second Save adds no stock
  - the invoice is found in history by number, supplier and date
  - the opened invoice and its A4 print show all 23 medicines
