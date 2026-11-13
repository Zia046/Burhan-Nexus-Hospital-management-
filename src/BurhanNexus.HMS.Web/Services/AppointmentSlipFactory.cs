using System.Globalization;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Printing;

namespace BurhanNexus.HMS.Web.Services;

/// <summary>Builds the single source of truth for a slip: the same data feeds the preview, the browser print and ESC/POS.</summary>
public sealed class AppointmentSlipFactory
{
    private readonly DisplayFormatter _fmt;
    private readonly IRegistrationSetupService _setup;

    public AppointmentSlipFactory(DisplayFormatter fmt, IRegistrationSetupService setup)
    {
        _fmt = fmt;
        _setup = setup;
    }

    public async Task<AppointmentSlipData> BuildAsync(Appointment a, bool isReprint)
    {
        var h = (await _fmt.ProfileAsync())!;
        var s = await _setup.GetSettingsAsync();
        var p = a.Patient!;
        var d = a.Doctor!;
        var age = AgeCalculator.Of(p, a.AppointmentDate)?.Short ?? "-";
        var gender = p.Gender switch { Gender.Male => "M", Gender.Female => "F", Gender.Other => "O", _ => "-" };
        var fee = s.ShowFeeOnAppointmentSlip && a.ConsultationFee is > 0 ? _fmt.Money(a.ConsultationFee.Value) : null;
        var time = s.AppointmentTimeEnabled && a.AppointmentTime is { } t
            ? t.ToString(h.TimeFormat.Replace(":ss", ""), CultureInfo.InvariantCulture) : null;
        var date = a.AppointmentDate.ToString(h.DateFormat, CultureInfo.InvariantCulture);
        return new AppointmentSlipData(
            h.HospitalName,
            h.ContactNumber is { Length: > 0 } c ? "Ph: " + c : null,
            d.Name, d.Qualification, d.Specialty, a.TokenNumber, p.FullName, p.PatientNumber, $"{age} / {gender}",
            date, time, fee, s.AppointmentSlipContact, s.AppointmentSlipMessage, a.Encounter!.EncounterNumber,
            "Printed " + _fmt.DateTimeText(DateTime.UtcNow), isReprint, a.Status == AppointmentStatus.Cancelled);
    }
}
