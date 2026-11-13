using System.Globalization;
using BurhanNexus.HMS.Infrastructure.Patients;

namespace BurhanNexus.HMS.Web.Pages.Finance;

/// <summary>Period presets used by finance screens (hospital time zone).</summary>
public static class FinancePeriod
{
    public static readonly (string Key, string Name)[] Presets =
    {
        ("today", "Today"), ("yesterday", "Yesterday"), ("week", "This week"), ("month", "This month"), ("year", "This year"), ("custom", "Custom range"),
    };

    public static (DateOnly From, DateOnly To, string Key) Resolve(string? preset, DateOnly? from, DateOnly? to, DateOnly today)
    {
        switch (preset)
        {
            case "yesterday": return (today.AddDays(-1), today.AddDays(-1), "yesterday");
            case "week":
                var start = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // Monday
                return (start, today, "week");
            case "month": return (new DateOnly(today.Year, today.Month, 1), today, "month");
            case "year": return (new DateOnly(today.Year, 1, 1), today, "year");
            case "custom" or null when from is not null || to is not null:
                var f = from ?? to!.Value;
                var t = to ?? today;
                if (t < f) (f, t) = (t, f);
                if (t.DayNumber - f.DayNumber > 3660) f = t.AddDays(-3660);
                return (f, t, "custom");
            default: return (today, today, "today");
        }
    }

    public static string Label(DateOnly from, DateOnly to) =>
        from == to ? from.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)
                   : $"{from.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} – {to.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}";
}
