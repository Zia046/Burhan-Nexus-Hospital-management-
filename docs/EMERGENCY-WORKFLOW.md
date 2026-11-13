# Emergency workflow (Phase 2)

Minor OT / emergency staff do not need a computer. Reception registers the emergency and prints a slip that goes with the patient.

```
Patient / relatives arrive ─▶ Reception: existing patient? select : register (minimum details)
                           ─▶ Emergency registration ─▶ ER number ─▶ Emergency slip prints
                           ─▶ Patient shifted to Minor OT / treatment area ─▶ staff write on the slip
```

## Registering an emergency
*Emergency ▸ Emergency registration* (or *Emergency* on a patient profile / search result).

- **Existing patient**: search and *Select* – the same Patient ID is used.
- **New patient**: only **gender** and **name** are needed. Age, address etc. are optional and can be completed later.
- **Unconscious / unidentified**: tick *Unconscious / unidentified patient* – the patient is registered as *Unidentified Male/Female* with a permanent Patient ID, marked as a temporary registration. Later use *Complete patient details* (permission *Edit Patient Information*); the Patient ID and history stay the same.

Emergency details (all optional): arrival date & time (defaults to now; can be adjusted up to 48 h back), category (RTA, burn, poisoning, …), brief complaint, brought by, attendant name & contact, referred by, medico-legal case (MLC), notes.

**Register & print slip** creates a unique Emergency number (`ER-2026-000045`) and opens the slip.

## Emergency slip (A4 default, A5 available)
Hospital name & logo, Emergency number, Patient ID, name, age & gender, arrival time, contact, address, category, complaint, brought by, attendant, referral, MLC mark – then large blank boxes for:
Initial assessment (BP, pulse, temp, RR, SpO₂, GCS, BSR, weight) · Diagnosis · Treatment/medicines · Injections · IV infusion · Dressing/stitches · Investigations · Referral instructions · Discharge instructions · outcome and medical officer signature.
See `docs/samples/sample-emergency-slip-A4.pdf`.

## Status management
Open the emergency (*Emergency ▸ Open*) ▸ *Update status*:

| Status | Who | Reason |
|--------|-----|--------|
| Under Treatment, Discharged | *Update Status* permission | optional |
| Referred | *Update Status* permission | **required** |
| Admitted (reserved for future IPD), Closed, Re-open | *Close / Admit / Re-open* permission (restricted) | **required** |

Every change is stored in the status history (previous status, new status, user, date & time, reason) and in the audit log. Users without the permission do not see the options and the server rejects direct requests.

The *Emergency* list shows **active patients** (Registered / Under Treatment) by default, with tabs for Referred, Discharged, Admitted, Closed, All and filters by date and category.

## Permissions (Emergency Management)
| Permission | Allows |
|------------|--------|
| View Emergency Patients | Lists and details |
| Emergency Registration | Register emergencies |
| Emergency Slip Printing | Print / reprint slips |
| Edit Emergency Details | Category, complaint, attendant, notes |
| Update Status | Under treatment, referred, discharged |
| Close / Admit / Re-open | Restricted statuses |
| Emergency Settings | Emergency categories |
