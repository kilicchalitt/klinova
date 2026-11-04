using System.Security.Claims;
using Klinova.Application.Abstractions;
using Klinova.Application.Appointments;
using Klinova.Application.Common;
using Klinova.Application.Organization;
using Klinova.Application.Scheduling;
using Klinova.Domain.Appointments;
using Klinova.Domain.Common;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Portal;

/// <summary>
/// Online booking wizard modelled on MHRS: province → facility → polyclinic →
/// physician ("fark etmez" for any) → day and time.
/// </summary>
public class BookModel(
    FacilityService facilities,
    PolyclinicService polyclinics,
    DoctorService doctors,
    AvailabilityService availability,
    AppointmentService appointments,
    WaitlistService waitlist,
    PortalAccounts accounts,
    IClock clock) : PageModel
{
    public const int WindowDays = 7;

    [BindProperty(SupportsGet = true)]
    public string? City { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? FacilityId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PolyclinicId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? DoctorId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? From { get; set; }

    /// <summary>When set, the chosen slot replaces this appointment.</summary>
    [BindProperty(SupportsGet = true)]
    public int? RescheduleId { get; set; }

    public List<string> Cities { get; private set; } = [];

    public List<LookupItem> Facilities { get; private set; } = [];

    public List<LookupItem> Polyclinics { get; private set; } = [];

    public List<LookupItem> Doctors { get; private set; } = [];

    public SlotPickerModel? Picker { get; private set; }

    public SlotView? Earliest { get; private set; }

    public BookingRestriction? Restriction { get; private set; }

    public DateOnly LastBookableDay { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Deep links (e.g. "change appointment") may carry only the polyclinic: derive facility and province.
        if (PolyclinicId is { } requested && (FacilityId is null || City is null)
            && await polyclinics.FindFacilityAsync(requested, ct) is { } location)
        {
            FacilityId ??= location.FacilityId;
            City ??= location.City;
        }

        Cities = await facilities.CitiesAsync(ct);
        if (City is null && Cities.Count == 1)
        {
            City = Cities[0];
        }

        if (City is null)
        {
            return;
        }

        Facilities = await facilities.LookupAsync(activeOnly: true, City, ct);
        if (FacilityId is null && Facilities.Count == 1)
        {
            FacilityId = Facilities[0].Id;
        }

        if (FacilityId is not { } facilityId || Facilities.All(f => f.Id != facilityId))
        {
            FacilityId = null;
            return;
        }

        Polyclinics = await polyclinics.OnlineLookupAsync(facilityId, ct);
        if (PolyclinicId is not { } polyclinicId || Polyclinics.All(p => p.Id != polyclinicId))
        {
            PolyclinicId = null;
            return;
        }

        Doctors = await doctors.LookupAsync(polyclinicId, ct);
        if (DoctorId is { } d && Doctors.All(x => x.Id != d))
        {
            DoctorId = null;
        }

        if (User.IsInRole(Roles.Patient))
        {
            var patient = await accounts.GetPatientAsync(UserId, ct);
            Restriction = await appointments.GetRestrictionAsync(patient.Id, polyclinicId, ct);
        }

        var today = clock.Today;
        LastBookableDay = availability.LastBookableDay(online: true);
        var from = From is { } f && f >= today ? f : today;
        var to = from.AddDays(WindowDays - 1);
        var slots = await availability.GetFreeSlotsAsync(polyclinicId, DoctorId, from, to, online: true, ct);
        Earliest = await availability.FindEarliestAsync(polyclinicId, DoctorId, online: true, ct);

        Picker = new SlotPickerModel(
            slots,
            from,
            to < LastBookableDay ? to : LastBookableDay,
            from > today ? WindowUrl(from.AddDays(-WindowDays) < today ? today : from.AddDays(-WindowDays)) : null,
            to < LastBookableDay ? WindowUrl(to.AddDays(1)) : null);
    }

    public async Task<IActionResult> OnPostBookAsync(string? slot, string? note, CancellationToken ct)
    {
        if (!User.IsInRole(Roles.Patient))
        {
            return Challenge();
        }

        if (!SlotValue.TryParse(slot, out var doctorId, out var date, out var start))
        {
            this.Error("Lütfen bir randevu saati seçin.");
            return RedirectToPage(RouteValues());
        }

        var patient = await accounts.GetPatientAsync(UserId, ct);
        var request = new BookingRequest { PatientId = patient.Id, DoctorId = doctorId, Date = date, StartTime = start, Note = note };
        try
        {
            var appointment = RescheduleId is { } previous
                ? await appointments.RescheduleAsync(previous, request, AppointmentChannel.Online, patient.Id, ct)
                : await appointments.BookAsync(request, AppointmentChannel.Online, ct);
            this.Success(RescheduleId is null
                ? $"Randevunuz oluşturuldu. Randevu kodunuz: {appointment.ConfirmationCode}"
                : $"Randevunuz değiştirildi. Yeni randevu kodunuz: {appointment.ConfirmationCode}");
            return RedirectToPage("Appointments");
        }
        catch (DomainException ex)
        {
            this.Error(ex.Message);
            return RedirectToPage(RouteValues());
        }
    }

    public async Task<IActionResult> OnPostWaitlistAsync(DateOnly? until, CancellationToken ct)
    {
        if (!User.IsInRole(Roles.Patient))
        {
            return Challenge();
        }

        if (PolyclinicId is not { } polyclinicId)
        {
            return RedirectToPage(RouteValues());
        }

        var patient = await accounts.GetPatientAsync(UserId, ct);
        var today = clock.Today;
        try
        {
            await waitlist.JoinAsync(patient.Id, polyclinicId, DoctorId, today, until ?? today.AddDays(14), ct);
            this.Success("Bekleme listesine eklendiniz. Uygun bir randevu açıldığında Bildirimlerim bölümünden haber vereceğiz.");
            return RedirectToPage("Appointments");
        }
        catch (DomainException ex)
        {
            this.Error(ex.Message);
            return RedirectToPage(RouteValues());
        }
    }

    public string WindowUrl(DateOnly from)
        => Url.Page("Book", new { City, FacilityId, PolyclinicId, DoctorId, RescheduleId, From = from.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) })!;

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private object RouteValues() => new { City, FacilityId, PolyclinicId, DoctorId, RescheduleId, From = From?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) };
}
