# Licensing – for Burhan Nexus staff

## Design
- On setup completion a **30-day trial** starts and a random **Installation ID** (GUID) is created.
- Activation keys are signed with the **Burhan Nexus private key (ECDSA P-256 / SHA-256)**. The product contains only the public key (`EcdsaLicenseKeyVerifier.VendorPublicKeyPem`) – no secret is in the application or the browser.
- Key format: `BNHMS1-<base64url payload>.<base64url signature>`. The payload contains product, type (Annual/Lifetime), Installation ID, licensee, issue date, expiry (annual), serial.
- A key works only for the Installation ID it was issued for. Activation is fully **offline**.
- The licence state is stored in the `LicenseInformation` table, sealed (encrypted + authenticated) with server-side Data Protection keys in `data\keys` – editing the table makes the licence *verification required*, never grants a licence.
- Clock roll-back of more than 24 h is detected.
- Expired or invalid licence ⇒ **read-only mode** (view, backup, activation still work). Licensing **never deletes or modifies hospital data**.
- `ILicenseKeyVerifier` can be replaced later by an online activation service without touching the rest of the system.

## Generating a key
Requirements: .NET 10 SDK and the private key file `burhan-nexus-hms-license.private.pem` (keep it offline, never commit it, never send it to customers).

```bash
dotnet run --project tools/BurhanNexus.LicenseGenerator -- \
  --key path/to/burhan-nexus-hms-license.private.pem \
  --installation 4d35def3-93f8-475d-974a-d5e705637328 \
  --type Annual --years 1 --to "Al-Shifa Teaching Hospital, Peshawar"

dotnet run --project tools/BurhanNexus.LicenseGenerator -- --key ... --installation ... --type Lifetime --to "..."
```
The tool prints the key and verifies it against the public key built into the product. Send the key to the hospital; they paste it on *Settings ▸ License*.

If the private key is ever lost, a new key pair must be generated and the public key in `EcdsaLicenseKeyVerifier` replaced in a new release – keys issued with the old key would then stop verifying, so **back the private key up securely (two offline copies)**.
