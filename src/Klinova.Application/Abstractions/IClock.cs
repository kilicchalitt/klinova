namespace Klinova.Application.Abstractions;

/// <summary>Provides the current time in UTC and in the institution's time zone.</summary>
public interface IClock
{
    DateTime UtcNow { get; }

    TimeZoneInfo TimeZone { get; }

    /// <summary>Current business day in the institution's time zone.</summary>
    DateOnly Today { get; }

    DateTime ToLocal(DateTime utc);

    /// <summary>Returns the UTC instant at which the given local day starts.</summary>
    DateTime StartOfDayUtc(DateOnly day);
}
