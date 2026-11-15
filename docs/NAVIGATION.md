# Administrator navigation – sidebar & top taskbar

This was a UI change only. It made no database change and no permission change, and every route is unchanged.

The menu is now defined once, in `Services/NavMenu.cs`:

- It has the same sections, links, permission checks and Private / Government conditions as before.
- `_Layout.cshtml` renders that menu in two ways:
  - **System administrator** (the Admin Dashboard): 11 pages in the sidebar. Every other link goes in the **top taskbar**, grouped under its existing category.
  - **All other users**: the same permission-based sidebar as before. They get no taskbar, and no admin page is shown to them.

The server still checks every page's permission as before.

## Sidebar (administrator)

| Existing category | Pages kept in the sidebar |
|---|---|
| Appointments (Private only) | New Appointment · Doctor-wise Lists |
| Patients & Visits | OPD · Emergency |
| Finance | My Financial Account · Services & Fees |
| Administration | Users · Departments · Audit Log |
| System | Hospital Settings · Backup & Restore |

## Top taskbar (administrator)

The taskbar starts with **Dashboard**, then one button per category. Clicking a category opens its menu.

| Category | Options in its menu |
|---|---|
| Appointments | Appointment Reports |
| Patients & Visits | Register Patient · Patient Search |
| Laboratory | Lab Dashboard · New Lab Visit · Sample Collection · Pending Tests · Verification · Lab Results · Lab Reports · Test Catalogue · Lab Settings |
| IPD & Maternity | IPD Dashboard · Admit Patient · Inpatients · Bed Map · Gynae & ANC · Labour Room · NICU · Nursing · Wards & Beds · IPD Settings |
| Radiology | Radiology Dashboard · New Booking · X-Ray Worklist · Ultrasound Worklist · Bookings · Reporting · Report Verification · Patient History · Radiology Reports · Services & Templates · Radiology Settings |
| Pharmacy | Pharmacy Dashboard · Patient Issue · Retail Sale (Private) · Receive Stock · Stock Transfer · Stock & Batches · Medicines |
| Finance | Collect Payment · Receipts · Financial Dashboard · Cash Deposits · Doctor Accounts |
| Administration | Responsibilities · Login History |
| System | License |

Each page appears only once: either in the sidebar or in the taskbar.

How the taskbar behaves:

- The category of the current page and the page itself are highlighted.
- One menu is open at a time. A click outside the menu, or Escape, closes it.
- On tablets the categories wrap onto more lines.
- On phones the bar scrolls sideways and the menu opens across the full width.

Nothing had to stay behind: every existing item could be moved without affecting how it works.

Pages that were never in the sidebar are still reached from their module's own sub-menu as before, for example Finance → Counters / Closing / Refunds.
