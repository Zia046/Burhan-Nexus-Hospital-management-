using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BurhanNexus.HMS.Infrastructure.Pharmacy;

/// <summary>One movement to post: batch, location and signed quantity plus the document it belongs to.</summary>
internal sealed record StockMove(
    InventoryTxnType Type, int LocationId, int MedicineId, long BatchId, int Quantity,
    string DocumentType, long DocumentId, string DocumentNumber,
    decimal? UnitCost = null, decimal? UnitPrice = null, int? CounterpartLocationId = null, long? PatientId = null, string? Note = null);

/// <summary>A FEFO allocation: which batch and how much of it.</summary>
internal sealed record Allocation(MedicineBatch Batch, int Quantity, bool FefoOverridden);

/// <summary>
/// The only code that changes stock balances. Each move updates the balance with one atomic SQL statement
/// (decrements only succeed when enough stock is left, so two counters can never over-issue the same batch) and
/// writes the permanent inventory transaction in the same database transaction.
/// </summary>
internal static class StockEngine
{
    /// <summary>Posts a movement. Returns the new balance, or null when a decrease would make the stock negative.</summary>
    public static async Task<int?> PostAsync(HmsDbContext db, StockMove m, DateOnly businessDate, DateTime now, int? userId, string? username, CancellationToken ct)
    {
        if (m.Quantity == 0) throw new ArgumentException("Quantity must not be zero.");
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Stock can only change inside a database transaction.");
        List<int> result;
        if (m.Quantity > 0)
        {
            result = await db.Database.SqlQueryRaw<int>("""
                INSERT INTO "StockBalances" ("LocationId", "BatchId", "MedicineId", "Quantity", "UpdatedUtc")
                VALUES (@l, @b, @m, @q, @now)
                ON CONFLICT ("LocationId", "BatchId") DO UPDATE SET "Quantity" = "StockBalances"."Quantity" + EXCLUDED."Quantity", "UpdatedUtc" = EXCLUDED."UpdatedUtc"
                RETURNING "Quantity" AS "Value"
                """, P("l", m.LocationId), P("b", m.BatchId), P("m", m.MedicineId), P("q", m.Quantity), P("now", now)).ToListAsync(ct);
        }
        else
        {
            result = await db.Database.SqlQueryRaw<int>("""
                UPDATE "StockBalances" SET "Quantity" = "Quantity" + @q, "UpdatedUtc" = @now
                WHERE "LocationId" = @l AND "BatchId" = @b AND "Quantity" + @q >= 0
                RETURNING "Quantity" AS "Value"
                """, P("l", m.LocationId), P("b", m.BatchId), P("q", m.Quantity), P("now", now)).ToListAsync(ct);
        }
        if (result.Count == 0) return null;
        db.InventoryTransactions.Add(new InventoryTransaction
        {
            Type = m.Type, LocationId = m.LocationId, MedicineId = m.MedicineId, BatchId = m.BatchId, Quantity = m.Quantity, BalanceAfter = result[0],
            UnitCost = m.UnitCost, UnitPrice = m.UnitPrice, DocumentType = m.DocumentType, DocumentId = m.DocumentId, DocumentNumber = m.DocumentNumber,
            CounterpartLocationId = m.CounterpartLocationId, PatientId = m.PatientId, Note = m.Note,
            BusinessDate = businessDate, CreatedUtc = now, UserId = userId, Username = username
        });
        return result[0];
    }

    private static NpgsqlParameter P(string name, object value) => new(name, value);

    /// <summary>Batches of a medicine at a location that may be issued today, earliest expiry first (FEFO).</summary>
    public static async Task<List<(MedicineBatch Batch, int Available)>> EligibleAsync(HmsDbContext db, int locationId, int medicineId, DateOnly today, CancellationToken ct) =>
        (await db.StockBalances.AsNoTracking().Include(s => s.Batch)
            .Where(s => s.LocationId == locationId && s.MedicineId == medicineId && s.Quantity > 0 && s.Batch!.ExpiryDate >= today)
            .OrderBy(s => s.Batch!.ExpiryDate).ThenBy(s => s.BatchId)
            .Select(s => new { s.Batch, s.Quantity }).ToListAsync(ct))
        .Select(x => (x.Batch!, x.Quantity)).ToList();

    public static bool IsExpired(DateOnly expiry, DateOnly today) => expiry < today;

    /// <summary>
    /// Decides the batches for one line. Without a chosen batch the quantity is taken from the earliest-expiring batches
    /// (split over several batches when needed, each recorded separately). A chosen batch that is not the FEFO batch needs
    /// the "Select another batch" permission. Expired batches are never allocated.
    /// </summary>
    public static async Task<(List<Allocation>? Allocations, string? Error)> AllocateAsync(
        HmsDbContext db, int locationId, Medicine medicine, int quantity, long? chosenBatchId, bool canSelectBatch, DateOnly today, CancellationToken ct)
    {
        if (quantity <= 0) return (null, $"Enter the quantity of {medicine.Name}.");
        var eligible = await EligibleAsync(db, locationId, medicine.Id, today, ct);
        if (chosenBatchId is long bid)
        {
            var batch = await db.MedicineBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bid && b.MedicineId == medicine.Id, ct);
            if (batch is null) return (null, $"Batch not found for {medicine.Name}.");
            if (IsExpired(batch.ExpiryDate, today)) return (null, $"{medicine.Name} batch {batch.BatchNumber} expired on {batch.ExpiryDate:dd MMM yyyy} and cannot be issued or sold.");
            var e = eligible.FirstOrDefault(x => x.Batch.Id == bid);
            if (e.Batch is null || e.Available < quantity)
                return (null, $"Only {(e.Batch is null ? 0 : e.Available)} {medicine.Unit}(s) of {medicine.Name} batch {batch.BatchNumber} are in stock.");
            var overridden = eligible[0].Batch.Id != bid && eligible[0].Batch.ExpiryDate < batch.ExpiryDate;
            if (overridden && !canSelectBatch)
                return (null, $"{medicine.Name}: use batch {eligible[0].Batch.BatchNumber} (expires first). Choosing another batch needs the “Select another batch” permission.");
            return (new List<Allocation> { new(batch, quantity, overridden) }, null);
        }
        var total = eligible.Sum(x => x.Available);
        if (total < quantity)
            return (null, $"Only {total} {medicine.Unit}(s) of {medicine.Name} are available (not expired){(total == 0 ? "" : "")}.");
        var list = new List<Allocation>();
        var left = quantity;
        foreach (var (b, available) in eligible)
        {
            var take = Math.Min(left, available);
            list.Add(new Allocation(b, take, false));
            left -= take;
            if (left == 0) break;
        }
        return (list, null);
    }
}
