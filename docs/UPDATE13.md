# Update 13 – OPD fees, no default free services, lab New Visit, purchase-rate batches, licensing

## 1. OPD Setup
- **Add doctor** now has a **Consultation fee** field and a **FREE** tick box. Saving the doctor creates (or updates) that doctor's consultation service automatically (code `CON-{doctor id}`). There is no separate code/name fee entry.
- The doctors table shows each doctor's fee: the amount, **FREE**, or **Fee not set**.
- The **Service/Fee Reference** table was removed from OPD Setup. Other fees stay under **Fees & Services**.
- **OPD Settings** moved to **Settings → OPD** (`/Settings/Opd`, permission *OPD Settings*). The settings themselves are unchanged.

## 2. Private hospital – no service is free by default
- `ServiceItems.IsFree` (new column) marks a service as FREE. It is free **only** when an administrator marks it so.
- A fee of 0 without FREE means **"Fee not set"**. In Private mode such a service cannot be billed. The booking points refuse it with a clear message:
  - appointment / consultation
  - cashier billing
  - lab tests
  - radiology exams
  - IPD manual service charges
- Fee screens, booking screens and catalogues show the amount, **FREE** or **Fee not set**. Nothing is hardcoded. Receipts and reports still use the stored charge amounts, so they are unchanged.
- Government mode is unchanged.
- **Migration (existing hospitals):** any service with fee 0 that was already billed, or that an administrator had set to 0 (a fee record not created by the system), is marked FREE. Existing behaviour and data are preserved. Unpriced standard-catalogue items stay "Fee not set".

## 3 / 5 / 6. Laboratory → New Visit
- Only the **top 6** most frequently booked tests are shown, most frequent first. They are calculated from the last 90 days of bookings, with the standard common tests filling any gaps. Every other test stays hidden but is found by the search box.
- The **payment section** sits directly below the 6 suggestions. Its function is unchanged.
- Without a selected patient, the page lists **today's OPD patients** (up to 30, newest first): patients with an OPD visit today, or registered today. Each is a compact two-line card (name, then registration number). Clicking a card loads that patient.
- **Live search:** suggestions update on every keystroke (today's / admitted patients, the same rule as the routine search). Each suggestion shows the name and registration number. Use ↑/↓ and Enter, or click, to load the patient. Endpoint: `/Lab/Book?handler=Suggest&q=`.

## 4. Pharmacy – purchase rates are never mixed
- The batch number stays optional while the purchase rate equals the medicine's current purchase rate.
- If the purchase rate differs, a separate batch number is required. The server refuses the invoice with: *"…the purchase rate Rs. X differs from the current purchase rate Rs. Y. Enter a separate batch number…"*. On screen, the batch box turns red and becomes required as soon as the rate is changed.
- A batch keeps exactly one purchase rate. Receiving more of an existing batch at a different rate is refused, where previously the batch cost was overwritten. Batch-wise stock value and profit therefore stay exact.

## 7. Licensing
**Architecture**
- Keys are signed with ECDSA P-256 / SHA-256. The format is `BNHMS1-<payload>.<signature>`.
- The **private key** exists only at Burhan Nexus. The HMS contains only the **public key**, so a modified or self-made key always fails the signature check. Everything works offline.
- **Payload contents:**
  - licence ID (`BNL-yyyyMMdd-XXXXXXXX`)
  - type (Lifetime / Annual) and term (Lifetime / Yearly / Custom)
  - hospital name
  - issue date and expiry
  - Installation ID
  - optional **Machine ID**
- The licence state is stored sealed (encrypted and authenticated with the server's Data Protection keys) in `LicenseInformation`. Editing the database row is detected.
- **Clock protection:** the sealed state keeps the highest date ever seen. If the server clock is set back by more than 24 hours, the system becomes read-only until the date is corrected. Activating a key is refused while the clock is wrong.
- **Expiry:** Yearly and custom licences expire automatically and the system becomes read-only. All data is kept and backups still work. From 30 days before expiry, every page shows a warning and the licence screen shows a renewal notice.

**Customer side** (`Settings → License`)
- Shows the status, licence ID, type, hospital, activation and expiry dates, and days remaining.
- Shows the **Installation ID**, the **Machine ID** (a hash of the Windows MachineGuid) and a copyable **activation request code** `installation|machine`.
- **Activate / Renew:** enter the key. It is validated offline: signature, installation, machine, expiry, clock and revoked IDs.
- **Deactivate** (permission *License → Activate*, requires a reason and a confirmation, audited as `License.Deactivated`): the system becomes read-only and that licence ID can never be activated on this installation again. Entering a new key renews the licence.

**Vendor side** (`tools/BurhanNexus.LicenseGenerator`, `bnhms-license`)
- `bnhms-license` with no arguments starts interactive mode, which asks for the key file, request code, hospital, type and expiry.
- `bnhms-license keygen --out <folder>` creates a new key pair. It never overwrites an existing pair.
- `bnhms-license issue --key private.pem --request "<installation>|<machine>" --to "Hospital" --type Lifetime|Yearly|Custom [--years N] [--expires yyyy-MM-dd] [--customer "..."]`
- Every issued key is appended to `licenses.csv`, next to the private key: licence ID, generation date, hospital, customer, type, expiry date, Installation ID, Machine ID, status and key fingerprint. The key itself is not stored.
- `bnhms-license list` shows the register. `bnhms-license status --id <licence> --set Activated|Deactivated|Renewed|Cancelled` changes a status.
- The old command line (`--key --installation --type Annual|Lifetime --to`) still works.
- **The private key is never committed or shipped.**

## Installer fix – first start of the Windows service
Before starting the service, `Install-Server.ps1` now runs `BurhanNexus.HMS.Web.exe --migrate-only`, which prepares or upgrades the database and then exits. A long first-time migration therefore no longer exceeds Windows' 30-second service start limit. A slow start is also waited for, up to 2 minutes.

## Database
- Migration `Update13`: `ServiceItems.IsFree boolean NOT NULL DEFAULT false`, plus the data update described in section 2.
- No other schema changes. Licence fields live in the sealed licence state.
