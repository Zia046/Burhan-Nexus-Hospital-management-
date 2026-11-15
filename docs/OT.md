# Phase 8 – Operation Theatre (OT) & Surgical Management

The OT module covers every step of a surgery, from booking through the theatre and recovery room to billing. It is built on the existing modules:

- Patients and the Patient ID (Phase 2)
- Finance: services, fees, charges and the cashier (Phase 3)
- Laboratory (Phase 4)
- Pharmacy stock (Phase 5)
- Radiology (Phase 6)
- IPD and the configurable clinical forms (Phase 7)

Nothing is duplicated and no earlier data is changed.

## 1. Setup (one time, no code changes)

| Page | What you configure |
|---|---|
| **OT Rooms & Equipment** (`/Ot/Theatres`) | Theatres can be added in any number. Each theatre has a code, a name and a type (General, Gynaecology, Orthopedic, Emergency, Minor, Other). You also set its location, operating hours (blank = 24 h) and the cleaning / preparation interval between surgeries. Mark a theatre available or unavailable and give the reason (maintenance, cleaning…). A theatre can also have an **OT stock location**, which is a pharmacy department store, and a **theatre charge**, which is a Phase 3 service. The equipment register records each item's status and its last and next maintenance, and keeps the history. |
| **Surgery Catalogue** (`/Ot/Procedures`) | 17 starter procedures (general surgery, obstetrics & gynaecology, orthopaedics, minor procedures). Edit, deactivate or add your own. Each procedure has an expected duration, suitable anesthesia types, a default theatre type and an optional procedure charge (Phase 3 service). |
| **OT Settings** (`/Ot/Setup`) | Fee services: surgeon, assistant, anesthesia, anesthesia supplies, recovery, equipment and consumables. You also set the default cleaning interval, the scan upload size and the print header or letterhead margin. Consent types come with printable templates (`{patient}`, `{procedure}`, `{hospital}`). The **pre-operative, Sign In, Time Out and Sign Out checklists** are edited here: add, rename, make mandatory / optional, deactivate. |
| **Clinical form editor** (IPD Settings) | The fields of the pre-operative assessment, anesthesia assessment, intra-operative monitoring, PACU and post-operative forms. **No clinical alert limits are pre-set.** Add them only from the hospital's approved protocol. |

The starter consent texts are examples only. The hospital must approve the wording.

## 2. Booking and scheduling

- **Book surgery** (`/Ot/Book`):
  - Search the registered patient (or open it from the patient profile) and choose the procedure, theatre, date / time, duration, priority (Elective / Urgent / Emergency), anesthesia plan, surgeon, assistant and anesthetist.
  - **Check availability** shows conflicts before saving.
  - A double click never creates a second booking.
  - If the patient is admitted in IPD, the surgery is linked to the admission automatically.
- **Conflicts are refused**:
  - the same theatre is busy (the cleaning interval after each surgery counts);
  - the surgeon or anesthetist is booked elsewhere at the same time;
  - the theatre is unavailable;
  - an elective surgery falls outside the operating hours.
- **Emergency** (`/Ot/Book?emergency=true`):
  - The reason is required.
  - The start time may be left blank, which means now.
  - The surgeon may be added later.
  - A team conflict can be accepted only for an emergency, with a written reason.
- **Statuses**:
  - Requested → Scheduled → Pre-operative preparation → Ready → In progress → Completed.
  - A surgery can also be Postponed or Cancelled.
  - Cancel and postpone need a reason. A postponed surgery gets its new date by **Reschedule** (also with a reason).
  - Every change is kept in the surgery history and the audit log.
- **OT schedule** (`/Ot/Schedule`) shows a day or week per theatre, and can be printed. The **dashboard** (`/Ot`) has the KPIs, the theatre board and seven sections, with date / theatre / surgeon filters.

## 3. Surgery record (`/Ot/Surgery/{id}`)

| Tab | Content |
|---|---|
| Booking & team | Booking details, edit and reschedule. **Surgical team**: roles linked to doctors or to HMS users who have an OT permission. A removed member stays in the history, with a reason. CSSD sets can be issued to the surgery. |
| Pre-op | Pre-operative assessment (configurable form). Existing **Laboratory / Radiology** orders of the patient are listed with links to their reports, and new tests are ordered through the normal modules (no duplicate records). The **pre-operative checklist** shows mandatory pending items as warnings. |
| Consent | Document consent (type, person, relation, time, status, staff, witness). Each consent has its own printable form. **Signed scans** (PDF / JPG / PNG) are stored as files in the protected storage folder, which is included in backups, and are shown only to authorised users. A status change needs a reason. |
| Anesthesia | Pre-anesthesia assessment, anesthesia times and provider, drugs and fluids, intra-operative monitoring. Values are kept exactly as entered; a missing value is “not recorded”. |
| Safety checklist | **Sign In → Time Out → Sign Out**, in that order. A stage with pending mandatory items can be completed only by a user with *Authorize Safety Exceptions* and with a reason. The exception is shown and audited. |
| Intra-op & notes | **Start surgery** needs Sign In and Time Out (and the pre-op checklist for non-emergency surgery), or an authorised emergency exception. Only one surgery can be in progress per theatre. The **operation note** is a draft until *Finalize*. A final note cannot be changed (the database also refuses it). Corrections are made by **Amend**, with a reason, as a new version. Specimens (collected → dispatched → received at the laboratory, optionally linked to an existing lab order) are recorded here. **End surgery** needs Sign Out and moves the patient to recovery. |
| OT consumption | Major OT patient-wise use of consumables from the theatre's OT store, and returns of unused items. See section 4. |
| Recovery | PACU status, nurse, observations (chart), drugs. **Transfer / discharge from recovery** needs the destination, the receiving staff and the **clinician who decided it**. The system never derives fitness from observations. |
| Post-op | Post-operative instructions with their **source**, for example “Handwritten by doctor”: the user entering them is recorded and the responsible clinician is named. Scans can be uploaded. The record is linked to the IPD admission, and medication orders go on its MAR. |
| Billing | See section 6. |
| History | Every booking, status, team, checklist, note, stock and recovery event. |

## 4. OT stock and the Minor OT

- **Major OT**:
  - The pharmacy transfers stock to the theatre's OT store with the normal Phase 5 transfer.
  - In the surgery, *Record use* deducts from that store **only when the entry is saved**, using FEFO batches and the single stock ledger (`OtConsumption`). *Return unused* puts stock back (`OtReturn`).
  - More than the available stock is refused, and the entries cannot be deleted.
- **Minor OT – unchanged.** The Minor OT stays the existing pharmacy department location: the pharmacy transfers stock to it, and there is no login and no patient-wise consumption entry.

## 5. CSSD (`/Ot/Cssd`)

- Instruments and instrument sets (contents with quantities, optional theatre).
- **Sterilisation batches** (number `CSB-…`): method, steriliser, cycle, parameters, responsible staff and the sets in the load.
  - The result is *Passed* (with a sterile-until date) or *Failed* (with a reason).
  - **A failed cycle is never released**, and the database enforces this too: its sets stay *Not sterile*.
  - A passed batch must be **released** before its sets become *Sterile*.
- Only a sterile, unexpired set can be issued, to a surgery or to staff.
- A returned set becomes *Not sterile* and must be reprocessed.
- A set can be quarantined, with a reason.

## 6. Billing (Phase 3 only)

- **Post OT charges** adds the configured services as normal Phase 3 charges, linked to the surgery and the IPD admission: theatre, procedure, surgeon, assistant, anesthesia, supplies, recovery, equipment and consumables.
  - Each one is posted **only once**.
  - Other surgical services can be added.
  - Medicines and investigations are refused here, because they are billed in Pharmacy / Laboratory / Radiology.
  - A discount needs the discount permission and a reason.
- Payments (also partial), receipts, cancellations and refunds are done at the **existing cashier**. *Collect payment* opens it with the patient and the OT charges pre-selected.
- Cancelling a surgery never deletes charges or payments.
- **Government mode:** only the services the hospital configured are charged (for example a registration or OT fee). There is no purchase, retail or profit calculation.

## 7. Printing (14 A4 documents)

`/Print/Ot/{form}/{id}`, with *Print* or *Save as PDF*:

- booking slip
- OT schedule
- pre-operative assessment & checklist
- consent form
- pre-anesthesia assessment
- anesthesia record (with charts)
- surgical safety checklist
- operation notes
- intra-operative record
- specimen dispatch form & labels
- recovery (PACU) chart
- post-operative instructions
- OT bill
- recovery transfer / handover

Each document has the hospital header, or blank space for a pre-printed letterhead. Printing only reads the stored records. Every view is audited.

## 8. Reports (15) – `/Ot/Reports`

**List:**

1. Daily surgeries
2. Monthly surgeries
3. Surgeon-wise
4. Department-wise
5. OT-wise utilization
6. Procedure-wise
7. Emergency vs elective
8. Cancelled / postponed
9. Anesthesia type
10. Complications (as documented)
11. Recovery room
12. OT consumption
13. OT stock
14. CSSD sterilisation
15. OT revenue & charges

**Filters, print and export:**

- Filters: date, theatre, surgeon, department, procedure and status.
- *Print / PDF*, plus Excel and CSV export. The export is audited and needs *Export OT reports*.
- The revenue report needs *View OT billing*.

## 9. Permissions

The module **Operation Theatre** has 26 actions:

- Dashboard
- Manage theatres
- Manage catalogue
- Create / edit / cancel bookings
- View / enter pre-op
- Consent
- Team
- Anesthesia
- Intra-op
- Finalize notes
- Checklist
- **Authorize safety exceptions**
- OT stock
- Record consumption
- Recovery
- Post-op
- CSSD
- View billing
- Manage charges
- Print
- Export
- Statistics
- Settings

Each permission is checked on the server for every page and action. Hiding buttons is not the only protection.

## 10. Data integrity

- Insert-only history, which cannot be changed or deleted:
  - surgery events
  - checklist answers and stage completions
  - drugs and fluids
  - OT consumption and returns
  - equipment logs
  - OT charge links
  - CSSD batch contents
  - documents
- Master and clinical records cannot be deleted:
  - theatres, procedures and surgeries
  - team members, consents and checklist items
  - operation notes, specimens and recovery episodes
  - CSSD instruments, sets, batches and issues
- Database guards:
  - Final operation notes are immutable.
  - The surgery number and patient never change.
  - A completed or cancelled surgery keeps its status.
  - A failed CSSD batch is never passed or released.
- Concurrency:
  - Scheduling is serialised with a database lock.
  - Concurrent edits are detected (row versions).
  - Duplicate submits are ignored (idempotency keys).
