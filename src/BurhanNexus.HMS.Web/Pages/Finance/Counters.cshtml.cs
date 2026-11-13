using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance;

/// <summary>Counters, staff assignments/transfers and counter-wise collections, balances and receipts.</summary>
[HasPermission(Perm.FinanceCounterReports)]
public class CountersModel : HmsPageModel
{
    private readonly IFinanceSetupService _setup;
    private readonly IFinanceReportService _reports;
    private readonly ICashAccountService _cash;
    private readonly IDepartmentService _departments;
    private readonly IUserService _users;
    private readonly HospitalClock _clock;

    public CountersModel(IFinanceSetupService setup, IFinanceReportService reports, ICashAccountService cash, IDepartmentService departments,
        IUserService users, HospitalClock clock)
    {
        _setup = setup;
        _reports = reports;
        _cash = cash;
        _departments = departments;
        _users = users;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty] public CounterInput Form { get; set; } = new();
    [BindProperty] public int AssignUserId { get; set; }
    [BindProperty] public int? TransferFrom { get; set; }

    public IReadOnlyList<CounterRow> Counters { get; private set; } = Array.Empty<CounterRow>();
    public IReadOnlyList<DepartmentListItem> Departments { get; private set; } = Array.Empty<DepartmentListItem>();
    public IReadOnlyList<(int Id, string Name)> Users { get; private set; } = Array.Empty<(int, string)>();
    public FinanceDashboard Summary { get; private set; } = null!;
    public IReadOnlyList<ClosingFigures> Balances { get; private set; } = Array.Empty<ClosingFigures>();
    public IReadOnlyList<CounterAssignment> History { get; private set; } = Array.Empty<CounterAssignment>();
    public CounterRow? Editing { get; private set; }
    public bool CanManage { get; private set; }
    public string PresetKey { get; private set; } = "today";

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "finance-counters";
        ViewData["FinanceTab"] = "counters";
        CanManage = await CanAsync(Perm.FinanceManageCounters);
        Counters = await _setup.CountersAsync(activeOnly: false);
        Departments = await _departments.ListAsync(includeInactive: false);
        Users = (await _users.SearchAsync(null, true, null, 1, 500)).Items.Select(u => (u.Id, $"{u.FullName} ({u.Username})")).ToList();
        Editing = Id is int id ? Counters.FirstOrDefault(c => c.Counter.Id == id) : null;
        var today = await _clock.TodayAsync();
        var (from, to, key) = FinancePeriod.Resolve(Period, From, To, today);
        From = from; To = to; PresetKey = key;
        Summary = await _reports.DashboardAsync(new FinanceFilter { From = from, To = to, CounterId = Id });
        var list = new List<ClosingFigures>();
        foreach (var c in Counters.Where(c => Id is null || c.Counter.Id == Id)) list.Add(await _cash.ClosingFiguresAsync(ClosingScope.Counter, c.Counter.Id, to));
        Balances = list;
        if (Editing is not null) History = await _setup.AssignmentHistoryAsync(Editing.Counter.Id);
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (Editing is not null) Form = new CounterInput { Code = Editing.Counter.Code, Name = Editing.Counter.Name, DepartmentId = Editing.Counter.DepartmentId, IsActive = Editing.Counter.IsActive };
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (await DenyUnlessAsync(Perm.FinanceManageCounters) is { } deny) return deny;
        var r = await _setup.SaveCounterAsync(Id, Form);
        if (r.Succeeded) { FlashSuccess = "Counter saved."; return RedirectToPage(new { id = r.Value }); }
        await LoadAsync();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostAssignAsync()
    {
        if (await DenyUnlessAsync(Perm.FinanceManageCounters) is { } deny) return deny;
        var r = await _setup.AssignAsync(Id!.Value, AssignUserId, TransferFrom);
        if (r.Succeeded) { FlashSuccess = TransferFrom is null ? "Employee assigned." : "Employee transferred."; return RedirectToPage(new { id = Id }); }
        await LoadAsync();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostUnassignAsync(int userId)
    {
        if (await DenyUnlessAsync(Perm.FinanceManageCounters) is { } deny) return deny;
        var r = await _setup.UnassignAsync(Id!.Value, userId);
        if (r.Succeeded) FlashSuccess = "Employee removed from the counter."; else FlashError = r.ErrorText;
        return RedirectToPage(new { id = Id });
    }
}
