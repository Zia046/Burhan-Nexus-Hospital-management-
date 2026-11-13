using BurhanNexus.HMS.Infrastructure.Services;

namespace BurhanNexus.HMS.Infrastructure.Patients;

/// <summary>Current date/time in the hospital's configured time zone.</summary>
public sealed class HospitalClock
{
    private readonly ISettingsService _settings;
    private readonly TimeProvider _time;
    private TimeZoneInfo? _tz;

    public HospitalClock(ISettingsService settings, TimeProvider time)
    {
        _settings = settings;
        _time = time;
    }

    public DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    public async Task<TimeZoneInfo> ZoneAsync()
    {
        if (_tz is not null) return _tz;
        var profile = await _settings.GetProfileAsync();
        _tz = Resolve(profile?.TimeZoneId ?? "Asia/Karachi");
        return _tz;
    }

    public async Task<DateTime> LocalNowAsync() => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, await ZoneAsync());
    public async Task<DateOnly> TodayAsync() => DateOnly.FromDateTime(await LocalNowAsync());
    public async Task<DateOnly> LocalDateOfAsync(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), await ZoneAsync()));

    /// <summary>UTC start of a local date (00:00 hospital time).</summary>
    public async Task<DateTime> StartOfDayUtcAsync(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), await ZoneAsync());

    public async Task<DateTime> LocalToUtcAsync(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), await ZoneAsync());

    public static TimeZoneInfo Resolve(string id)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        if (id == "Asia/Karachi" && TimeZoneInfo.TryFindSystemTimeZoneById("Pakistan Standard Time", out tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.FromHours(id == "Asia/Karachi" ? 5 : 0), id, id);
    }
}
