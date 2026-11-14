# Phase 4 – testing report (Laboratory Management System)

All tests run automatically in GitHub Actions on every push. Each run uses PostgreSQL 16 and the real ASP.NET Core application
in memory, with a fresh database per test. It also includes a browser walk-through of the published application
(Playwright/Chromium) and a Windows Server 2022 installation test.

Result for the Phase 4 code: **58 / 58 tests passed**. That is 50 Phase 1–3 tests and 8 new laboratory tests. The Windows
installation and Print Agent test also passed.

## Functional tests (prompt section 17)

| # | Requirement | Automated test | Result |
|---|---|---|---|
| 1 | Register/search patient → book tests (external referral) → order number, order slip, one Phase 3 charge per test; duplicate submission with the same key returns the same order | `Complete_workflow_from_booking_to_released_report` | Passed |
| 2 | Sample collection, fast entry with predefined parameters/units/ranges, automatic High flag, the test leaves the pending list and appears in the verification queue | same test | Passed |
| 3 | No final report before verification; verify; A4 report with values, comment, “verified by”; print releases the test; reprint needs its own permission and is copy 2; previous results and search keep the report reachable; audit entries | same test | Passed |
| 4 | Test definition changes (renamed parameter, new unit, replaced range) do not change orders booked earlier; male/female ranges are applied per patient; free tests are billed at zero | `Definition_changes_never_alter_booked_orders_and_ranges_follow_gender` | Passed |
| 5 | Invalid numbers refused; self-verification blocked; send back requires a reason and returns the test to the pending list; corrections need *Edit Unverified Results*; bulk verification of the whole visit | `Verification_rules_rejection_correction_and_bulk_approval` | Passed |
| 6 | Verified values cannot be changed (even with direct SQL); result entry is closed after verification; withdrawal needs permission and a reason, keeps a revision with the original values (revision cannot be deleted), and the test is re-entered and re-verified | `Verified_results_are_protected_and_withdrawal_keeps_revision_history` | Passed |
| 7 | Lab fees through the Phase 3 cash counter: balance shown with `labOrderId`, partial payment refused for a full-payment test, allowed for a partial test; repeated submission → one receipt; paying a settled charge again refused; lab collection in lab statistics equals the receipts; Excel export audited; “release only paid tests” policy blocks then allows printing | `Laboratory_fees_use_the_phase3_cashier_without_duplicates` | Passed |
| 8 | Shared sample per sample type; sample rejection and test cancellation require reasons; replacement sample created; unpaid charge cancelled | `Sample_rejection_and_test_cancellation_need_reasons` | Passed |
| 9 | Lab dashboard counts; home page shows the Laboratory module; OPD registration and the cashier still work | `Lab_dashboard_counts_and_previous_phases_still_work` | Passed |
| 10 | Phase 1–3 functionality | all 50 earlier tests: setup, login, users, permissions, licence, backup/restore, patients, OPD, emergency, appointments, thermal printing, Print Agent, finance | Passed |

## Security tests

`Laboratory_permissions_are_enforced_on_the_server` checks the following:

* A user without laboratory permissions is refused on every laboratory page and the report page.
* A technician cannot open the catalogue, settings, reports, booking or verification, and cannot verify or cancel by posting directly.
* A booking user cannot enter results and sees no fees.
* A catalogue user without *Manage Laboratory Fees* cannot set a price (the test is created free).
* A user cannot change ranges without *Manage Reference Ranges*.
* *Register Laboratory Patient* is enough to register a patient from the laboratory.
* The database refuses to delete a lab order.

The catalogue test (`Permission_catalogue_is_consistent`) confirms the 25 laboratory permissions.

## Browser walk-through

`tools/ui-check/phase2.mjs` (Phase 4 part) runs these steps:

1. Configures lab settings (name, signatory, self-verification for the demo).
2. Prices and activates CBC and blood sugar, then books both for a registered patient with an external referral.
3. Opens the cashier with the lab balance and prints the order slip.
4. Collects the samples, enters all results with one abnormal value, and bulk-verifies them.
5. Prints the A4 report and opens results, history, dashboard and reports.

There were no script or browser console errors. Screenshots `p4-01` … `p4-16` are on the `ci-results` branch.
The sample PDFs are [A4 lab report](samples/sample-lab-report-A4.pdf) and [lab order slip 80 mm](samples/sample-lab-slip-80mm.pdf).

## Known limitations

* **Inventory**: only the item master (reagents, consumables, kits) exists. Stock receipts, consumption per test and expiry
  tracking come in a later phase.
* **Analyser interfacing**: no instrument/LIS connection (HL7/ASTM). Results are entered on screen.
* **PDF**: produced with the browser's *Save as PDF*. There is no server-side PDF file generation.
* **Barcode labels**: sample numbers are printed on the order slip. Dedicated barcode label printing is not included.
* **Physical printers**: the A4 report uses the normal Windows print dialog. Thermal slips use the Phase 2 printing routes,
  which are tested in CI; real printers were not available.
* **Starter catalogue**: reference ranges are typical adult values. The laboratory must review them, set prices and activate
  the tests before use.
