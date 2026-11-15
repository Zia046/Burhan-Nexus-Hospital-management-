# Phase 7 – testing report (IPD, Ward, Gynae, Maternity, Labour Room & NICU)

**Result:** 76 / 76 automated tests passed on a real PostgreSQL 16 database (GitHub Actions).

- The total includes all tests from Phases 1–6, so the earlier phases still work.
- The browser walkthrough (Phase 7 part: 18 screenshots and 6 sample PDFs) completed with no script or page errors.
- The database migration `Phase7Ipd` is additive only: no column or table is dropped or altered.

## 1. Automated tests (`tests/…/IpdTests.cs`)

| Test | Scenarios covered (prompt Part 21) |
|---|---|
| Admission, bed allocation, transfer, records, billing, discharge and forms (Private) | IPD admission, beds, transfer, discharge, documentation, corrections, billing, printing, letterhead, audit |
| Pregnancy, ANC, labour, twin delivery, newborns, NICU and mother–baby rules | Gynae / ANC, high risk, Labour Room, partograph, delivery of twins, separate Patient IDs, rooming-in, NICU, return to mother, discharge order |
| Nursing, MAR, handover, ward access, permissions, concurrency and database protection | Nursing, medication safety, handover, ward restriction, permissions, concurrent bed allocation, bed status rules, no deletes / insert-only |
| Government mode | Admission and discharge without any purchase, sale or profit information |

**Admission, bed allocation, transfer, records, billing, discharge and forms (Private):**

- Seeded configuration:
  - 7 starter wards and more than 50 default form fields;
  - **no alert limits are built in**;
  - no beds until the hospital adds them.
- Bed-charge and admission-fee services are created as Phase 3 services.
- Admission from an OPD visit:
  - the number has the form `IPD-yyyy-nnnnnn`;
  - the bed becomes *Occupied*;
  - the admission fee is a charge linked to the admission.
- A double click creates no second admission. A second admission of the same patient is refused. An occupied bed cannot be given to another patient.
- The allergy is shown on the chart.
- Vitals and progress notes are stored. Text in a number field is refused. An empty field is **not stored** (never assumed normal). The status becomes *Under treatment*.
- A correction without a reason is refused. With a reason, it is saved as a new version and the original is kept. The correction is audited.
- Transfer:
  - without a reason, it is refused;
  - with a reason, the transfer row and allocation history are kept, the old bed needs cleaning and the new one is occupied.
- A procedure charge with quantity 2 is added. The bill tab links to the Phase 3 cashier with the admission pre-selected.
- Discharge:
  - without details, it is refused;
  - *Request discharge*, then *Complete discharge*: the bed is released for cleaning and at least one bed night is billed.
  - A closed admission accepts no new entries.
- Printed forms: discharge summary, final bill, admission, progress, vitals (with chart), nursing, MAR, daily bill and investigations.
  - Reprinting creates no charge, admission or record. Views are audited.
  - Letterhead mode removes the header and leaves the space.
- Inpatient list, patient profile card and dashboard work.

**Pregnancy, ANC, labour, twin delivery, newborns, NICU and mother–baby rules:**

- Pregnancy:
  - refused for a male patient;
  - EDD = LMP + 280 when left blank;
  - a second ongoing pregnancy is refused.
- Two ANC visits are stored as separate entries.
- High risk without a reason is refused, then accepted with a reason. The timeline, high-risk filter and ANC card work.
- Labour Room:
  - admission on a Labour Room bed;
  - a second admission is refused with *Move to Labour Room*;
  - two partograph entries are saved;
  - *Active labour* is set; *Delivery completed* by hand is refused.
- The partograph print draws the cervix chart and has **no built-in alert or action lines**.
- Twins delivery:
  - a NICU baby without a cot is refused;
  - after that, one delivery is saved and a double submit returns the same delivery;
  - 2 newborn records are linked to the mother;
  - each baby has its **own Patient ID**: "Baby of …", correct sex, never the mother;
  - baby 1 is a Newborn admission sharing the mother's bed; baby 2 is a NICU admission on the incubator with NICU details;
  - the labour is *Delivery completed* and the pregnancy *Delivered*;
  - the print and detail pages show both Patient IDs.
- NICU:
  - observations and a feeding note are recorded on the baby's own record;
  - NICU dashboard and observation chart work;
  - *Return to mother* releases the cot for cleaning and moves the baby to the mother's bed.
- The mother cannot be discharged while a baby is rooming-in. The babies are discharged first. The mother's bed stays occupied until her own discharge. The labour then becomes *Discharged*.
- The NICU discharge summary prints. Delivery and newborn registrations are audited.

**Nursing, MAR, handover, ward access, permissions, concurrency and database protection:**

- MAR orders and doses:
  - An order without a prescriber is refused.
  - An order "as written" (BD, 2 days) schedules the doses, all *Scheduled*.
  - The MAR shows the medicine and the allergy warning.
- Recording doses:
  - *Administered* without the five confirmations is refused; with them, it is saved.
  - Recording the same dose twice is refused.
  - *Withheld* needs a reason.
- Stopping the order cancels the future doses and keeps the given and withheld ones. The MAR prints.
- Ward access by department:
  - an IPD nurse sees the patient on the Nursing dashboard;
  - a Gynae nurse does not, and gets "not found" for the admission.
- Handover:
  - a nursing note is saved;
  - the outgoing nurse cannot acknowledge her own handover; another nurse can;
  - the handover prints.
- Permissions are enforced on the server. A nurse cannot:
  - discharge;
  - enter medication orders;
  - open Gynae or ward setup;
  - print the bill;
  - see the Billing tab;
  - write a doctor's progress note.
- Concurrency: two simultaneous admissions to the same bed → exactly one succeeds, with one open allocation.
- Bed status:
  - an occupied bed cannot be set available by hand;
  - maintenance without a reason is refused, and accepted with one.
- Database protection: deleting admissions, doses and bed-status history, and updating clinical records, are all refused by database triggers.

**Government mode:**

- Admission from an OPD visit works.
- IPD Settings shows the Government note.
- The billing tab, bill print and dashboard contain no profit, purchase price or sale price.
- Admission form and discharge work.

The existing test `ResetAndCatalogTests` also checks the new permission module (37 actions).

## 2. Browser walkthrough (`tools/ui-check/phase2.mjs`, Phase 7 part)

The walkthrough ran in a real Chromium against the running application (Private mode):

1. Beds added for the wards, Labour Room and NICU (incubator and cot).
2. A new patient registered and admitted with a bed and allergy.
3. Vitals and a progress note entered.
4. A Paracetamol TDS order entered from the handwritten prescription, and the first dose recorded with the five confirmations.
5. A shift handover written.
6. The bed map checked.
7. For the female patient: pregnancy registered, an ANC visit entered, admitted to the Labour Room, and three partograph entries made.
8. Twins delivered: one baby with the mother, one to the NICU.
9. NICU, Gynae, Nursing and IPD dashboards opened.
10. The first patient discharged.

Screenshots `p7-01` … `p7-18` are in the CI artefacts. The samples are in `docs/samples/`:

- `sample-ipd-admission-form-A4.pdf`
- `sample-ipd-mar-A4.pdf`
- `sample-ipd-partograph-A4.pdf`
- `sample-ipd-delivery-record-A4.pdf`
- `sample-ipd-discharge-summary-A4.pdf`
- `sample-ipd-final-bill-A4.pdf`

## 3. Clinical safety checks

The system:

- never suggests a diagnosis, medicine, dose or threshold;
- stores no default alert limits, and limits can only be added with the name of the hospital protocol;
- shows empty fields as "not recorded";
- never marks a dose as given automatically.
