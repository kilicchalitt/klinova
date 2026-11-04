using Klinova.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Klinova.Infrastructure.Services;

internal sealed class SystemClock(IOptions<KlinovaOptions> options, TimeProvider timeProvider) : IClock
{
    public TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public DateOnly Today => DateOnly.FromDateTime(ToLocal(UtcNow));

    public DateTime ToLocal(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);

    public DateTime StartOfDayUtc(DateOnly day)
        => TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), TimeZone);
}
