using System.Globalization;
using Klinova.Application.Abstractions;
using Klinova.Application.Appointments;
using Klinova.Application.Common;
using Klinova.Application.Organization;
using Klinova.Application.Patients;
using Klinova.Application.Scheduling;
using Klinova.Domain.Appointments;
using Klinova.Domain.Common;
using Klinova.Domain.Patients;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Appointments;

/// <summary>Booking on behalf of a patient (desk or phone).</summary>
public class BookModel(
    PatientService patients,
    PolyclinicService polyclinics,
    DoctorService doctors,
    AvailabilityService availability,
    AppointmentService appointments,
    IClock clock) : PageModel
{
    public const int WindowDays = 7;

    [BindProperty(SupportsGet = true)]
    public int PatientId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PolyclinicId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? DoctorId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? RescheduleId { get; set; }

    public Patient Patient { get; private set; } = default!;

    public List<LookupItem> Polyclinics { get; private set; } = [];

    public List<LookupItem> Doctors { get; private set; } = [];

    public SlotPickerModel? Picker { get; private set; }

    public BookingRestriction? Restriction { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Patient = await patients.GetAsync(PatientId, ct);
        Polyclinics = await polyclinics.LookupAsync(activeOnly: true, ct);
        if (PolyclinicId is not { } polyclinicId || Polyclinics.All(p => p.Id != polyclinicId))
        {
            PolyclinicId = null;
            return;
        }

        Doctors = await doctors.LookupAsync(polyclinicId, ct);
        Restriction = await appointments.GetRestrictionAsync(PatientId, polyclinicId, ct);

        var today = clock.Today;
        var last = availability.LastBookableDay(online: false);
        var from = From is { } f && f >= today ? f : today;
        var to = from.AddDays(WindowDays - 1);
        var slots = await availability.GetFreeSlotsAsync(polyclinicId, DoctorId, from, to, online: false, ct);
        Picker = new SlotPickerModel(
            slots,
            from,
            to < last ? to : last,
            from > today ? WindowUrl(from.AddDays(-WindowDays) < today ? today : from.AddDays(-WindowDays)) : null,
            to < last ? WindowUrl(to.AddDays(1)) : null);
    }

    public async Task<IActionResult> OnPostAsync(string? slot, string? note, CancellationToken ct)
    {
        if (!SlotValue.TryParse(slot, out var doctorId, out var date, out var start))
        {
            this.Error("Lütfen bir randevu saati seçin.");
            return RedirectToPage(RouteValues());
        }

        var request = new BookingRequest { PatientId = PatientId, DoctorId = doctorId, Date = date, StartTime = start, Note = note };
        try
        {
            var appointment = RescheduleId is { } previous
                ? await appointments.RescheduleAsync(previous, request, AppointmentChannel.Desk, ct: ct)
                : await appointments.BookAsync(request, AppointmentChannel.Desk, ct);
            this.Success($"{(RescheduleId is null ? "Randevu oluşturuldu" : "Randevu değiştirildi")}: {date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)} {start.ToString("HH:mm", CultureInfo.InvariantCulture)} · Kod {appointment.ConfirmationCode}");
            return RedirectToPage("Index", new { date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), polyclinicId = PolyclinicId });
        }
        catch (DomainException ex)
        {
            this.Error(ex.Message);
            return RedirectToPage(RouteValues());
        }
    }

    public string WindowUrl(DateOnly from)
        => Url.Page("Book", new { PatientId, PolyclinicId, DoctorId, RescheduleId, From = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) })!;

    private object RouteValues() => new { PatientId, PolyclinicId, DoctorId, RescheduleId, From = From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
}
