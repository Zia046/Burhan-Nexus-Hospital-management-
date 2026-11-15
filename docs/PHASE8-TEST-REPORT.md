# Phase 8 – testing report (Operation Theatre & Advanced Surgical Management)

**Result:** 86 / 86 automated tests passed on a real PostgreSQL 16 database (GitHub Actions).

- The total includes every test from Phases 1–7 and the navigation change, so the earlier phases still work.
- The 8 new OT tests are in `tests/…/OtTests.cs`. The catalogue test also checks that the OT module has 26 permission actions.
- The browser walkthrough (Phase 8 part: 18 screenshots and 14 sample PDFs) completed with **no script or page errors**.
- The database migration `Phase8Ot` is additive only: no existing table or column is dropped or altered.

## 1. Automated tests by subphase

| Subphase | Test | Covered |
|---|---|---|
| 8A | Theatres, catalogue, booking, conflicts, emergency and schedule changes | Seeded data, theatres, catalogue editing, booking and idempotency, OT overlap, cleaning interval, surgeon and anesthetist conflicts, unavailable theatre, emergency booking, reschedule and postpone, cancel with a reason, dashboard, schedule, patient profile, audit. Details in section 2. |
| 8B / 8C | Pre-op, consent, team, checklist, exception, notes, recovery and post-op linked to IPD | Pre-op assessment, checklists, consent and scans, team, anesthesia, safety stages, operation notes, recovery transfer, post-op record, history. Details in section 2. |
| 8C | Emergency exception is authorised, documented and visible | Start by exception with a reason (audited, visible in the history). A second surgery cannot start in a theatre where one is in progress. |
| 8D | Major OT consumption deducts OT stock on save, returns, Minor OT unchanged | Pharmacy transfer to the OT store. Consumption is refused before preparation. On save the stock is deducted exactly once, from the OT store only, and the ledger entry is `OtConsumption`. More than the stock is refused. A return needs a reason and cannot exceed the use. The **Minor OT** gets only its transfer: no patient entries. Consumption rows cannot be deleted. Report. |
| 8E | CSSD failed cycle never released, issue / return and specimen dispatch | Instruments, sets and batches. A failure needs a reason, and the sets stay *not sterile*. Releasing a failed batch is refused, and the database refuses it too. A non-sterile set cannot be issued. Passed → release → sterile → issue to surgery → return → not sterile. Specimens: collected → dispatched → received (receiver required), with printable labels. |
| 8F | OT charges post once through Phase 3, discount rules, cancellation keeps charges | Standard charges are posted once; a second post is refused. A discount needs a reason. The cashier link is pre-filled. A cancelled surgery keeps its charges and takes no new ones. Users without *View OT billing* cannot open the bill or the revenue report. |
| 8F | Government mode | Only the configured fee is charged. There is no retail or profit calculation. |
| 8G | 14 print forms, 15 reports, permissions, audit and database protection | All 14 documents print, with charts. Printing creates nothing and is audited. 15 reports, Excel export (audited). Server-side denials for booking, theatres, CSSD, notes, cancel, print and reports. The navigation category is shown. Database guards: surgeries, theatres and procedures cannot be deleted; events and checklist rows are insert-only; the patient on a surgery is immutable. |

## 2. Details of the main tests

**8A – theatres, catalogue, booking, conflicts, emergency and schedule changes:**

- Seeded data: starter procedures, consent types and checklists, and **no alert limits**.
- Theatres:
  - added through the UI;
  - an unavailable theatre is refused.
- The catalogue can be edited.
- Booking and idempotency:
  - the surgery number has the form `SRG-yyyy-nnnnnn`;
  - a double click does not create a second booking.
- Conflicts:
  - overlap in the same OT is refused, and the **cleaning interval** counts;
  - surgeon and anesthetist conflicts are refused.
- A surgeon is required.
- Emergency booking:
  - the reason is required;
  - the start is *now*.
- Schedule changes:
  - reschedule needs a reason and is kept in the history;
  - *Ready* is refused before pre-op;
  - postpone and cancel need a reason;
  - a postponed surgery only goes back to *Scheduled* by rescheduling;
  - a cancelled surgery frees its slot and is locked.
- Dashboard, schedule, patient profile and audit work.

**8B / 8C – pre-op, consent, team, checklist, exception, notes, recovery and post-op linked to IPD:**

- The IPD admission is linked automatically, and the allergy is shown.
- Pre-op assessment:
  - saved with the surgery and the admission;
  - Laboratory / Radiology results are listed without creating orders.
- Pre-op checklist:
  - pending mandatory items block completion;
  - N/A on a mandatory item needs a remark;
  - status flow PreOp → Ready.
- Consent:
  - required fields are checked;
  - an **executable upload is refused**, and the PDF scan is stored and served;
  - the printable consent works;
  - a status change needs a reason.
- Team:
  - a user without an OT permission is refused;
  - doctors and users are added;
  - no changes are allowed after the start.
- Anesthesia assessment and drugs are saved.
- Safety checklist:
  - Time Out before Sign In is refused;
  - start without the checklist is refused;
  - an exception without *Authorize* permission is refused.
- Operation note:
  - draft → finalize (incomplete is refused);
  - edit after final is refused, and the **database refuses an UPDATE of the final note**;
  - amendment needs a reason, and versions 1 and 2 are both kept.
- End needs Sign Out → recovery.
- Recovery and post-op:
  - PACU observations;
  - transfer without a clinician's decision is refused;
  - the post-op record keeps its source *Handwritten by doctor* and is linked to the IPD admission;
  - printed instructions.
- History and audit.

## 3. Browser walkthrough (CI, Chromium)

**Screenshots** `p8-01` … `p8-18`:

- theatres and equipment, catalogue
- booking form, surgery and team
- pre-op assessment and checklist, consent
- safety checklist, anesthesia record
- final operation note with specimen
- recovery board and transfer
- OT billing, surgery history
- CSSD
- OT dashboard, weekly schedule, reports
- the *Operation Theatre* taskbar menu

**Sample PDFs (A4, in `docs/samples/`):**

- booking slip
- pre-op checklist
- pre-anesthesia assessment
- anesthesia record (with charts)
- safety checklist
- operation notes
- intra-op record
- specimen dispatch / labels
- PACU chart
- post-op instructions
- recovery handover
- OT bill
- consent form
- OT schedule

## 4. Acceptance criteria (prompt section 17)

All 25 acceptance tests are covered by the automated tests above, by the walkthrough, or by both:

- theatre setup
- catalogue
- booking for existing patients
- OT overlap
- surgeon / anesthetist conflict
- emergency
- reschedule history
- pre-op
- investigations without duplicates
- consent and scans
- team
- anesthesia
- the three-stage checklist with an exception
- intra-op and notes (draft / final / amend)
- OT stock reduction
- Minor OT unchanged
- specimens
- recovery handover
- post-op linked to IPD
- CSSD failed never released
- Phase 3 billing without duplicates
- printing
- backend permissions
- audit
- database protection
- Government mode
