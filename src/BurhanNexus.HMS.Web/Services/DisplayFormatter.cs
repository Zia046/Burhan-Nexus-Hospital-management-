using System.Globalization;
using BurhanNexus.HMS.Infrastructure.Services;

namespace BurhanNexus.HMS.Web.Services;

/// <summary>Formats dates, times and money using the hospital's configured preferences and time zone.</summary>
public sealed class DisplayFormatter
{
    private readonly ISettingsService _settings;
    private HospitalProfile? _profile;
    private TimeZoneInfo? _tz;
    private bool _loaded;

    public DisplayFormatter(ISettingsService settings) => _settings = settings;

    public async Task<HospitalProfile?> ProfileAsync()
    {
        if (!_loaded)
        {
            _profile = await _settings.GetProfileAsync();
            _tz = Resolve(_profile?.TimeZoneId ?? "Asia/Karachi");
            _loaded = true;
        }
        return _profile;
    }

    public async Task EnsureLoadedAsync() => await ProfileAsync();

    private string DateFormat => _profile?.DateFormat ?? "dd/MM/yyyy";
    private string TimeFormat => _profile?.TimeFormat ?? "hh:mm tt";

    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _tz ?? Resolve("Asia/Karachi"));

    public DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _tz ?? Resolve("Asia/Karachi"));

    public string Date(DateTime? utc) => utc is null ? "—" : ToLocal(utc.Value).ToString(DateFormat, CultureInfo.InvariantCulture);
    public string Time(DateTime? utc) => utc is null ? "—" : ToLocal(utc.Value).ToString(TimeFormat, CultureInfo.InvariantCulture);
    public string Day(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);
    public string DateTimeText(DateTime? utc) => utc is null ? "—" : $"{Date(utc)} {Time(utc)}";
    public string Money(decimal amount) => $"{_profile?.CurrencySymbol ?? "Rs."} {amount.ToString("N2", CultureInfo.InvariantCulture)}";

    public static string Bytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:N1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024.0:N1} MB",
        _ => $"{bytes / 1024.0 / 1024.0 / 1024.0:N2} GB"
    };

    public static TimeZoneInfo Resolve(string id)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        if (id == "Asia/Karachi" && TimeZoneInfo.TryFindSystemTimeZoneById("Pakistan Standard Time", out tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.FromHours(id == "Asia/Karachi" ? 5 : 0), id, id);
    }
}
