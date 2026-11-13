using System.Globalization;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Printing;

namespace BurhanNexus.HMS.Web.Services;

public sealed class ReceiptSlipFactory
{
    private readonly DisplayFormatter _fmt;
    private readonly IFinanceSetupService _setup;

    public ReceiptSlipFactory(DisplayFormatter fmt, IFinanceSetupService setup)
    {
        _fmt = fmt;
        _setup = setup;
    }

    public async Task<ReceiptSlipData> BuildAsync(Receipt r, bool isReprint)
    {
        var h = (await _fmt.ProfileAsync())!;
        var s = await _setup.GetSettingsAsync();
        string M(decimal v) => v.ToString("N2", CultureInfo.InvariantCulture);
        var p = r.Patient;
        string? ageGender = null;
        if (p is not null)
        {
            var age = AgeCalculator.Of(p, r.BusinessDate)?.Short ?? "-";
            var g = p.Gender switch { Gender.Male => "M", Gender.Female => "F", Gender.Other => "O", _ => "-" };
            ageGender = $"{age} / {g}";
        }
        var address = string.Join(", ", new[] { h.Address, h.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return new ReceiptSlipData(
            h.HospitalName, address.Length == 0 ? null : address, string.IsNullOrWhiteSpace(h.ContactNumber) ? null : "Ph: " + h.ContactNumber,
            r.ReceiptNumber, _fmt.DateTimeText(r.CreatedUtc), p?.FullName ?? r.PayerName, p?.PatientNumber, ageGender,
            r.Lines.Select(l => new ReceiptSlipLine(l.ServiceName, l.DoctorName, M(l.Fee), l.Discount > 0 ? M(l.Discount) : null, M(l.NetAmount), M(l.Amount),
                l.BalanceAfter > 0 ? M(l.BalanceAfter) : null)).ToList(),
            M(r.TotalFee), r.TotalDiscount > 0 ? M(r.TotalDiscount) : null, M(r.TotalNet), $"{s.CurrencySymbol} {M(r.AmountPaid)}",
            r.BalanceAfter > 0 ? M(r.BalanceAfter) : null, r.RefundedAmount > 0 ? M(r.RefundedAmount) : null,
            r.PaymentMethod!.Name, r.PaymentReference, s.ShowCashierOnReceipt ? r.CashierName : null, r.Counter!.Name,
            s.ReceiptFooter ?? h.ReceiptFooter, "Printed " + _fmt.DateTimeText(DateTime.UtcNow), isReprint,
            r.Status == ReceiptStatus.Cancelled, r.CancelReason);
    }
}
