using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Security;

public interface IPermissionService
{
    /// <summary>Effective permissions = responsibilities ∪ individual grants − individual denials.</summary>
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(int userId, CancellationToken ct = default);
}

public sealed class PermissionService : IPermissionService
{
    private readonly HmsDbContext _db;

    public PermissionService(HmsDbContext db) => _db = db;

    public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(int userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.IsSystemAdministrator })
            .FirstOrDefaultAsync(ct);

        if (user is null || !user.IsActive) return new HashSet<string>();
        if (user.IsSystemAdministrator) return new HashSet<string>(PermissionCatalog.AllKeys, StringComparer.Ordinal);

        var fromRoles = await _db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.Role!.IsActive)
            .SelectMany(ur => ur.Role!.Permissions.Select(rp => rp.Permission!.Key))
            .ToListAsync(ct);

        var overrides = await _db.UserPermissions.AsNoTracking()
            .Where(up => up.UserId == userId)
            .Select(up => new { up.Permission!.Key, up.IsGranted })
            .ToListAsync(ct);

        var set = new HashSet<string>(fromRoles, StringComparer.Ordinal);
        foreach (var o in overrides.Where(o => o.IsGranted)) set.Add(o.Key);
        foreach (var o in overrides.Where(o => !o.IsGranted)) set.Remove(o.Key);
        set.IntersectWith(PermissionCatalog.AllKeys);
        return set;
    }
}

/// <summary>Synchronises the permission catalogue with the database (adds new permissions, never removes grants).</summary>
public static class PermissionSeeder
{
    public static async Task SyncAsync(HmsDbContext db, CancellationToken ct = default)
    {
        var existing = await db.Permissions.ToDictionaryAsync(p => p.Key, ct);
        foreach (var def in PermissionCatalog.All)
        {
            if (!existing.TryGetValue(def.Key, out var row))
            {
                db.Permissions.Add(new Core.Domain.Permission
                {
                    Key = def.Key, Module = def.Module, Action = def.Action,
                    ModuleName = def.ModuleName, ActionName = def.ActionName,
                    Description = def.Description, SortOrder = def.SortOrder
                });
            }
            else
            {
                row.ModuleName = def.ModuleName;
                row.ActionName = def.ActionName;
                row.Description = def.Description;
                row.SortOrder = def.SortOrder;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
