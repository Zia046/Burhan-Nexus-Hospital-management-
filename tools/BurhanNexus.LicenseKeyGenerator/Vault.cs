using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BurhanNexus.LicenseKeyGenerator;

/// <summary>
/// The signing key store of the License Key Generator. The P-256 private key is created HERE, on the vendor's computer, and is saved
/// only encrypted: AES-256-GCM with a key derived from the administrator password (PBKDF2-SHA256, 600 000 iterations).
/// The password is never stored; it is checked by decrypting the key. The public key (not secret) is stored in clear so it can be
/// handed to the HMS developers.
/// </summary>
internal sealed class Vault
{
    public const int Iterations = 600_000;

    public int V { get; set; } = 1;
    public string Username { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public int Pbkdf2Iterations { get; set; } = Iterations;
    public string Nonce { get; set; } = string.Empty;
    public string Cipher { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string PublicKeyPem { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }

    public static string Folder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BurhanNexus", "HMS License Key Generator");
    public static string VaultPath => Path.Combine(Folder, "signing-key.vault");
    public static string RegisterPath => Path.Combine(Folder, "licenses.csv");
    public static string IssuedFolder => Path.Combine(Folder, "issued");
    public static bool Exists => File.Exists(VaultPath);

    public static string KeyIdOf(string publicPem) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(publicPem.Replace("\r", "").Trim())))[..16];

    public static void ValidateNewPassword(string username, string password)
    {
        if (username.Trim().Length < 3) throw new ArgumentException("The administrator name needs at least 3 characters.");
        if (password.Length < 10) throw new ArgumentException("The password needs at least 10 characters.");
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) throw new ArgumentException("The password must contain letters and digits.");
    }

    /// <summary>Creates a NEW signing key pair, protected by the administrator password.</summary>
    public static (Vault Vault, string PrivatePem) Create(string username, string password)
    {
        ValidateNewPassword(username, password);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var priv = ecdsa.ExportPkcs8PrivateKeyPem();
        var pub = ecdsa.ExportSubjectPublicKeyInfoPem();
        var v = new Vault { Username = username.Trim(), PublicKeyPem = pub, KeyId = KeyIdOf(pub), CreatedUtc = DateTime.UtcNow };
        v.Seal(priv, password);
        return (v, priv);
    }

    private void Seal(string privatePem, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, 32);
        var plain = Encoding.UTF8.GetBytes(privatePem);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using (var gcm = new AesGcm(key, 16)) gcm.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(KeyId));
        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(plain);
        Salt = Convert.ToBase64String(salt); Nonce = Convert.ToBase64String(nonce);
        Cipher = Convert.ToBase64String(cipher); Tag = Convert.ToBase64String(tag);
        Pbkdf2Iterations = Iterations;
    }

    /// <summary>Returns the private key, or null when the name or password is wrong (or the file was altered).</summary>
    public string? Unlock(string username, string password)
    {
        if (!string.Equals(username.Trim(), Username, StringComparison.OrdinalIgnoreCase)) { Thread.Sleep(800); return null; }
        try
        {
            var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Convert.FromBase64String(Salt), Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);
            var cipher = Convert.FromBase64String(Cipher);
            var plain = new byte[cipher.Length];
            using (var gcm = new AesGcm(key, 16)) gcm.Decrypt(Convert.FromBase64String(Nonce), cipher, Convert.FromBase64String(Tag), plain, Encoding.UTF8.GetBytes(KeyId));
            CryptographicOperations.ZeroMemory(key);
            var pem = Encoding.UTF8.GetString(plain);
            CryptographicOperations.ZeroMemory(plain);
            // The decrypted private key must belong to the stored public key.
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(pem);
            if (KeyIdOf(ecdsa.ExportSubjectPublicKeyInfoPem()) != KeyId) return null;
            return pem;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            Thread.Sleep(800);
            return null;
        }
    }

    public void ChangePassword(string privatePem, string newPassword)
    {
        ValidateNewPassword(Username, newPassword);
        Seal(privatePem, newPassword);
    }

    public void Save(string? path = null)
    {
        path ??= VaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, path, overwrite: true);
    }

    public static Vault Load(string? path = null)
    {
        var v = JsonSerializer.Deserialize<Vault>(File.ReadAllText(path ?? VaultPath)) ?? throw new InvalidDataException("The key file is damaged.");
        if (v.V != 1 || string.IsNullOrEmpty(v.Cipher) || KeyIdOf(v.PublicKeyPem) != v.KeyId) throw new InvalidDataException("The key file is damaged or not a Burhan Nexus signing key.");
        return v;
    }

    /// <summary>Restores a backup (still encrypted) as this computer's signing key – the password of the backup is needed.</summary>
    public static Vault RestoreFrom(string backupPath, string username, string password)
    {
        var v = Load(backupPath);
        if (v.Unlock(username, password) is null) throw new ArgumentException("The administrator name or password of the backup is wrong.");
        v.Save();
        return v;
    }
}
