using System.Globalization;
using System.Text;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using BurhanNexus.HMS.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Audit;

/// <summary>Read-only audit log. There is no edit or delete action anywhere in the application.</summary>
[HasPermission(Perm.AuditView)]
public class IndexModel : HmsPageModel
{
    private readonly IAuditQueryService _audit;
    private readonly IAuditService _auditWriter;
    private readonly DisplayFormatter _fmt;

    public IndexModel(IAuditQueryService audit, IAuditService auditWriter, DisplayFormatter fmt)
    {
        _audit = audit;
        _auditWriter = auditWriter;
        _fmt = fmt;
    }

    [BindProperty(SupportsGet = true, Name = "q")] public string? Search { get; set; }
    [BindProperty(SupportsGet = true, Name = "action")] public string? ActionFilter { get; set; }
    [BindProperty(SupportsGet = true, Name = "from")] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true, Name = "to")] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "result")] public string? ResultFilter { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public PagedResult<AuditLog> Result { get; private set; } = new(Array.Empty<AuditLog>(), 0, 1, 50);
    public IReadOnlyList<string> Actions { get; private set; } = Array.Empty<string>();

    private async Task<AuditQuery> BuildQueryAsync()
    {
        await _fmt.EnsureLoadedAsync();
        return new AuditQuery
        {
            Search = Search,
            Action = ActionFilter,
            FromUtc = From is { } f ? _fmt.ToUtc(f.Date) : null,
            ToUtc = To is { } t ? _fmt.ToUtc(t.Date.AddDays(1)) : null,
            Success = ResultFilter switch { "ok" => true, "failed" => false, _ => null },
            Page = PageNumber,
            PageSize = 50
        };
    }

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "audit";
        Result = await _audit.SearchAsync(await BuildQueryAsync());
        Actions = await _audit.ActionsAsync();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        if (await DenyUnlessAsync(Perm.AuditExport) is { } deny) return deny;
        var q = await BuildQueryAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Id,Date,Time,Username,Action,Description,EntityType,EntityId,Success,Workstation,IpAddress,OldValues,NewValues");
        await foreach (var a in _audit.StreamAsync(q, HttpContext.RequestAborted))
        {
            sb.AppendJoin(',', new[]
            {
                a.Id.ToString(CultureInfo.InvariantCulture), _fmt.Date(a.TimestampUtc), _fmt.Time(a.TimestampUtc), a.Username, a.Action,
                a.Description, a.EntityType, a.EntityId, a.Success ? "Yes" : "No", a.Workstation, a.IpAddress, a.OldValues, a.NewValues
            }.Select(Csv));
            sb.AppendLine();
        }
        await _auditWriter.LogAsync(AuditActions.AuditExported, "AuditLog", null, "Audit log exported to CSV");
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"audit-log-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    /// <summary>CSV escaping that also neutralises spreadsheet formula injection.</summary>
    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
