# Phase 7 – IPD, Ward, Gynae, Maternity, Labour Room & NICU

This phase adds inpatient care to Burhan Nexus Hospital Management:

- admissions, wards, rooms and beds;
- the Gynae / ANC unit;
- the Labour Room, deliveries and newborns;
- postnatal care and the NICU;
- nursing and the medication administration record (MAR);
- IPD billing;
- 23 printable A4 forms.

It builds on the earlier phases. It reuses:

- the patient register: a newborn gets its own Patient ID;
- OPD and Emergency visits as admission sources;
- Phase 3 charges and the cashier;
- Laboratory and Radiology orders, linked to the admission;
- the Phase 5 medicine catalogue and ward stock.

Nothing existing was rebuilt.

The menu section **IPD & Maternity** has these screens:

- IPD Dashboard · Admit Patient · Inpatients · Bed Map
- Gynae & ANC · Labour Room · NICU · Nursing
- Wards & Beds · IPD Settings

> **Clinical safety.** The system records what clinicians decide:
>
> - It never diagnoses, prescribes, suggests doses or interprets values.
> - No clinical thresholds are built in. The partograph has no alert or action lines unless the hospital configures them.
> - A field left empty is stored as **not recorded**. It is never treated as "normal".

## 1. Wards, rooms and beds (configuration, no code changes)

- **Wards** have:
  - code, name and type: General, Male / Female General, Children, Gynae, Maternity, Postnatal, Labour Room, NICU, Other;
  - department and floor / location;
  - capacity and responsible staff;
  - a **bed charge per night**, which is a Phase 3 service;
  - an optional **ward stock location** from the Phase 5 department stock.
- **Rooms** have a type and a bed capacity.
- **Beds** have a unique number and a type: Standard, Labour, Incubator, Cot, Radiant warmer. Cots and incubators can carry an equipment ID.
- The first start creates 7 starter wards, each with "Room 1" and no beds:
  - Male General Ward, Female General Ward, Children Ward;
  - Gynae Ward, Labour Room, Postnatal Ward;
  - NICU.
- It also creates the departments IPD, Gynae and NICU.
- **Bed statuses** are Available, Occupied, Reserved, Cleaning required, Under maintenance and Temporarily unavailable.
  - *Occupied* is set only by an allocation.
  - Maintenance and unavailability need a reason.
  - Every change is kept in the bed history.
- The **Bed Map** shows every ward as coloured tiles with the patient's name, and can be filtered by ward.

**Concurrency** is handled at database level:

- An open allocation is unique per bed (filtered unique index) and the bed row is locked (`SELECT … FOR UPDATE`). Two people can never give the same bed to two patients.
- A patient can have only one active admission (filtered unique index). The admission form carries an idempotency key, so a double click returns the same admission.
- The only exception to "one patient per bed" is **rooming-in**: a newborn shares the mother's bed through a *shared* allocation.

## 2. Admission, transfer and discharge (ADT)

### Admission

**Admit Patient**:

1. Search for an existing patient. There is no duplicate registration.
2. Choose the source visit. Sources: OPD, Emergency, Gynae, Maternity, Direct, Delivery room, Labour room, Postnatal ward, External referral, Other.
3. Enter the department, the referring and admitting doctor, the admission diagnosis, the initial condition, medical history, **allergies** and the attendant.
4. Choose the bed. It can also be allocated later.

The **admission number** is `IPD-yyyy-nnnnnn`, taken from a database sequence.

- Direct admissions can be switched off in IPD Settings.
- **Statuses:** Registered → Admitted → Under treatment (set by the first clinical entry) → Transferred / Discharge requested → Discharged / Referred, or Cancelled.
- *Cancel* is only for a wrong entry. It is refused once anything clinical has been recorded or a payment was received.
- A closed admission cannot be reopened. A database trigger enforces this, and also keeps the patient and the admission number immutable.

### Transfer

A transfer can move the patient to another bed, room, ward or department, or to an external facility.

- It needs a reason and can name the receiving staff.
- Each transfer is kept as its own record, and can be printed as a transfer form.
- The old bed is set to *Cleaning required*.
- A baby rooming-in moves with the mother.

### Discharge

1. *Request discharge* (optional) records who asked for it.
2. *Complete discharge* needs:
   - a discharge type: Routine, Referred, LAMA, Transferred, Death, Other;
   - the final diagnosis;
   - the condition at discharge;
   - the receiving facility, for referral or transfer.
3. It also records the treatment summary, procedures, investigations, discharge medications (as prescribed) and follow-up.

Discharge rules:

- Babies rooming-in must be discharged or transferred first.
- Bed nights are posted.
- If *Final bill must be cleared* is on (Private only), the discharge is refused while there is a balance.
- The bed is released only at the end.

The discharge summary prints from the admission.

## 3. Clinical documentation (configurable forms)

All documentation uses one generic, configurable store:

- Doctors' daily progress notes and vital signs.
- Nursing assessment and nursing notes.
- Gynae history and Gynae assessment.
- ANC visits.
- Labour initial assessment and labour observations (the partograph).
- Postnatal assessment.
- NICU: newborn assessment, observations, feeding / intake-output, care (oxygen, phototherapy, incubator) and daily progress.

Under **IPD Settings → Clinical form fields** the clinical administrator can add or change fields without code:

- Each field has a label, section, type (number, text, long text, choice, yes / no), unit, choices and required flag.
- **Alert limits** are optional, apply to numbers only, and must name the approved hospital protocol they come from.
  - A value outside the limits is highlighted ⚑ on screen and on charts (dashed line).
  - It is never interpreted.

Rules for every entry:

- Each entry records the observation time, the entry time, the user and the responsible clinician or nurse.
- Values are validated (numbers, choices). Future times are refused.
- An empty value is **not stored** and prints as "—" or "not recorded".
- **Corrections** never overwrite. A correction is a new version that points to the original and carries a reason. It needs the **Correct Records** permission. The original stays visible, marked "corrected".
- Clinical records are insert-only at database level: update and delete triggers refuse changes.

## 4. Gynae unit & ANC

- **Gynae & ANC** has:
  - patient search;
  - active pregnancies (high-risk first, with a high-risk filter);
  - Gynae inpatients.
- The patient's Gynae page holds:
  - Gynae / obstetric history and Gynae assessment, which are **sensitive**: viewing needs *View Gynae Records* and entering needs *Enter Gynae Records*;
  - every pregnancy, each kept separately, and the patient's admissions.
- **Pregnancies:**
  - female patients only, with one ongoing pregnancy at a time;
  - fields: LMP, EDD (LMP + 280 days if left blank), G / P / A / living children, previous complications, history and risk factors;
  - changes are audited with old and new values;
  - a closed pregnancy is kept unchanged;
  - it is closed as *Delivered* by the delivery record, or as *Ended* or *Care transferred out* with a note.
- **ANC visits** are separate entries and are never overwritten.
- **High-risk** is the clinician's decision. It needs a reason and can record the assessment, referral and follow-up plan.
- The **pregnancy timeline** combines these events in one list: registration, changes, ANC visits, admissions, delivery, babies and postnatal assessments.
- The ANC card prints.

## 5. Labour Room, delivery and newborns

**Labour Room admission:**

- It needs a Labour Room bed and the existing (female) patient, and is linked to the ongoing pregnancy.
- A woman already admitted in a ward is not admitted twice. From her admission, choose *Move to Labour Room*.

**Labour statuses:**

- Set by hand: Admitted → Under observation → Early labour → Active labour.
- *Delivery completed* is set only by recording the delivery.
- Referral, transfer and discharge go through the admission.

**Observations / partograph:**

- The fields are configurable. By default:
  - Labour progress: cervix, descent, contractions and their duration;
  - Fetal: FHR, membranes, liquor, moulding;
  - Maternal: pulse, BP, temperature, respiration, urine, oxytocin / IV as ordered.
- The **partograph print** draws one chart per numeric field from the recorded values only.

**Delivery record:**

- Fields:
  - time and type: NVD, assisted vaginal, caesarean, other;
  - delivering doctor and staff;
  - outcome, placenta, blood loss, perineum, maternal condition, complications and immediate care.
- A delivery can have **1 to 6 babies**.
- For each baby: sex, weight, APGAR 1 / 5 / 10, live birth or stillbirth, resuscitation, examination, and placement (with mother / NICU + cot).
- Double submit is safe (idempotency key).

**Each live-born baby:**

- It is registered through the normal patient service with a **separate Patient ID**: "Baby of <mother>", guardian = the mother, DaughterOf / SonOf.
- With mother: a Newborn admission is created on the mother's bed (shared allocation).
- NICU: a NICU admission is created on the selected cot / incubator.
- It is linked to the mother's admission. Two babies cannot share one cot.

Delivery record, newborn record and birth record (for official registration) print from the delivery.

## 6. Postnatal care & NICU

- **Postnatal assessments:**
  - fields: vitals, uterus, lochia, pain, breastfeeding, mobility, urination, wound / perineal care, treatment, nursing and discharge readiness;
  - they are made on the mother's admission and print as the postnatal record.
- **NICU:**
  - The dashboard shows the babies, the time of the last observation, cot / incubator tiles and the babies rooming-in.
  - **NICU admission** is for a baby born here (with the birth record), a baby rooming-in (transferred to the cot) or an external referral. It records the reason, gestational age, weights, condition, birth history, clinician and parent.
  - NICU documentation covers assessment, observations, feeding / intake-output, care and daily progress. It prints as the NICU observation chart and the feeding chart.
  - *Return to mother* releases the cot for cleaning and moves the baby back to the mother's bed.
  - The NICU discharge summary prints after discharge.

## 7. Nursing

- The **Nursing** dashboard is limited to the **wards of the nurse's department(s)**. *View All Wards* removes the limit.
- It shows patients, doses due or overdue in the next 2 hours, tasks due, and handovers waiting for acknowledgement.
- The same ward limit applies when opening an admission: a nurse from another ward gets "not found".
- **Tasks:** vital signs, medication, feeding, intake / output, wound care, repositioning, other. A task is marked done or cancelled (cancel needs a reason).
- **Shift handover:**
  - The outgoing nurse records condition, diagnosis, pending investigations, medication due, IV fluids, observations, procedures, special care and outstanding tasks.
  - **A different user (the incoming nurse) must acknowledge it.**
  - It prints as a handover form.

## 8. Medication administration (MAR)

**Orders:**

- An order is entered from the doctor's **handwritten prescription**, a verbal order or direct entry.
- It records:
  - the medicine (Phase 5 catalogue);
  - dose, route and frequency *as written*;
  - administration times, duration (1–60 days), start and prescriber;
  - instructions;
  - optional ward-stock units per dose.
- No medicine or dose is suggested.

**Dose scheduling and recording:**

- Every dose is scheduled as *Scheduled*. The status changes only when a nurse records it:
  - Administered: time, dose and route given;
  - Missed / Withheld: a reason is required;
  - Pending.
- A recorded dose cannot be recorded again (database trigger).
- Overdue doses are highlighted. Nothing changes silently.

**Safety:**

- With *MAR confirmations* on (default), *Administered* needs ticks for right patient, medicine, dose, route and time.
- The patient's **allergies** are shown on the chart banner and in every dose form.

**Stopping an order** needs a reason. It cancels the future doses and keeps the history.

**Ward stock:**

- If the ward has a stock location and the order has units per dose, *Administered* takes the units from that ward stock (FEFO) as a `WardAdministration` movement in the existing Phase 5 ledger.
- If there is not enough stock, the system asks for a pharmacy transfer first.
- There is no second ledger.

## 9. Investigations

- From the chart, *Order laboratory test*, *Order X-ray / ultrasound* and *Issue medicines* open the existing Phase 4, 6 and 5 screens with `admissionId`.
- The order or issue and its charges are linked to the admission. Government hospitals do not need a visit for an admitted patient.
- The admission's Investigations tab lists everything ordered for it with links to the results.

## 10. IPD billing (Phase 3 only)

All IPD amounts are Phase 3 charges carrying `IpdAdmissionId`:

- Admission fee: the service set in IPD Settings.
- Bed charges: one per night per admission (unique), using the ward's bed-charge service.
  - *Post bed charges* posts up to last night.
  - Discharge posts the rest, with at least one night.
- Services and procedures added on the Billing tab. Quantity is 1–100. A discount needs *Give Discounts* and a reason.
- Laboratory, Radiology and Pharmacy charges, created by those modules.

Investigation and medicine services cannot be added by hand, which prevents double billing.

The Billing tab shows the lines by source with totals and balance. *Collect payment* opens the Phase 3 cashier with the admission's charges pre-selected.

The **daily bill** (any date) and the **final bill** print. Before discharge, the final bill is marked *provisional*.

**Government mode:**

- services are recorded for the patient's record;
- there is no admission fee setting and no bill-clearance rule;
- no purchase, sale or profit figures appear anywhere.

## 11. Printable forms (A4)

All forms are at `/Print/Ipd/{form}/{id}`.

| Form key | Form | ID |
|---|---|---|
| `admission` | IPD admission form (with consent signatures) | admission |
| `progress` | Daily progress notes | admission |
| `vitals` | Vital signs chart (charts + table) | admission |
| `nursing` | Nursing assessment & notes | admission |
| `mar` | Medication administration record | admission |
| `transfer` | Patient transfer form | transfer |
| `handover` | Nursing shift handover | handover |
| `discharge` | Discharge summary | admission |
| `daily-bill` | Daily IPD bill (`?date=`) | admission |
| `bill` | Final IPD bill | admission |
| `investigations` | Investigation summary | admission |
| `gynae` | Gynae / obstetric record | patient |
| `anc` | ANC card | pregnancy |
| `labour` | Labour record | admission |
| `partograph` | Partograph / labour observation chart | admission |
| `delivery` | Delivery record | delivery |
| `newborn` | Newborn record | newborn record |
| `birth` | Birth record (for notification) | newborn record |
| `postnatal` | Postnatal record | admission |
| `nicu-admission` | NICU admission form | admission |
| `nicu-chart` | NICU observation chart | admission |
| `feeding` | NICU feeding & intake / output chart | admission |
| `nicu-discharge` | NICU discharge summary | admission |

Header, footer and pre-printed letterhead are set in IPD Settings:

- With *Print the hospital header* on, the logo, name, address and contact are printed.
- With it off, a blank space of the configured top margin is left instead.

A PDF is made with *Save as PDF* in the print dialog.

Printing and reprinting only read the stored records. Nothing is created or changed. Each view is audited as `Ipd.FormPrinted`.

## 12. Permissions (module "IPD, Maternity & NICU", 37 actions)

- **Dashboard and patients:** View IPD Dashboard · View Inpatient Records · View All Wards.
- **ADT:** Register Admission · Edit Admission · Allocate Bed · Transfer Patient · Request Discharge · Discharge Patient.
- **Configuration:** Manage Wards · Manage Rooms · Manage Beds.
- **Doctors:** Progress Notes.
- **Gynae / ANC:** View Gynae Records · Enter Gynae Records · Manage ANC.
- **Labour Room:** Manage Labour Room · Labour Observations · Partograph · Enter Delivery · Register Newborn · Link Mother & Baby.
- **Postnatal and NICU:** Postnatal Care · Manage NICU · NICU Observations.
- **Nursing:** Manage Nursing · Nursing Notes · Shift Handover.
- **Medication:** Medication Orders · Record Medication Administration.
- **Investigations and records:** View Investigations · Correct Records.
- **Billing:** View IPD Financial · Manage IPD Billing.
- **Printing and settings:** Print Forms · Export PDF · IPD Settings.

How permissions are enforced:

- Every handler checks its permission on the server, in addition to the hidden buttons.
- Each kind of clinical record has its own write permission. For example, a nurse cannot write a doctor's progress note.
- Ward access is checked for every admission, print and action.

## 13. Audit and data protection

- Every action is audited, for example:
  - wards, rooms, beds and bed status;
  - admissions, edits, cancellations and blocked duplicates;
  - allocations, transfers and discharges;
  - records and corrections;
  - pregnancies, labour, deliveries, newborns and mother–baby links;
  - NICU admissions, tasks and handovers;
  - medication orders, stops and doses;
  - charges and printed forms.
- **No deletes.** Database triggers refuse deletion of:
  - wards, rooms, beds, admissions and allocations;
  - pregnancies, labour, newborns and NICU;
  - tasks, handovers, orders and doses.
- Bed status history, transfers, events, clinical records, bed charges and delivery records are **insert-only**.
- Check constraints:
  - a baby can never be its own mother;
  - newborn Patient IDs are unique per baby;
  - one birth order per delivery.

## 14. Upgrade

The migration `Phase7Ipd` only adds tables, columns, indexes, sequences and triggers. Existing data is not changed.

On first start after the update, the starter wards, rooms, form fields and departments are created once.
