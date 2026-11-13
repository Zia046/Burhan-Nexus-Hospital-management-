using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BurhanNexus.HMS.Infrastructure.Services;

public sealed record DepartmentListItem(int Id, string Name, string? Code, string? Description, bool IsActive, int UserCount, uint RowVersion);

public interface IDepartmentService
{
    Task<IReadOnlyList<DepartmentListItem>> ListAsync(bool includeInactive = true, CancellationToken ct = default);
    Task<ServiceResult<int>> CreateAsync(string name, string? code, string? description, CancellationToken ct = default);
    Task<ServiceResult> UpdateAsync(int id, string name, string? code, string? description, bool isActive, uint rowVersion, CancellationToken ct = default);
}

public sealed class DepartmentService : IDepartmentService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly TimeProvider _time;

    public DepartmentService(HmsDbContext db, IAuditService audit, TimeProvider time)
    {
        _db = db;
        _audit = audit;
        _time = time;
    }

    public async Task<IReadOnlyList<DepartmentListItem>> ListAsync(bool includeInactive = true, CancellationToken ct = default) =>
        await _db.Departments.AsNoTracking()
            .Where(d => includeInactive || d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentListItem(d.Id, d.Name, d.Code, d.Description, d.IsActive, d.Users.Count, d.RowVersion))
            .ToListAsync(ct);

    public async Task<ServiceResult<int>> CreateAsync(string name, string? code, string? description, CancellationToken ct = default)
    {
        var errors = Validate(name, code, description);
        if (errors.Count > 0) return ServiceResult<int>.Fail(errors.ToArray());
        var now = _time.GetUtcNow().UtcDateTime;
        var dept = new Department { Name = name.Trim(), Code = Clean(code)?.ToUpperInvariant(), Description = Clean(description), CreatedUtc = now, UpdatedUtc = now };
        if (await _db.Departments.AnyAsync(d => d.Name.ToLower() == dept.Name.ToLower(), ct))
            return ServiceResult<int>.Fail($"A department named '{dept.Name}' already exists.");
        _db.Departments.Add(dept);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            _db.ChangeTracker.Clear();
            return ServiceResult<int>.Fail("A department with the same name or code already exists.");
        }
        await _audit.LogAsync(AuditActions.DepartmentCreated, "Department", dept.Id.ToString(), $"Department '{dept.Name}' created",
            newValues: new { dept.Name, dept.Code, dept.Description }, ct: ct);
        return ServiceResult<int>.Ok(dept.Id);
    }

    public async Task<ServiceResult> UpdateAsync(int id, string name, string? code, string? description, bool isActive, uint rowVersion, CancellationToken ct = default)
    {
        var errors = Validate(name, code, description);
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());
        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dept is null) return ServiceResult.Fail("Department not found.");
        var trimmed = name.Trim();
        if (await _db.Departments.AnyAsync(d => d.Id != id && d.Name.ToLower() == trimmed.ToLower(), ct))
            return ServiceResult.Fail($"A department named '{trimmed}' already exists.");

        var old = new { dept.Name, dept.Code, dept.Description, dept.IsActive };
        _db.Entry(dept).Property(d => d.RowVersion).OriginalValue = rowVersion;
        dept.Name = trimmed;
        dept.Code = Clean(code)?.ToUpperInvariant();
        dept.Description = Clean(description);
        dept.IsActive = isActive;
        dept.UpdatedUtc = _time.GetUtcNow().UtcDateTime;
        _audit.Add(AuditActions.DepartmentUpdated, "Department", dept.Id.ToString(), $"Department '{dept.Name}' updated", old,
            new { dept.Name, dept.Code, dept.Description, dept.IsActive });
        try
        {
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return ServiceResult.Fail("This department was changed on another computer. Please reload and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            _db.ChangeTracker.Clear();
            return ServiceResult.Fail("A department with the same name or code already exists.");
        }
    }

    private static List<string> Validate(string name, string? code, string? description)
    {
        var e = new List<string>();
        if (string.IsNullOrWhiteSpace(name)) e.Add("Department name is required.");
        else if (name.Trim().Length > 100) e.Add("Department name must not exceed 100 characters.");
        if ((code?.Trim().Length ?? 0) > 20) e.Add("Code must not exceed 20 characters.");
        if ((description?.Trim().Length ?? 0) > 300) e.Add("Description must not exceed 300 characters.");
        return e;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public sealed record RoleListItem(int Id, string Name, string? Description, bool IsActive, int PermissionCount, int UserCount);

public interface IRoleService
{
    Task<IReadOnlyList<RoleListItem>> ListAsync(CancellationToken ct = default);
    Task<Role?> GetAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveAsync(int? id, string name, string? description, bool isActive, IReadOnlyCollection<string> permissionKeys, uint rowVersion, CancellationToken ct = default);
}

/// <summary>Responsibilities: admin-defined permission bundles.</summary>
public sealed class RoleService : IRoleService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IPermissionService _permissions;
    private readonly IRequestContext _ctx;
    private readonly TimeProvider _time;

    public RoleService(HmsDbContext db, IAuditService audit, IPermissionService permissions, IRequestContext ctx, TimeProvider time)
    {
        _db = db;
        _audit = audit;
        _permissions = permissions;
        _ctx = ctx;
        _time = time;
    }

    public async Task<IReadOnlyList<RoleListItem>> ListAsync(CancellationToken ct = default) =>
        await _db.Roles.AsNoTracking().OrderBy(r => r.Name)
            .Select(r => new RoleListItem(r.Id, r.Name, r.Description, r.IsActive, r.Permissions.Count, r.Users.Count))
            .ToListAsync(ct);

    public Task<Role?> GetAsync(int id, CancellationToken ct = default) =>
        _db.Roles.AsNoTracking().Include(r => r.Permissions).ThenInclude(p => p.Permission).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<ServiceResult<int>> SaveAsync(int? id, string name, string? description, bool isActive,
        IReadOnlyCollection<string> permissionKeys, uint rowVersion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return ServiceResult<int>.Fail("Responsibility name is required.");
        name = name.Trim();
        if (name.Length > 100) return ServiceResult<int>.Fail("Responsibility name must not exceed 100 characters.");
        if ((description?.Length ?? 0) > 300) return ServiceResult<int>.Fail("Description must not exceed 300 characters.");
        var keys = permissionKeys.Distinct().ToHashSet(StringComparer.Ordinal);
        if (keys.Any(k => !PermissionCatalog.AllKeys.Contains(k))) return ServiceResult<int>.Fail("Unknown permission selected.");
        if (await _db.Roles.AnyAsync(r => r.Id != (id ?? 0) && r.Name.ToLower() == name.ToLower(), ct))
            return ServiceResult<int>.Fail($"A responsibility named '{name}' already exists.");

        // Non-system administrators may only bundle permissions they hold themselves.
        if (_ctx.UserId is int actorId && !await _db.Users.AnyAsync(u => u.Id == actorId && u.IsSystemAdministrator, ct))
        {
            var mine = await _permissions.GetEffectivePermissionsAsync(actorId, ct);
            var existingKeys = id is null ? new HashSet<string>() :
                (await _db.RolePermissions.Where(rp => rp.RoleId == id).Select(rp => rp.Permission!.Key).ToListAsync(ct)).ToHashSet();
            var notAllowed = keys.Except(existingKeys).Where(k => !mine.Contains(k)).ToList();
            if (notAllowed.Count > 0) return ServiceResult<int>.Fail("You can only add permissions that you have yourself.");
            if (id is int rid && await _db.UserRoles.AnyAsync(ur => ur.RoleId == rid && ur.UserId == actorId, ct))
                return ServiceResult<int>.Fail("You cannot edit a responsibility that is assigned to you.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var permIds = await _db.Permissions.Where(p => keys.Contains(p.Key)).ToDictionaryAsync(p => p.Key, p => p.Id, ct);
        Role role;
        List<string> before;
        if (id is null)
        {
            role = new Role { CreatedUtc = now };
            _db.Roles.Add(role);
            before = new List<string>();
        }
        else
        {
            var found = await _db.Roles.Include(r => r.Permissions).ThenInclude(p => p.Permission).FirstOrDefaultAsync(r => r.Id == id, ct);
            if (found is null) return ServiceResult<int>.Fail("Responsibility not found.");
            role = found;
            _db.Entry(role).Property(r => r.RowVersion).OriginalValue = rowVersion;
            before = role.Permissions.Select(p => p.Permission!.Key).OrderBy(k => k).ToList();
        }

        var oldInfo = new { role.Name, role.Description, role.IsActive };
        role.Name = name;
        role.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        role.IsActive = isActive;
        role.UpdatedUtc = now;
        role.Permissions.RemoveAll(p => !keys.Contains(p.Permission?.Key ?? string.Empty));
        foreach (var key in keys.Where(k => !before.Contains(k)))
            role.Permissions.Add(new RolePermission { PermissionId = permIds[key] });

        var after = keys.OrderBy(k => k).ToList();
        if (id is null)
            _audit.Add(AuditActions.RoleCreated, "Role", null, $"Responsibility '{role.Name}' created", null,
                new { role.Name, role.Description, Permissions = after });
        else
        {
            _audit.Add(AuditActions.RoleUpdated, "Role", id.ToString(), $"Responsibility '{role.Name}' updated", oldInfo,
                new { role.Name, role.Description, role.IsActive });
            if (!before.SequenceEqual(after))
                _audit.Add(AuditActions.RolePermissionsChanged, "Role", id.ToString(), $"Permissions of responsibility '{role.Name}' changed",
                    new { Permissions = before }, new { Permissions = after });
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return ServiceResult<int>.Fail("This responsibility was changed on another computer. Please reload and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            _db.ChangeTracker.Clear();
            return ServiceResult<int>.Fail($"A responsibility named '{name}' already exists.");
        }
        return ServiceResult<int>.Ok(role.Id);
    }
}
