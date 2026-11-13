namespace BurhanNexus.HMS.Core.Domain;

public readonly record struct PatientAge(int Value, AgeUnit Unit)
{
    public override string ToString() => Unit switch
    {
        AgeUnit.Years => $"{Value} {(Value == 1 ? "year" : "years")}",
        AgeUnit.Months => $"{Value} {(Value == 1 ? "month" : "months")}",
        _ => $"{Value} {(Value == 1 ? "day" : "days")}"
    };

    /// <summary>Compact form for slips: 35Y, 7M, 12D.</summary>
    public string Short => $"{Value}{Unit switch { AgeUnit.Years => "Y", AgeUnit.Months => "M", _ => "D" }}";
}

/// <summary>Age in years, months or days – infants are shown in months/days.</summary>
public static class AgeCalculator
{
    public static PatientAge FromDateOfBirth(DateOnly dob, DateOnly onDate)
    {
        if (dob > onDate) return new PatientAge(0, AgeUnit.Days);
        var years = onDate.Year - dob.Year;
        if (dob.AddYears(years) > onDate) years--;
        if (years >= 1) return new PatientAge(years, AgeUnit.Years);

        var months = (onDate.Year - dob.Year) * 12 + onDate.Month - dob.Month;
        if (dob.AddMonths(months) > onDate) months--;
        if (months >= 1) return new PatientAge(months, AgeUnit.Months);

        return new PatientAge(onDate.DayNumber - dob.DayNumber, AgeUnit.Days);
    }

    /// <summary>Estimated date of birth from an age entered manually on <paramref name="onDate"/>.</summary>
    public static DateOnly EstimateDateOfBirth(int age, AgeUnit unit, DateOnly onDate) => unit switch
    {
        AgeUnit.Years => onDate.AddYears(-age),
        AgeUnit.Months => onDate.AddMonths(-age),
        _ => onDate.AddDays(-age)
    };

    public static PatientAge? Of(Patient p, DateOnly onDate) =>
        p.DateOfBirth is { } dob ? FromDateOfBirth(dob, onDate)
        : p.AgeAtRegistration is { } a && p.AgeUnitAtRegistration is { } u ? new PatientAge(a, u) : null;
}
