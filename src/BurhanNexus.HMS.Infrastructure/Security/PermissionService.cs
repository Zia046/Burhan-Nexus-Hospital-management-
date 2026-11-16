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
        var added = new List<Core.Domain.Permission>();
        foreach (var def in PermissionCatalog.All)
        {
            if (!existing.TryGetValue(def.Key, out var row))
            {
                added.Add(new Core.Domain.Permission
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
        db.Permissions.AddRange(added);
        await db.SaveChangesAsync(ct);

        // A new permission that splits an existing task is given once, when it first appears, to whoever already had that task
        // (so an update never stops staff from doing their work). Nothing else is granted automatically.
        foreach (var p in added)
        {
            if (!Inherits.TryGetValue(p.Key, out var from) || !existing.TryGetValue(from, out var source)) continue;
            var users = await db.UserPermissions.Where(u => u.PermissionId == source.Id && u.IsGranted).ToListAsync(ct);
            foreach (var u in users)
                db.UserPermissions.Add(new Core.Domain.UserPermission { UserId = u.UserId, PermissionId = p.Id, IsGranted = true, AssignedUtc = u.AssignedUtc, AssignedByUserId = u.AssignedByUserId });
            var roles = await db.RolePermissions.Where(r => r.PermissionId == source.Id).ToListAsync(ct);
            foreach (var r in roles) db.RolePermissions.Add(new Core.Domain.RolePermission { RoleId = r.RoleId, PermissionId = p.Id });
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>New permission → existing permission whose holders receive it once (update 9).</summary>
    private static readonly Dictionary<string, string> Inherits = new()
    {
        [Perm.AppointmentsCollectFee] = Perm.AppointmentsAdd,
        [Perm.IpdRequestMedicines] = Perm.IpdRecordMedication,
        [Perm.PharmProcessWardRequests] = Perm.PharmIssueToPatients,
        // update 10: whoever managed service fees in Finance manages the central Fees & Services catalogue
        [Perm.SvcViewFees] = Perm.FinanceManageServiceFees,
        [Perm.SvcManageCatalogue] = Perm.FinanceManageServiceFees,
        [Perm.SvcManageCategories] = Perm.FinanceManageServiceFees,
        [Perm.SvcManageServices] = Perm.FinanceManageServiceFees,
        [Perm.SvcManageFees] = Perm.FinanceManageServiceFees,
        [Perm.SvcReports] = Perm.FinanceDashboard,
        // update 10 laboratory: correcting saved results was "withdraw result"; receipts go with booking; payments with the cashier task
        [Perm.LabReviewResults] = Perm.LabDeleteResults,
        [Perm.LabPrintReceipts] = Perm.LabBook,
        [Perm.LabCollectPayments] = Perm.FinanceCollect,
        // update 10 IPD billing: split from the earlier IPD finance / billing / settings tasks (financial clearance override is never inherited)
        [Perm.IpdViewLedger] = Perm.IpdViewFinancial,
        [Perm.IpdViewStatements] = Perm.IpdViewFinancial,
        [Perm.IpdViewBillingHistory] = Perm.IpdViewFinancial,
        [Perm.IpdPostCharges] = Perm.IpdManageBilling,
        [Perm.IpdManageAdmissionFee] = Perm.IpdSettings,
        [Perm.IpdRecordAdmissionPayment] = Perm.FinanceCollect,
        [Perm.IpdManageWardCharges] = Perm.IpdSettings,
        [Perm.IpdRecordEquipment] = Perm.IpdManageNicu,
        [Perm.IpdManagePackages] = Perm.IpdSettings,
        [Perm.FinanceReceiveInpatientPayments] = Perm.FinanceCollect,
        [Perm.FinanceReceiveAdvances] = Perm.FinanceCollect,
        [Perm.FinanceViewClearance] = Perm.IpdViewFinancial,
    };
}
