# Burhan Nexus HMS License Key Generator – key procedure (for Burhan Nexus only)

This document is **not** part of the HMS installers. The generator program (`BurhanNexus.HMS.LicenseKeyGenerator.exe`) is
published only as the separate CI artifact `BurhanNexus-HMS-LicenseKeyGenerator`.

## How the security works
* Every activation key is a JSON licence (product, hospital, customer ID, installation ID, optional Machine ID, type, dates,
  licence ID) **digitally signed** with an ECDSA P-256 private key (SHA-256).
* The HMS contains **only public keys** (`EcdsaLicenseKeyVerifier.TrustedPublicKeys`). It checks the signature offline.
  Changing one character of a key, the database or a config file cannot create a valid licence – only the private key can sign.
* The licence state in the database is additionally sealed with ASP.NET Data Protection; a server clock moved back by
  more than 24 hours switches the HMS to read-only. (No offline program can guarantee tamper-proof time – this only detects it.)
* Licence problems never delete data; the HMS only becomes read-only.

## First start (on the Burhan Nexus administrator's own computer)
1. Start the generator. Choose an administrator name and a strong password (≥ 10 characters, letters and digits).
2. The generator creates a NEW P-256 key pair **on this computer**. The private key is saved only encrypted
   (AES-256-GCM, key derived from the password with PBKDF2-SHA256, 600 000 iterations) in
   `%LOCALAPPDATA%\BurhanNexus\HMS License Key Generator\signing-key.vault`. The password is never stored.
3. **Key & backup → Backup private key (encrypted)** to a USB drive. Keep the USB drive and the password in two different safe places.
   Without the backup and the password a lost computer means a new key (and new keys for every customer).
4. **Key & backup → Save public key** and send only that `.pem` file to the HMS developer. It is added to
   `TrustedPublicKeys` in the next HMS update; the original public key stays, so existing licences keep working.
   Never send the private key or the `.vault` file to anyone.

## Generating a licence
1. The hospital opens *Settings → License & Activation* and sends the **activation request code** (Installation ID | Machine ID).
2. Enter the hospital name, customer / registration ID, paste the request code, choose Trial / 1 Year / Lifetime,
   the activation date and (for Trial and 1 Year) the expiry date. Lifetime licences never expire.
3. *Generate License Key* → *Copy License Key* or *Save License*. Every key is recorded in the licence history
   (`licenses.csv`, plus a copy of the key in `issued\`).

## Revoking a licence
History → select → *Revoke licence*, then *Export revocation list*. The hospital imports the file on its licence page;
the revoked licence becomes read-only and can never be activated there again. A list signed by another key is refused.

## Future products
The signed payload carries a product code (`BNHMS` for the HMS). A future Burhan Nexus product uses its own product code
and verification code, so keys of one product never activate another.
