using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace BurhanNexus.HMS.Infrastructure.Finance;

/// <summary>Shared financial helpers: numbering, rounding, doctor-share arithmetic and locks.</summary>
public static class FinanceMath
{
    public static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>Doctor's part of a consultation net amount under the configured arrangement.</summary>
    public static decimal DoctorShare(DoctorShareType type, decimal value, decimal net)
    {
        if (net <= 0) return 0;
        var share = type switch
        {
            DoctorShareType.FullFeeToDoctor => net,
            DoctorShareType.HospitalCommissionPercent => net * (100 - value) / 100,
            DoctorShareType.DoctorPercent => net * value / 100,
            DoctorShareType.DoctorFixedAmount => Math.Min(value, net),
            DoctorShareType.HospitalFixedAmount => Math.Max(net - value, 0),
            _ => 0,
        };
        return Math.Clamp(Round(share), 0, net);
    }

    public static string Describe(DoctorShareType type, decimal value) => type switch
    {
        DoctorShareType.FullFeeToDoctor => "Full fee to doctor",
        DoctorShareType.HospitalCommissionPercent => $"Hospital commission {value:0.##}%",
        DoctorShareType.DoctorPercent => $"Doctor {value:0.##}%",
        DoctorShareType.DoctorFixedAmount => $"Doctor fixed {value:N2} per consultation",
        DoctorShareType.HospitalFixedAmount => $"Hospital fixed {value:N2} per consultation",
        _ => "Hospital keeps full fee",
    };

    public static string? ValidateShare(DoctorShareType type, decimal value)
    {
        if (!Enum.IsDefined(type)) return "Unknown fee-sharing arrangement.";
        if (type is DoctorShareType.HospitalCommissionPercent or DoctorShareType.DoctorPercent && value is < 0 or > 100)
            return "Percentage must be between 0 and 100.";
        if (type is DoctorShareType.DoctorFixedAmount or DoctorShareType.HospitalFixedAmount && value < 0)
            return "Fixed amount cannot be negative.";
        if (decimal.Round(value, 2) != value) return "Use at most 2 decimal places.";
        return null;
    }

    public static bool IsMoney(decimal v) => decimal.Round(v, 2) == v;
}

internal static class FinanceDb
{
    public static async Task<string> NextNumberAsync(HmsDbContext db, string sequence, string prefix, DateOnly date, CancellationToken ct)
    {
        var n = await db.Database.SqlQueryRaw<long>($"SELECT nextval('{sequence}') AS \"Value\"").SingleAsync(ct);
        return $"{prefix}-{date.Year}-{n:000000}";
    }

    /// <summary>Transaction-scoped lock: serialises operations on one account (cashier, doctor, receipt).</summary>
    public static Task LockAsync(HmsDbContext db, string key, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext(@k))",
            new[] { new NpgsqlParameter("k", NpgsqlDbType.Text) { Value = key } }, ct);

    public static bool IsUniqueViolation(DbUpdateException ex, string? index = null) =>
        ex.InnerException is PostgresException { SqlState: "23505" } pg && (index is null || pg.ConstraintName == index);
}

/// <summary>
/// Starts a database transaction, or joins the caller's one (Phase 5: a pharmacy sale creates its stock movements and the
/// Phase 3 receipt atomically). Commit/rollback only act when this scope started the transaction.
/// </summary>
internal sealed class TxScope : IAsyncDisposable
{
    private readonly IDbContextTransaction? _tx;
    private TxScope(IDbContextTransaction? tx) => _tx = tx;
    public bool Owned => _tx is not null;

    public static async Task<TxScope> BeginAsync(HmsDbContext db, CancellationToken ct) =>
        new(db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null);

    public Task CommitAsync(CancellationToken ct) => _tx?.CommitAsync(ct) ?? Task.CompletedTask;
    public Task RollbackAsync(CancellationToken ct) => _tx?.RollbackAsync(ct) ?? Task.CompletedTask;
    public ValueTask DisposeAsync() => _tx?.DisposeAsync() ?? ValueTask.CompletedTask;
}
