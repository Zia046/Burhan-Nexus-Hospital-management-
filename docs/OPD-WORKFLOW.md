# OPD workflow (Phase 2)

The doctor does **not** use the software. Reception registers the visit and prints a slip; the doctor writes on the slip by hand.

```
Patient arrives ─▶ Reception: search patient (or register new)
                 ─▶ New OPD visit ─▶ visit number + token ─▶ slip prints
                 ─▶ Patient takes slip to doctor ─▶ doctor writes complaints, findings, diagnosis, Rx, tests
                 ─▶ Patient takes slip to laboratory / X-Ray / pharmacy if advised
```

## Registering an OPD visit
1. *OPD ▸ New OPD visit* (or **Register & OPD visit** on the registration screen, or *OPD* in search results).
2. Find the patient by ID, name, mobile or CNIC – or press *New patient*.
3. Department is pre-selected (default from *OPD setup*); choose the doctor (optional in Government mode).
4. Optional presenting complaint and notes.
5. **Register & print slip** – the slip opens and the print dialog appears automatically (can be switched off in *Patient ID & slips*).

Every visit creates a **new encounter** with a unique OPD number under the **same Patient ID**. Earlier visits are never overwritten; repeat visits are marked *Follow-up*.

### Tokens
If enabled (*OPD ▸ OPD setup*), each visit gets a daily token number, restarting at 1 every day – for the whole OPD, per department or per doctor. Token numbers are issued under a database lock, so simultaneous registrations on several computers never get the same token.

## Government hospital slip
Printed on **A5** by default (A4 available). Contains hospital name, logo and address, Patient ID, OPD slip number, token, patient name, father's/husband's name, age, gender, mobile, date & time, doctor (if selected) and department – and leaves most of the page **blank for the doctor**:

- Complaints · Clinical findings (BP/Pulse/Temp/Wt prompts) · Diagnosis · Lab investigations · X-Ray / Ultrasound (left column)
- Large **℞** area for medicines/prescription
- Advice / follow-up, next visit date, doctor's signature & stamp

Black-and-white friendly (no coloured backgrounds). See `docs/samples/sample-opd-slip-A5.pdf` and `sample-opd-slip-A4.pdf`.

Fee: OPD visits store a **fee reference** (*OPD Registration*, default fee 0 = free). No money is collected or recorded in Phase 2; the billing phase will create receipts linked to the Patient ID and visit.

## Private hospital mode
- Visits are recorded **against a doctor** (required by default when doctors exist) → doctor-wise history in *OPD* (filter by doctor) and on the patient profile.
- A separate **Visit Record** document is printed (doctor's name, qualification and specialty at the top) instead of the government OPD slip. See `docs/samples/sample-private-visit-document-A5.pdf`.
- Doctor-wise consultation fees can be prepared as fee references (*OPD setup ▸ Service / fee references*, optionally linked to a doctor); actual fee collection comes with Doctor & Financial Management.

## Printing, reprinting and PDF
- Every slip page has **Print**, **Save as PDF** (choose "Save as PDF" as printer), A5/A4 switch and a Back button.
- Each print/reprint is recorded (copy number, paper, user, computer) and shown on the visit page and in the audit log.
- Browser print settings: Margins *Default*, Scale *100%*; enable *Background graphics* if the logo is missing.

## OPD list
*OPD* shows today's visits (token, visit no., time, patient, department/doctor, printed or not). Filter by date range, department, doctor or search text.

## Permissions (OPD Management)
| Permission | Allows |
|------------|--------|
| View OPD Visits | OPD list and visit details |
| OPD Registration | Register visits |
| OPD Slip Printing | Print / reprint slips and visit documents |
| Edit OPD Visit | Correct department, doctor, notes |
| OPD Settings | Tokens, doctors, fee references |

## Doctors
*OPD ▸ OPD setup ▸ Doctors*: name, qualification, specialty, PMDC no., department. Doctors do **not** need a user account. The future Doctor Management module extends these records.
