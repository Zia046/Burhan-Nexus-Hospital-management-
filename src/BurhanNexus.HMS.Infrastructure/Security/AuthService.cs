using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Security;

public sealed record LoginSuccess(int UserId, string Username, string FullName, Guid SessionId, string SecurityStamp, bool MustChangePassword);

public sealed record LoginResult(bool Succeeded, string? Error, LoginSuccess? Session)
{
    public static LoginResult Fail(string error) => new(false, error, null);
}

public enum SessionCheck
{
    Valid,
    Expired,
    Revoked,
    NotFound
}

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default);
    Task<SessionCheck> ValidateSessionAsync(int userId, Guid sessionId, string securityStamp, bool touch, CancellationToken ct = default);
    Task LogoutAsync(Guid sessionId, string reason, CancellationToken ct = default);
    Task<ServiceResult> ChangeOwnPasswordAsync(int userId, Guid currentSessionId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task RevokeAllSessionsAsync(int userId, string reason, Guid? exceptSessionId = null, CancellationToken ct = default);
}

public sealed class AuthService : IAuthService
{
    public const string GenericLoginError = "Invalid username or password.";

    private readonly HmsDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly ISettingsService _settings;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly TimeProvider _time;

    public AuthService(HmsDbContext db, IPasswordService passwords, ISettingsService settings, IAuditService audit,
        IRequestContext ctx, TimeProvider time)
    {
        _db = db;
        _passwords = passwords;
        _settings = settings;
        _audit = audit;
        _ctx = ctx;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        username = (username ?? string.Empty).Trim();
        if (username.Length == 0 || string.IsNullOrEmpty(password))
            return LoginResult.Fail("Please enter your username and password.");
        if (username.Length > 64 || password.Length > 128)
            return LoginResult.Fail(GenericLoginError);

        var security = await _settings.GetSecurityAsync(ct);
        var normalized = User.Normalize(username);
        var user = await _db.Users.Include(u => u.Credential).FirstOrDefaultAsync(u => u.NormalizedUsername == normalized, ct);
        var now = Now;

        if (user?.Credential is null)
        {
            // Spend comparable time to avoid revealing which usernames exist.
            _ = _passwords.Hash(password);
            await RecordFailureAsync(null, username, "Unknown username", ct);
            return LoginResult.Fail(GenericLoginError);
        }

        var cred = user.Credential;
        if (cred.LockoutEndUtc is { } until && until > now)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((until - now).TotalMinutes));
            await RecordFailureAsync(user.Id, username, "Account locked", ct);
            return LoginResult.Fail($"Too many failed attempts. This account is locked for {minutes} more minute(s).");
        }

        if (!_passwords.Verify(cred.PasswordHash, password, out var needsRehash))
        {
            cred.FailedLoginCount++;
            string message = GenericLoginError;
            if (cred.FailedLoginCount >= security.MaxFailedLoginAttempts)
            {
                cred.LockoutEndUtc = now.AddMinutes(security.LockoutMinutes);
                cred.FailedLoginCount = 0;
                _audit.Add(AuditActions.AccountLocked, "User", user.Id.ToString(),
                    $"Account '{user.Username}' locked for {security.LockoutMinutes} minutes after repeated failed logins", success: false);
                message = $"Too many failed attempts. This account is locked for {security.LockoutMinutes} minute(s).";
            }
            await RecordFailureAsync(user.Id, username, "Wrong password", ct);
            return LoginResult.Fail(message);
        }

        if (!user.IsActive)
        {
            await RecordFailureAsync(user.Id, username, "Account inactive", ct);
            return LoginResult.Fail("This account is inactive. Please contact the administrator.");
        }

        if (needsRehash) cred.PasswordHash = _passwords.Hash(password);
        cred.FailedLoginCount = 0;
        cred.LockoutEndUtc = null;
        user.LastLoginUtc = now;

        var sessionId = Guid.NewGuid();
        _db.LoginHistory.Add(new LoginHistory
        {
            UserId = user.Id,
            UsernameAttempted = user.Username,
            AttemptedUtc = now,
            Success = true,
            IpAddress = _ctx.IpAddress,
            Workstation = AuditService.Truncate(_ctx.Workstation, 100),
            UserAgent = AuditService.Truncate(_ctx.UserAgent, 500),
            SessionId = sessionId,
            LastActivityUtc = now
        });
        _db.AuditLogs.Add(new AuditLog
        {
            TimestampUtc = now, UserId = user.Id, Username = user.Username,
            Action = AuditActions.LoginSucceeded, EntityType = "User", EntityId = user.Id.ToString(),
            Description = "User logged in", IpAddress = _ctx.IpAddress,
            Workstation = AuditService.Truncate(_ctx.Workstation, 100), UserAgent = AuditService.Truncate(_ctx.UserAgent, 500)
        });
        await _db.SaveChangesAsync(ct);

        return new LoginResult(true, null,
            new LoginSuccess(user.Id, user.Username, user.FullName, sessionId, cred.SecurityStamp, cred.MustChangePassword));
    }

    private async Task RecordFailureAsync(int? userId, string username, string reason, CancellationToken ct)
    {
        var now = Now;
        _db.LoginHistory.Add(new LoginHistory
        {
            UserId = userId,
            UsernameAttempted = AuditService.Truncate(username, 64)!,
            AttemptedUtc = now,
            Success = false,
            FailureReason = reason,
            IpAddress = _ctx.IpAddress,
            Workstation = AuditService.Truncate(_ctx.Workstation, 100),
            UserAgent = AuditService.Truncate(_ctx.UserAgent, 500)
        });
        _db.AuditLogs.Add(new AuditLog
        {
            TimestampUtc = now, UserId = userId, Username = AuditService.Truncate(username, 64),
            Action = AuditActions.LoginFailed, EntityType = "User", EntityId = userId?.ToString(),
            Description = $"Failed login: {reason}", IpAddress = _ctx.IpAddress,
            Workstation = AuditService.Truncate(_ctx.Workstation, 100), UserAgent = AuditService.Truncate(_ctx.UserAgent, 500),
            Success = false
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<SessionCheck> ValidateSessionAsync(int userId, Guid sessionId, string securityStamp, bool touch, CancellationToken ct = default)
    {
        var session = await _db.LoginHistory.FirstOrDefaultAsync(l => l.SessionId == sessionId && l.UserId == userId, ct);
        if (session is null) return SessionCheck.NotFound;
        if (session.LogoutUtc is not null) return SessionCheck.Revoked;

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, Stamp = u.Credential!.SecurityStamp })
            .FirstOrDefaultAsync(ct);

        var now = Now;
        if (user is null || !user.IsActive || user.Stamp != securityStamp)
        {
            session.LogoutUtc = now;
            session.LogoutReason = user is { IsActive: false } ? "Account deactivated" : "Session revoked";
            await _db.SaveChangesAsync(ct);
            return SessionCheck.Revoked;
        }

        var security = await _settings.GetSecurityAsync(ct);
        var last = session.LastActivityUtc ?? session.AttemptedUtc;
        if (now - last > TimeSpan.FromMinutes(security.SessionTimeoutMinutes))
        {
            session.LogoutUtc = now;
            session.LogoutReason = "Session timeout";
            _db.AuditLogs.Add(new AuditLog
            {
                TimestampUtc = now, UserId = userId, Username = session.UsernameAttempted,
                Action = AuditActions.SessionExpired, EntityType = "User", EntityId = userId.ToString(),
                Description = $"Session ended after {security.SessionTimeoutMinutes} minutes of inactivity",
                IpAddress = _ctx.IpAddress, Workstation = AuditService.Truncate(_ctx.Workstation, 100)
            });
            await _db.SaveChangesAsync(ct);
            return SessionCheck.Expired;
        }

        if (touch && now - last > TimeSpan.FromSeconds(30))
        {
            session.LastActivityUtc = now;
            await _db.SaveChangesAsync(ct);
        }
        return SessionCheck.Valid;
    }

    public async Task LogoutAsync(Guid sessionId, string reason, CancellationToken ct = default)
    {
        var session = await _db.LoginHistory.FirstOrDefaultAsync(l => l.SessionId == sessionId, ct);
        if (session is null || session.LogoutUtc is not null) return;
        session.LogoutUtc = Now;
        session.LogoutReason = reason;
        _audit.Add(AuditActions.Logout, "User", session.UserId?.ToString(), $"User logged out ({reason})");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ServiceResult> ChangeOwnPasswordAsync(int userId, Guid currentSessionId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Credential).FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user?.Credential is null) return ServiceResult.Fail("User not found.");
        if (!_passwords.Verify(user.Credential.PasswordHash, currentPassword ?? string.Empty, out _))
            return ServiceResult.Fail("Current password is incorrect.");
        if (currentPassword == newPassword) return ServiceResult.Fail("New password must be different from the current password.");
        var errors = _passwords.Validate(newPassword, user.Username);
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());

        user.Credential.PasswordHash = _passwords.Hash(newPassword);
        user.Credential.PasswordChangedUtc = Now;
        user.Credential.MustChangePassword = false;
        user.UpdatedUtc = Now;
        _audit.Add(AuditActions.PasswordChanged, "User", user.Id.ToString(), "User changed own password");
        await _db.SaveChangesAsync(ct);
        await RevokeAllSessionsAsync(userId, "Password changed", currentSessionId, ct);
        return ServiceResult.Ok();
    }

    public async Task RevokeAllSessionsAsync(int userId, string reason, Guid? exceptSessionId = null, CancellationToken ct = default)
    {
        var sessions = await _db.LoginHistory
            .Where(l => l.UserId == userId && l.Success && l.LogoutUtc == null && l.SessionId != null && l.SessionId != exceptSessionId)
            .ToListAsync(ct);
        foreach (var s in sessions)
        {
            s.LogoutUtc = Now;
            s.LogoutReason = reason;
        }
        if (sessions.Count > 0) await _db.SaveChangesAsync(ct);
    }
}
