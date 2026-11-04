namespace Klinova.Web.Infrastructure;

public sealed record PageHeaderModel(string Title, string? Subtitle = null);

public sealed record PagerModel(int Page, int TotalPages, int TotalCount, Func<int, string> UrlFor);

public sealed record SlotPickerModel(IReadOnlyList<Klinova.Application.Scheduling.SlotView> Slots, DateOnly From, DateOnly To, string? PreviousUrl, string? NextUrl);

/// <summary>Parses the "doctorId|yyyy-MM-dd|HH:mm" value posted by the slot picker.</summary>
public static class SlotValue
{
    public static bool TryParse(string? value, out int doctorId, out DateOnly date, out TimeOnly start)
    {
        doctorId = 0;
        date = default;
        start = default;
        var parts = value?.Split('|');
        return parts is { Length: 3 }
               && int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out doctorId)
               && DateOnly.TryParseExact(parts[1], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date)
               && TimeOnly.TryParseExact(parts[2], "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out start);
    }
}
