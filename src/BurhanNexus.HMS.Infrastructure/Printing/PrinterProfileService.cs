using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BurhanNexus.HMS.Infrastructure.Printing;

public sealed class PrinterProfileInput
{
    public string WorkstationName { get; set; } = PrinterProfile.DefaultWorkstation;
    public PrintMode Mode { get; set; } = PrintMode.Browser;
    public int PaperWidthMm { get; set; } = 80;
    public string? WindowsPrinterName { get; set; }
    public string? LanHost { get; set; }
    public int LanPort { get; set; } = 9100;
    public string? AgentUrl { get; set; }
    public SlipAlignment Alignment { get; set; } = SlipAlignment.Center;
    public SlipFontSize FontSize { get; set; } = SlipFontSize.Normal;
    public bool ShowLogo { get; set; } = true;
    public bool BoldText { get; set; } = true;
    public int TokenSize { get; set; } = 4;
    public bool AutoCut { get; set; } = true;
    public int Copies { get; set; } = 1;
}

public interface IPrinterProfileService
{
    /// <summary>Profile of the workstation, falling back to the default profile ("*"), or built-in defaults.</summary>
    Task<PrinterProfile> ForWorkstationAsync(string? workstation, CancellationToken ct = default);
    Task<IReadOnlyList<PrinterProfile>> ListAsync(CancellationToken ct = default);
    Task<ServiceResult<int>> SaveAsync(int? id, PrinterProfileInput input, CancellationToken ct = default);
}

public sealed class PrinterProfileService : IPrinterProfileService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly Core.Abstractions.IRequestContext _ctx;
    private readonly TimeProvider _time;

    public PrinterProfileService(HmsDbContext db, IAuditService audit, Core.Abstractions.IRequestContext ctx, TimeProvider time)
    {
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _time = time;
    }

    public async Task<PrinterProfile> ForWorkstationAsync(string? workstation, CancellationToken ct = default)
    {
        var name = (workstation ?? string.Empty).Trim();
        var profiles = await _db.PrinterProfiles.AsNoTracking()
            .Where(p => p.WorkstationName == name || p.WorkstationName == PrinterProfile.DefaultWorkstation).ToListAsync(ct);
        return profiles.FirstOrDefault(p => p.WorkstationName.Equals(name, StringComparison.OrdinalIgnoreCase) && name.Length > 0)
               ?? profiles.FirstOrDefault(p => p.WorkstationName == PrinterProfile.DefaultWorkstation)
               ?? new PrinterProfile();
    }

    public async Task<IReadOnlyList<PrinterProfile>> ListAsync(CancellationToken ct = default) =>
        await _db.PrinterProfiles.AsNoTracking().OrderBy(p => p.WorkstationName == PrinterProfile.DefaultWorkstation ? 0 : 1).ThenBy(p => p.WorkstationName).ToListAsync(ct);

    public async Task<ServiceResult<int>> SaveAsync(int? id, PrinterProfileInput i, CancellationToken ct = default)
    {
        var e = new List<string>();
        var ws = (i.WorkstationName ?? string.Empty).Trim();
        if (ws.Length == 0 || ws.Length > 50) e.Add("Workstation name is required (use * for the default of all computers).");
        if (!Enum.IsDefined(i.Mode)) e.Add("Unknown print method.");
        if (i.PaperWidthMm is not (58 or 80)) e.Add("Paper width must be 58 mm or 80 mm.");
        if (i.TokenSize is < 2 or > 8) e.Add("Token size must be between 2 and 8.");
        if (i.Copies is < 1 or > 3) e.Add("Copies must be between 1 and 3.");
        if (!Enum.IsDefined(i.Alignment) || !Enum.IsDefined(i.FontSize)) e.Add("Invalid alignment or font size.");
        if (i.Mode == PrintMode.LanDirect)
        {
            var check = NetworkPrinter.Validate(i.LanHost, i.LanPort);
            if (!check.Succeeded) e.AddRange(check.Errors);
        }
        if (i.Mode == PrintMode.LocalAgent && string.IsNullOrWhiteSpace(i.WindowsPrinterName) && string.IsNullOrWhiteSpace(i.LanHost))
            e.Add("Enter the Windows printer name (as shown in Windows 'Printers & scanners') for the Print Agent.");
        var agent = string.IsNullOrWhiteSpace(i.AgentUrl) ? "http://127.0.0.1:9123" : i.AgentUrl.Trim();
        if (!Uri.TryCreate(agent, UriKind.Absolute, out var agentUri) || agentUri.Scheme != Uri.UriSchemeHttp
            || !(agentUri.Host is "127.0.0.1" or "localhost"))
            e.Add("Print Agent address must be on this computer, e.g. http://127.0.0.1:9123.");
        if ((i.WindowsPrinterName?.Length ?? 0) > 200) e.Add("Printer name is too long.");
        if (e.Count > 0) return ServiceResult<int>.Fail(e.ToArray());

        PrinterProfile p;
        object? old = null;
        if (id is null)
        {
            if (await _db.PrinterProfiles.AnyAsync(x => x.WorkstationName.ToLower() == ws.ToLower(), ct))
                return ServiceResult<int>.Fail($"A printer profile for '{ws}' already exists – edit it instead.");
            p = new PrinterProfile();
            _db.PrinterProfiles.Add(p);
        }
        else
        {
            var found = await _db.PrinterProfiles.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (found is null) return ServiceResult<int>.Fail("Printer profile not found.");
            p = found;
            old = Snapshot(p);
        }
        p.WorkstationName = ws;
        p.Mode = i.Mode;
        p.PaperWidthMm = i.PaperWidthMm;
        p.WindowsPrinterName = string.IsNullOrWhiteSpace(i.WindowsPrinterName) ? null : i.WindowsPrinterName.Trim();
        p.LanHost = string.IsNullOrWhiteSpace(i.LanHost) ? null : i.LanHost.Trim();
        p.LanPort = i.LanPort is > 0 and <= 65535 ? i.LanPort : 9100;
        p.AgentUrl = agent.TrimEnd('/');
        p.Alignment = i.Alignment;
        p.FontSize = i.FontSize;
        p.ShowLogo = i.ShowLogo;
        p.BoldText = i.BoldText;
        p.TokenSize = i.TokenSize;
        p.AutoCut = i.AutoCut;
        p.Copies = i.Copies;
        p.UpdatedUtc = _time.GetUtcNow().UtcDateTime;
        p.UpdatedByUserId = _ctx.UserId;
        _audit.Add(AuditActions.PrinterProfileSaved, "PrinterProfile", ws, $"Printer settings saved for workstation '{ws}'", old, Snapshot(p));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            _db.ChangeTracker.Clear();
            return ServiceResult<int>.Fail($"A printer profile for '{ws}' already exists.");
        }
        return ServiceResult<int>.Ok(p.Id);
    }

    private static object Snapshot(PrinterProfile p) => new
    {
        p.WorkstationName, Mode = p.Mode.ToString(), p.PaperWidthMm, p.WindowsPrinterName, p.LanHost, p.LanPort, p.AgentUrl,
        Alignment = p.Alignment.ToString(), FontSize = p.FontSize.ToString(), p.ShowLogo, p.BoldText, p.TokenSize, p.AutoCut, p.Copies
    };
}
