using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Licensing;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    private readonly ISystemStatusService _status;
    public HealthController(ISystemStatusService status) => _status = status;

    /// <summary>Health check for monitoring tools and the installer. Reveals no hospital data.</summary>
    [HttpGet("/health")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var h = await _status.CheckAsync(ct);
        var body = new { status = h.DatabaseConnected ? "Healthy" : "Unhealthy", database = h.DatabaseConnected ? "Connected" : "Unavailable", time = DateTime.UtcNow };
        return h.DatabaseConnected ? Ok(body) : StatusCode(503, body);
    }

    [HttpGet("/api/system/ping")]
    public IActionResult Ping() => Ok(new { ok = true, server = Environment.MachineName, time = DateTime.UtcNow });
}

[ApiController]
[Route("api/me")]
public sealed class MeApiController : ControllerBase
{
    private readonly UserPermissionContext _permissions;
    public MeApiController(UserPermissionContext permissions) => _permissions = permissions;

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(new
    {
        id = User.GetUserId(),
        username = User.Identity?.Name,
        fullName = User.GetFullName(),
        permissions = (await _permissions.GetAsync()).OrderBy(p => p)
    });
}

[ApiController]
[Route("api/dashboard")]
[HasPermission(Perm.DashboardView)]
public sealed class DashboardApiController : ControllerBase
{
    private readonly IUserService _users;
    private readonly ISystemStatusService _status;
    private readonly ILicenseService _license;
    private readonly UserPermissionContext _permissions;

    public DashboardApiController(IUserService users, ISystemStatusService status, ILicenseService license, UserPermissionContext permissions)
    {
        _users = users;
        _status = status;
        _license = license;
        _permissions = permissions;
    }

    /// <summary>Live dashboard figures (polled by the dashboard). Only returns what the user is allowed to see.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var perms = await _permissions.GetAsync();
        object? users = null;
        if (perms.Contains(Perm.UsersView))
        {
            var c = await _users.CountsAsync(ct);
            users = new { total = c.Total, active = c.Active, inactive = c.Inactive };
        }
        object? system = null;
        if (perms.Contains(Perm.SettingsView) || perms.Contains(Perm.BackupView))
        {
            var h = await _status.CheckAsync(ct);
            system = new { database = h.DatabaseConnected, activeSessions = h.ActiveSessions };
        }
        object? license = null;
        if (perms.Contains(Perm.LicenseView))
        {
            var l = await _license.GetStatusAsync(ct);
            license = new { state = l.State.ToString(), name = l.DisplayName, daysRemaining = l.DaysRemaining };
        }
        return Ok(new { serverTime = DateTime.UtcNow, users, system, license });
    }
}

[ApiController]
[Route("api/users")]
[HasPermission(Perm.UsersView)]
public sealed class UsersApiController : ControllerBase
{
    private readonly IUserService _users;
    public UsersApiController(IUserService users) => _users = users;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] bool? active, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var result = await _users.SearchAsync(q, active, null, page, 50, ct);
        return Ok(new
        {
            result.Total, result.Page, result.PageSize,
            items = result.Items.Select(u => new { u.Id, u.FullName, u.Username, u.Designation, u.IsActive, u.Departments, u.Roles })
        });
    }
}

[ApiController]
[Route("api/audit")]
[HasPermission(Perm.AuditView)]
public sealed class AuditApiController : ControllerBase
{
    private readonly IAuditQueryService _audit;
    public AuditApiController(IAuditQueryService audit) => _audit = audit;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? action, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var r = await _audit.SearchAsync(new AuditQuery { Action = action, Page = page, PageSize = 50 }, ct);
        return Ok(new
        {
            r.Total, r.Page,
            items = r.Items.Select(a => new { a.Id, a.TimestampUtc, a.Username, a.Action, a.EntityType, a.EntityId, a.Description, a.Success, a.Workstation, a.IpAddress })
        });
    }
}

[ApiController]
[Route("api/backups")]
[HasPermission(Perm.BackupView)]
public sealed class BackupApiController : ControllerBase
{
    private readonly Infrastructure.Backup.IBackupService _backups;
    public BackupApiController(Infrastructure.Backup.IBackupService backups) => _backups = backups;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok((await _backups.ListAsync(ct)).Select(b => new { b.Id, b.FileName, b.SizeBytes, status = b.Status.ToString(), kind = b.Kind.ToString(), b.StartedUtc, b.CompletedUtc }));
}
