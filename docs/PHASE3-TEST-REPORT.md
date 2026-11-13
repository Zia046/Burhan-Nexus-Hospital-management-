# Phase 3 – testing report (financial, cashier & receipt management)

All tests run automatically in GitHub Actions on every push: PostgreSQL 16, the real ASP.NET Core application in memory,
a fresh database per test, plus a browser walk-through (Playwright/Chromium) of the published application and a
Windows Server 2022 installation test. Result for the Phase 3 code: **50 / 50 tests passed** (41 Phase 1/2 tests + 9 new Phase 3 tests),
Windows installation and Print Agent test passed.

| # | Requirement (prompt section 15) | Automated test | Result |
|---|---|---|---|
| 1 | Three cashiers on three counters, receipts credited to the right user, users cannot open each other's account, Admin sees all | `Each_cashier_has_a_separate_account_and_cannot_see_others` (A = Rs 5,000, B = 3,000, C = 2,000, total 10,000; B/C get *Access denied* for A's account, receipt and print page; dashboard total 10,000; counter transfer keeps history) | Passed |
| 2 | Deposit Rs 7,000 of Rs 10,000 → outstanding Rs 3,000; partial, rejected and over-deposits; no self-approval | `Deposits_reduce_outstanding_only_when_confirmed_by_someone_else` | Passed |
| 3 | Free and paid receipts, unique numbers, reprint keeps number, cancel with reason stays in history, double submission | `Receipts_are_unique_reprints_keep_the_number_and_cancellation_keeps_history` (also: database refuses DELETE/UPDATE of receipts and ledger rows) | Passed |
| 4 | Fee change only affects new receipts, partial payments and balances, discounts, bank reference | `Fee_changes_affect_only_new_receipts_and_partial_payments_track_the_balance` | Passed |
| 5 | Full and partial refunds, refund limit, ledger and doctor-share adjustments, audit | `Refunds_are_separate_from_cancellation_and_cannot_exceed_the_paid_amount` | Passed |
| 6 | Doctors with different fees and arrangements (A full fee, B 30 % commission, C fixed Rs 500, C 40 %), partial and full payment, overpayment refused, arrangement change only for new consultations | `Doctor_accounts_follow_each_fee_arrangement_and_settlements` | Passed |
| 7 | Daily closing per cashier, counter and hospital; totals equal transactions; nothing reset; adjustments with reason | `Daily_closing_matches_the_transactions_and_never_resets_them` | Passed |
| 8 | Unauthorised users blocked on every finance page and API, no export without permission, 16 simultaneous payments (incl. 4 duplicate submissions) → 13 unique receipts, Excel/CSV export | `Unauthorised_users_cannot_reach_finance_and_concurrent_payments_stay_unique` | Passed |
| 9 | Appointment/OPD visit billing without changing doctor-wise tokens or the thermal slip | `Appointment_and_opd_visits_can_be_billed_without_changing_tokens` | Passed |
| 10 | Phase 1 and Phase 2 functionality | all 41 earlier tests (setup, login, users, permissions, licence, backup/restore, patients, OPD, emergency, appointments, thermal printing, Print Agent) | Passed |

Receipt printing: 58 mm, 80 mm and A4 receipts were rendered to PDF by Chromium in CI (`docs/samples/sample-receipt-*.pdf`);
ESC/POS receipt bytes were checked in the tests. The thermal printer routes (browser, LAN, Print Agent) are the same as for the
appointment slips and were tested in Phase 2; physical printers were not available.

Screenshots of every finance screen are produced by the walk-through (`tools/ui-check/phase2.mjs`, files `p3-*.png` on the `ci-results` branch).
