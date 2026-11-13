using System.Text.Json;
using System.Text.Json.Serialization;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;

namespace BurhanNexus.HMS.Infrastructure.Services;

public interface IAuditService
{
    /// <summary>Adds an audit entry to the current unit of work (saved with the caller's SaveChanges).</summary>
    void Add(string action, string? entityType = null, string? entityId = null, string? description = null,
        object? oldValues = null, object? newValues = null, bool success = true);

    /// <summary>Adds and immediately saves an audit entry.</summary>
    Task LogAsync(string action, string? entityType = null, string? entityId = null, string? description = null,
        object? oldValues = null, object? newValues = null, bool success = true, CancellationToken ct = default);
}

public sealed class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HmsDbContext _db;
    private readonly IRequestContext _ctx;
    private readonly TimeProvider _time;

    public AuditService(HmsDbContext db, IRequestContext ctx, TimeProvider time)
    {
        _db = db;
        _ctx = ctx;
        _time = time;
    }

    public void Add(string action, string? entityType = null, string? entityId = null, string? description = null,
        object? oldValues = null, object? newValues = null, bool success = true)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            TimestampUtc = _time.GetUtcNow().UtcDateTime,
            UserId = _ctx.UserId,
            Username = _ctx.Username,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = Truncate(description, 1000),
            OldValues = Serialize(oldValues),
            NewValues = Serialize(newValues),
            IpAddress = Truncate(_ctx.IpAddress, 64),
            Workstation = Truncate(_ctx.Workstation, 100),
            UserAgent = Truncate(_ctx.UserAgent, 500),
            Success = success
        });
    }

    public async Task LogAsync(string action, string? entityType = null, string? entityId = null, string? description = null,
        object? oldValues = null, object? newValues = null, bool success = true, CancellationToken ct = default)
    {
        Add(action, entityType, entityId, description, oldValues, newValues, success);
        await _db.SaveChangesAsync(ct);
    }

    private static string? Serialize(object? value) => value switch
    {
        null => null,
        string s => JsonSerializer.Serialize(new { value = s }, Json),
        _ => JsonSerializer.Serialize(value, Json)
    };

    internal static string? Truncate(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
