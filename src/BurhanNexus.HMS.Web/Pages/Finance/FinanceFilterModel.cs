using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance;

/// <summary>Shared filter handling of the financial dashboard and reports.</summary>
public abstract class FinanceFilterModel : HmsPageModel
{
    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "department")] public int? DepartmentId { get; set; }
    [BindProperty(SupportsGet = true, Name = "counter")] public int? CounterId { get; set; }
    [BindProperty(SupportsGet = true, Name = "cashier")] public int? CashierId { get; set; }
    [BindProperty(SupportsGet = true, Name = "doctor")] public int? DoctorId { get; set; }
    [BindProperty(SupportsGet = true, Name = "method")] public int? MethodId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }

    public string PresetKey { get; private set; } = "today";
    public FinanceFilter Filter { get; private set; } = new();
    public IReadOnlyList<DepartmentListItem> Departments { get; private set; } = Array.Empty<DepartmentListItem>();
    public IReadOnlyList<CounterRow> Counters { get; private set; } = Array.Empty<CounterRow>();
    public IReadOnlyList<UserListItem> Users { get; private set; } = Array.Empty<UserListItem>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<PaymentMethod> Methods { get; private set; } = Array.Empty<PaymentMethod>();

    protected async Task LoadFilterAsync()
    {
        var sp = HttpContext.RequestServices;
        var today = await sp.GetRequiredService<HospitalClock>().TodayAsync();
        var (from, to, key) = FinancePeriod.Resolve(Period, From, To, today);
        From = from; To = to; PresetKey = key;
        Filter = new FinanceFilter
        {
            From = from, To = to, DepartmentId = DepartmentId, CounterId = CounterId, UserId = CashierId, DoctorId = DoctorId,
            PaymentMethodId = MethodId, Status = Status is "valid" or "cancelled" or "refunded" ? Status : null
        };
        var setup = sp.GetRequiredService<IFinanceSetupService>();
        Departments = await sp.GetRequiredService<IDepartmentService>().ListAsync(includeInactive: true);
        Counters = await setup.CountersAsync(false);
        Users = (await sp.GetRequiredService<IUserService>().SearchAsync(null, null, null, 1, 500)).Items;
        Doctors = await sp.GetRequiredService<IRegistrationSetupService>().DoctorsAsync(activeOnly: false);
        Methods = await setup.PaymentMethodsAsync(false);
    }

    public string QueryString(string? extra = null) =>
        $"?period={PresetKey}&from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}&department={DepartmentId}&counter={CounterId}&cashier={CashierId}&doctor={DoctorId}&method={MethodId}&status={Status}{extra}";
}
