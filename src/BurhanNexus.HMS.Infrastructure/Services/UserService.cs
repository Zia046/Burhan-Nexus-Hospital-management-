using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BurhanNexus.HMS.Infrastructure.Services;

public sealed record UserListItem(
    int Id, string FullName, string Username, string? Designation, bool IsActive, bool IsSystemAdministrator,
    bool IsLocked, DateTime? LastLoginUtc, IReadOnlyList<string> Departments, IReadOnlyList<string> Roles);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed class UserInput
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
}

public enum PermissionOverride { Inherit, Grant, Deny }

public sealed record PermissionCell(string Key, string Action, string ActionName, string Description,
    PermissionOverride Override, bool FromResponsibilities, bool Effective, bool ActorMayChange);

public sealed record PermissionModuleRow(string ModuleKey, string ModuleName, string Description, bool IsAvailable, IReadOnlyList<PermissionCell> Cells);

public interface IUserService
{
    Task<PagedResult<UserListItem>> SearchAsync(string? query, bool? active, int? departmentId, int page, int pageSize, CancellationToken ct = default);
    Task<User?> GetAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<int>> CreateAsync(UserInput input, string password, bool mustChangePassword, IEnumerable<int> departmentIds, CancellationToken ct = default);
    Task<ServiceResult> UpdateAsync(int id, UserInput input, uint rowVersion, CancellationToken ct = default);
    Task<ServiceResult> SetActiveAsync(int id, bool active, CancellationToken ct = default);
    Task<ServiceResult> ResetPasswordAsync(int id, string newPassword, bool mustChange, CancellationToken ct = default);
    Task<ServiceResult> UnlockAsync(int id, CancellationToken ct = default);
    Task<ServiceResult> SetDepartmentsAsync(int id, IEnumerable<int> departmentIds, CancellationToken ct = default);
    Task<ServiceResult> SetRolesAsync(int id, IEnumerable<int> roleIds, CancellationToken ct = default);
    Task<IReadOnlyList<PermissionModuleRow>> GetPermissionMatrixAsync(int id, CancellationToken ct = default);
    Task<ServiceResult> SetPermissionOverridesAsync(int id, IReadOnlyDictionary<string, PermissionOverride> overrides, CancellationToken ct = default);
    Task<(int Total, int Active, int Inactive)> CountsAsync(CancellationToken ct = default);
}

public sealed class UserService : IUserService
{
    private readonly HmsDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IPermissionService _permissions;
    private readonly IAuthService _auth;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly TimeProvider _time;

    public UserService(HmsDbContext db, IPasswordService passwords, IPermissionService permissions, IAuthService auth,
        IAuditService audit, IRequestContext ctx, TimeProvider time)
    {
        _db = db;
        _passwords = passwords;
        _permissions = permissions;
        _auth = auth;
        _audit = audit;
        _ctx = ctx;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<UserListItem>> SearchAsync(string? query, bool? active, int? departmentId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);
        var q = _db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = "%" + query.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            q = q.Where(u => EF.Functions.ILike(u.FullName, term) || EF.Functions.ILike(u.Username, term)
                             || (u.Designation != null && EF.Functions.ILike(u.Designation, term))
                             || (u.ContactNumber != null && EF.Functions.ILike(u.ContactNumber, term)));
        }
        if (active.HasValue) q = q.Where(u => u.IsActive == active.Value);
        if (departmentId.HasValue) q = q.Where(u => u.Departments.Any(d => d.DepartmentId == departmentId.Value));

        var total = await q.CountAsync(ct);
        var now = Now;
        var rows = await q.OrderByDescending(u => u.IsSystemAdministrator).ThenBy(u => u.FullName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new
            {
                u.Id, u.FullName, u.Username, u.Designation, u.IsActive, u.IsSystemAdministrator, u.LastLoginUtc,
                Locked = u.Credential != null && u.Credential.LockoutEndUtc != null && u.Credential.LockoutEndUtc > now,
                Departments = u.Departments.Select(d => d.Department!.Name).OrderBy(n => n).ToList(),
                Roles = u.Roles.Select(r => r.Role!.Name).OrderBy(n => n).ToList()
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new UserListItem(r.Id, r.FullName, r.Username, r.Designation, r.IsActive, r.IsSystemAdministrator,
            r.Locked, r.LastLoginUtc, r.Departments, r.Roles)).ToList();
        return new PagedResult<UserListItem>(items, total, page, pageSize);
    }

    public Task<User?> GetAsync(int id, CancellationToken ct = default) =>
        _db.Users.AsNoTracking()
            .Include(u => u.Credential)
            .Include(u => u.Departments).ThenInclude(d => d.Department)
            .Include(u => u.Roles).ThenInclude(r => r.Role)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<(int Total, int Active, int Inactive)> CountsAsync(CancellationToken ct = default)
    {
        var total = await _db.Users.CountAsync(ct);
        var active = await _db.Users.CountAsync(u => u.IsActive, ct);
        return (total, active, total - active);
    }

    public async Task<ServiceResult<int>> CreateAsync(UserInput input, string password, bool mustChangePassword, IEnumerable<int> departmentIds, CancellationToken ct = default)
    {
        var errors = ValidateInput(input);
        errors.AddRange(_passwords.Validate(password, input.Username));
        if (errors.Count > 0) return ServiceResult<int>.Fail(errors.ToArray());

        var normalized = User.Normalize(input.Username);
        if (await _db.Users.AnyAsync(u => u.NormalizedUsername == normalized, ct))
            return ServiceResult<int>.Fail($"The username '{input.Username.Trim()}' is already in use.");

        var deptIds = departmentIds.Distinct().ToList();
        var validDepts = await _db.Departments.Where(d => deptIds.Contains(d.Id) && d.IsActive).Select(d => d.Id).ToListAsync(ct);
        if (validDepts.Count != deptIds.Count) return ServiceResult<int>.Fail("One or more selected departments are not available.");

        var now = Now;
        var user = new User
        {
            FullName = input.FullName.Trim(),
            Username = input.Username.Trim(),
            NormalizedUsername = normalized,
            Designation = Clean(input.Designation),
            ContactNumber = Clean(input.ContactNumber),
            Email = Clean(input.Email),
            IsActive = true,
            CreatedUtc = now,
            CreatedByUserId = _ctx.UserId,
            UpdatedUtc = now,
            Credential = new UserCredential
            {
                PasswordHash = _passwords.Hash(password),
                PasswordChangedUtc = now,
                MustChangePassword = mustChangePassword
            }
        };
        foreach (var d in validDepts)
            user.Departments.Add(new UserDepartment { DepartmentId = d, AssignedUtc = now, AssignedByUserId = _ctx.UserId });
        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            _db.ChangeTracker.Clear();
            return ServiceResult<int>.Fail($"The username '{input.Username.Trim()}' is already in use.");
        }

        await _audit.LogAsync(AuditActions.UserCreated, "User", user.Id.ToString(), $"User '{user.Username}' created",
            newValues: new { user.FullName, user.Username, user.Designation, user.ContactNumber, user.Email, Departments = validDepts, MustChangePassword = mustChangePassword },
            ct: ct);
        return ServiceResult<int>.Ok(user.Id);
    }

    public async Task<ServiceResult> UpdateAsync(int id, UserInput input, uint rowVersion, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return ServiceResult.Fail("User not found.");
        var guard = await GuardTargetAsync(user, allowSelf: true, ct);
        if (!guard.Succeeded) return guard;

        var errors = ValidateInput(input);
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());

        var normalized = User.Normalize(input.Username);
        if (await _db.Users.AnyAsync(u => u.Id != id && u.NormalizedUsername == normalized, ct))
            return ServiceResult.Fail($"The username '{input.Username.Trim()}' is already in use.");

        var old = new { user.FullName, user.Username, user.Designation, user.ContactNumber, user.Email };
        _db.Entry(user).Property(u => u.RowVersion).OriginalValue = rowVersion;
        user.FullName = input.FullName.Trim();
        user.Username = input.Username.Trim();
        user.NormalizedUsername = normalized;
        user.Designation = Clean(input.Designation);
        user.ContactNumber = Clean(input.ContactNumber);
        user.Email = Clean(input.Email);
        user.UpdatedUtc = Now;
        _audit.Add(AuditActions.UserUpdated, "User", user.Id.ToString(), $"User '{user.Username}' updated", old,
            new { user.FullName, user.Username, user.Designation, user.ContactNumber, user.Email });
        return await SaveAsync(ct);
    }

    public async Task<ServiceResult> SetActiveAsync(int id, bool active, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Credential).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return ServiceResult.Fail("User not found.");
        if (user.Id == _ctx.UserId) return ServiceResult.Fail("You cannot change the status of your own account.");
        if (user.IsSystemAdministrator && !active) return ServiceResult.Fail("The primary system administrator account cannot be deactivated.");
        var guard = await GuardTargetAsync(user, allowSelf: false, ct);
        if (!guard.Succeeded) return guard;
        if (user.IsActive == active) return ServiceResult.Ok();

        user.IsActive = active;
        user.UpdatedUtc = Now;
        if (!active && user.Credential is not null) user.Credential.SecurityStamp = Guid.NewGuid().ToString("N");
        _audit.Add(active ? AuditActions.UserActivated : AuditActions.UserDeactivated, "User", user.Id.ToString(),
            $"User '{user.Username}' {(active ? "activated" : "deactivated")}", new { IsActive = !active }, new { IsActive = active });
        var result = await SaveAsync(ct);
        if (result.Succeeded && !active) await _auth.RevokeAllSessionsAsync(user.Id, "Account deactivated", null, ct);
        return result;
    }

    public async Task<ServiceResult> ResetPasswordAsync(int id, string newPassword, bool mustChange, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Credential).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user?.Credential is null) return ServiceResult.Fail("User not found.");
        if (user.Id == _ctx.UserId) return ServiceResult.Fail("Use 'Change password' to change your own password.");
        var guard = await GuardTargetAsync(user, allowSelf: false, ct);
        if (!guard.Succeeded) return guard;

        var errors = _passwords.Validate(newPassword, user.Username);
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());

        user.Credential.PasswordHash = _passwords.Hash(newPassword);
        user.Credential.PasswordChangedUtc = Now;
        user.Credential.MustChangePassword = mustChange;
        user.Credential.FailedLoginCount = 0;
        user.Credential.LockoutEndUtc = null;
        user.Credential.SecurityStamp = Guid.NewGuid().ToString("N");
        user.UpdatedUtc = Now;
        _audit.Add(AuditActions.UserPasswordReset, "User", user.Id.ToString(),
            $"Password reset for '{user.Username}'" + (mustChange ? " (must change at next login)" : ""));
        var result = await SaveAsync(ct);
        if (result.Succeeded) await _auth.RevokeAllSessionsAsync(user.Id, "Password reset by administrator", null, ct);
        return result;
    }

    public async Task<ServiceResult> UnlockAsync(int id, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Credential).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user?.Credential is null) return ServiceResult.Fail("User not found.");
        var guard = await GuardTargetAsync(user, allowSelf: false, ct);
        if (!guard.Succeeded) return guard;
        user.Credential.LockoutEndUtc = null;
        user.Credential.FailedLoginCount = 0;
        _audit.Add(AuditActions.UserUnlocked, "User", user.Id.ToString(), $"Account '{user.Username}' unlocked");
        return await SaveAsync(ct);
    }

    public async Task<ServiceResult> SetDepartmentsAsync(int id, IEnumerable<int> departmentIds, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Departments).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return ServiceResult.Fail("User not found.");
        var guard = await GuardTargetAsync(user, allowSelf: true, ct);
        if (!guard.Succeeded) return guard;

        var wanted = departmentIds.Distinct().ToHashSet();
        var valid = await _db.Departments.Where(d => wanted.Contains(d.Id)).Select(d => d.Id).ToListAsync(ct);
        if (valid.Count != wanted.Count) return ServiceResult.Fail("One or more selected departments do not exist.");

        var before = user.Departments.Select(d => d.DepartmentId).OrderBy(x => x).ToList();
        user.Departments.RemoveAll(d => !wanted.Contains(d.DepartmentId));
        foreach (var d in wanted.Where(w => user.Departments.All(x => x.DepartmentId != w)))
            user.Departments.Add(new UserDepartment { UserId = user.Id, DepartmentId = d, AssignedUtc = Now, AssignedByUserId = _ctx.UserId });
        var after = wanted.OrderBy(x => x).ToList();
        if (before.SequenceEqual(after)) return ServiceResult.Ok();

        user.UpdatedUtc = Now;
        _audit.Add(AuditActions.UserDepartmentsChanged, "User", user.Id.ToString(), $"Departments changed for '{user.Username}'",
            new { Departments = await DepartmentNames(before, ct) }, new { Departments = await DepartmentNames(after, ct) });
        return await SaveAsync(ct);
    }

    public async Task<ServiceResult> SetRolesAsync(int id, IEnumerable<int> roleIds, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return ServiceResult.Fail("User not found.");
        if (user.Id == _ctx.UserId) return ServiceResult.Fail("You cannot change your own responsibilities or permissions.");
        var guard = await GuardTargetAsync(user, allowSelf: false, ct);
        if (!guard.Succeeded) return guard;

        var wanted = roleIds.Distinct().ToHashSet();
        var roles = await _db.Roles.Include(r => r.Permissions).ThenInclude(p => p.Permission)
            .Where(r => wanted.Contains(r.Id)).ToListAsync(ct);
        if (roles.Count != wanted.Count) return ServiceResult.Fail("One or more selected responsibilities do not exist.");

        var before = user.Roles.Select(r => r.RoleId).ToHashSet();
        var actor = await ActorAsync(ct);
        if (!actor.IsSystemAdministrator)
        {
            foreach (var role in roles.Where(r => !before.Contains(r.Id)))
            {
                var missing = role.Permissions.Select(p => p.Permission!.Key).Where(k => !actor.Permissions.Contains(k)).ToList();
                if (missing.Count > 0)
                    return ServiceResult.Fail($"You cannot assign the responsibility '{role.Name}' because it includes permissions you do not have.");
            }
        }

        user.Roles.RemoveAll(r => !wanted.Contains(r.RoleId));
        foreach (var r in wanted.Where(w => !before.Contains(w)))
            user.Roles.Add(new UserRole { UserId = user.Id, RoleId = r, AssignedUtc = Now, AssignedByUserId = _ctx.UserId });
        if (before.SetEquals(wanted)) return ServiceResult.Ok();

        var names = await _db.Roles.Where(r => before.Contains(r.Id) || wanted.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        user.UpdatedUtc = Now;
        _audit.Add(AuditActions.UserRolesChanged, "User", user.Id.ToString(), $"Responsibilities changed for '{user.Username}'",
            new { Responsibilities = before.Select(i => names.GetValueOrDefault(i)).OrderBy(n => n).ToList() },
            new { Responsibilities = wanted.Select(i => names.GetValueOrDefault(i)).OrderBy(n => n).ToList() });
        return await SaveAsync(ct);
    }

    public async Task<IReadOnlyList<PermissionModuleRow>> GetPermissionMatrixAsync(int id, CancellationToken ct = default)
    {
        var overrides = await _db.UserPermissions.AsNoTracking().Where(p => p.UserId == id)
            .Select(p => new { p.Permission!.Key, p.IsGranted }).ToDictionaryAsync(p => p.Key, p => p.IsGranted, ct);
        var fromRoles = (await _db.UserRoles.AsNoTracking().Where(r => r.UserId == id && r.Role!.IsActive)
            .SelectMany(r => r.Role!.Permissions.Select(p => p.Permission!.Key)).ToListAsync(ct)).ToHashSet();
        var effective = await _permissions.GetEffectivePermissionsAsync(id, ct);
        var actor = await ActorAsync(ct);

        return PermissionCatalog.Modules.Select(m => new PermissionModuleRow(m.Key, m.Name, m.Description, m.IsAvailable,
            m.Actions.Select(a =>
            {
                var key = m.PermissionKey(a.Key);
                var ov = overrides.TryGetValue(key, out var g) ? (g ? PermissionOverride.Grant : PermissionOverride.Deny) : PermissionOverride.Inherit;
                return new PermissionCell(key, a.Key, a.Name, a.Description, ov, fromRoles.Contains(key), effective.Contains(key),
                    actor.IsSystemAdministrator || actor.Permissions.Contains(key));
            }).ToList())).ToList();
    }

    public async Task<ServiceResult> SetPermissionOverridesAsync(int id, IReadOnlyDictionary<string, PermissionOverride> overrides, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Permissions).ThenInclude(p => p.Permission).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return ServiceResult.Fail("User not found.");
        if (user.Id == _ctx.UserId) return ServiceResult.Fail("You cannot change your own responsibilities or permissions.");
        if (user.IsSystemAdministrator) return ServiceResult.Fail("The primary system administrator always has every permission.");
        var guard = await GuardTargetAsync(user, allowSelf: false, ct);
        if (!guard.Succeeded) return guard;

        var unknown = overrides.Keys.Where(k => !PermissionCatalog.AllKeys.Contains(k)).ToList();
        if (unknown.Count > 0) return ServiceResult.Fail("Unknown permission: " + string.Join(", ", unknown));

        var actor = await ActorAsync(ct);
        var current = user.Permissions.ToDictionary(p => p.Permission!.Key,
            p => p.IsGranted ? PermissionOverride.Grant : PermissionOverride.Deny);
        var changed = overrides.Where(kv => (current.TryGetValue(kv.Key, out var c) ? c : PermissionOverride.Inherit) != kv.Value).ToList();
        if (changed.Count == 0) return ServiceResult.Ok();

        if (!actor.IsSystemAdministrator)
        {
            var notAllowed = changed.Where(kv => !actor.Permissions.Contains(kv.Key)).Select(kv => kv.Key).ToList();
            if (notAllowed.Count > 0)
                return ServiceResult.Fail("You can only assign permissions that you have yourself: " + string.Join(", ", notAllowed));
        }

        var permIds = await _db.Permissions.ToDictionaryAsync(p => p.Key, p => p.Id, ct);
        var now = Now;
        foreach (var (key, value) in changed)
        {
            var existing = user.Permissions.FirstOrDefault(p => p.Permission!.Key == key);
            if (value == PermissionOverride.Inherit)
            {
                if (existing is not null) user.Permissions.Remove(existing);
            }
            else if (existing is null)
            {
                user.Permissions.Add(new UserPermission
                {
                    UserId = user.Id, PermissionId = permIds[key], IsGranted = value == PermissionOverride.Grant,
                    AssignedUtc = now, AssignedByUserId = _ctx.UserId
                });
            }
            else
            {
                existing.IsGranted = value == PermissionOverride.Grant;
                existing.AssignedUtc = now;
                existing.AssignedByUserId = _ctx.UserId;
            }
        }

        user.UpdatedUtc = now;
        _audit.Add(AuditActions.UserPermissionsChanged, "User", user.Id.ToString(), $"Individual permissions changed for '{user.Username}'",
            changed.ToDictionary(kv => kv.Key, kv => (current.TryGetValue(kv.Key, out var c) ? c : PermissionOverride.Inherit).ToString()),
            changed.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()));
        return await SaveAsync(ct);
    }

    private sealed record Actor(bool IsSystemAdministrator, IReadOnlySet<string> Permissions);

    private async Task<Actor> ActorAsync(CancellationToken ct)
    {
        if (_ctx.UserId is not int actorId) return new Actor(false, new HashSet<string>());
        var isSys = await _db.Users.AnyAsync(u => u.Id == actorId && u.IsSystemAdministrator && u.IsActive, ct);
        return new Actor(isSys, await _permissions.GetEffectivePermissionsAsync(actorId, ct));
    }

    /// <summary>Only the primary system administrator may modify the primary system administrator account.</summary>
    private async Task<ServiceResult> GuardTargetAsync(User target, bool allowSelf, CancellationToken ct)
    {
        if (!allowSelf && target.Id == _ctx.UserId) return ServiceResult.Fail("You cannot perform this action on your own account.");
        if (target.IsSystemAdministrator && target.Id != _ctx.UserId)
        {
            var actor = await ActorAsync(ct);
            if (!actor.IsSystemAdministrator) return ServiceResult.Fail("Only the primary system administrator can change this account.");
        }
        return ServiceResult.Ok();
    }

    private async Task<ServiceResult> SaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return ServiceResult.Fail("This record was changed by another user on another computer. Please reload and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            _db.ChangeTracker.Clear();
            return ServiceResult.Fail("A record with the same name already exists.");
        }
    }

    private async Task<List<string>> DepartmentNames(IEnumerable<int> ids, CancellationToken ct)
    {
        var list = ids.ToList();
        return await _db.Departments.Where(d => list.Contains(d.Id)).OrderBy(d => d.Name).Select(d => d.Name).ToListAsync(ct);
    }

    private static List<string> ValidateInput(UserInput i)
    {
        var e = new List<string>();
        if (string.IsNullOrWhiteSpace(i.FullName)) e.Add("Full name is required.");
        else if (i.FullName.Trim().Length > 150) e.Add("Full name must not exceed 150 characters.");
        if (string.IsNullOrWhiteSpace(i.Username)) e.Add("Username is required.");
        else if (!Validation.IsValidUsername(i.Username)) e.Add("Username may contain only letters, numbers, dot, underscore and hyphen (3–64 characters).");
        if ((i.Designation?.Trim().Length ?? 0) > 100) e.Add("Designation must not exceed 100 characters.");
        if (!string.IsNullOrWhiteSpace(i.ContactNumber) && !Validation.IsValidPhone(i.ContactNumber)) e.Add("Contact number is not valid.");
        if (!string.IsNullOrWhiteSpace(i.Email) && !Validation.IsValidEmail(i.Email)) e.Add("Email address is not valid.");
        return e;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
