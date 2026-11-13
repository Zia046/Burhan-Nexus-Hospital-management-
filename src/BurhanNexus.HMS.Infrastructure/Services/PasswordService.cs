using BurhanNexus.HMS.Core.Domain;
using Microsoft.AspNetCore.Identity;

namespace BurhanNexus.HMS.Infrastructure.Services;

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string hash, string password, out bool needsRehash);
    IReadOnlyList<string> Validate(string? password, string? username = null);
}

/// <summary>PBKDF2 (HMAC-SHA512, 100k+ iterations, random salt) via ASP.NET Core Identity's hasher.</summary>
public sealed class PasswordService : IPasswordService
{
    public const int MinimumLength = 8;
    private readonly PasswordHasher<User> _hasher = new();
    private static readonly User Dummy = new();

    public string Hash(string password) => _hasher.HashPassword(Dummy, password);

    public bool Verify(string hash, string password, out bool needsRehash)
    {
        needsRehash = false;
        if (string.IsNullOrEmpty(hash)) return false;
        var result = _hasher.VerifyHashedPassword(Dummy, hash, password);
        needsRehash = result == PasswordVerificationResult.SuccessRehashNeeded;
        return result != PasswordVerificationResult.Failed;
    }

    public IReadOnlyList<string> Validate(string? password, string? username = null)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Password is required.");
            return errors;
        }
        if (password.Length < MinimumLength) errors.Add($"Password must be at least {MinimumLength} characters long.");
        if (password.Length > 128) errors.Add("Password must not exceed 128 characters.");
        if (!password.Any(char.IsLetter)) errors.Add("Password must contain at least one letter.");
        if (!password.Any(char.IsDigit)) errors.Add("Password must contain at least one number.");
        if (!string.IsNullOrWhiteSpace(username) && password.Contains(username.Trim(), StringComparison.OrdinalIgnoreCase))
            errors.Add("Password must not contain the username.");
        return errors;
    }
}
