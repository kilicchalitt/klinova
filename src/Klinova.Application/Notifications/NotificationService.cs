using System.Globalization;
using Klinova.Application.Abstractions;
using Klinova.Domain.Appointments;
using Klinova.Domain.Notifications;
using Klinova.Domain.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Klinova.Application.Notifications;

/// <summary>
/// Queues patient notifications (outbox). Messages are added to the current unit of
/// work and therefore saved in the same transaction as the change that caused them.
/// </summary>
public sealed class NotificationService(IAppDbContext db, IClock clock)
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public async Task AppointmentBookedAsync(Patient patient, Appointment appointment, string doctorName, string polyclinicName, string facilityName, CancellationToken ct)
    {
        var institution = await InstitutionNameAsync(ct);
        Enqueue(
            patient,
            NotificationType.AppointmentBooked,
            $"{institution}: Randevunuz oluşturuldu",
            $"Sayın {patient.FullName}, {Describe(appointment)} tarihinde {facilityName} {polyclinicName} polikliniği, {doctorName} için randevunuz oluşturulmuştur. Randevu kodu: {appointment.ConfirmationCode}. Gelemeyecekseniz lütfen iptal ediniz. {institution}");
    }

    public async Task AppointmentReminderAsync(Patient patient, Appointment appointment, string doctorName, string polyclinicName, string facilityName, CancellationToken ct)
    {
        var institution = await InstitutionNameAsync(ct);
        Enqueue(
            patient,
            NotificationType.AppointmentReminder,
            $"{institution}: Randevu hatırlatması",
            $"Sayın {patient.FullName}, {Describe(appointment)} tarihinde {facilityName} {polyclinicName} polikliniği, {doctorName} randevunuz bulunmaktadır. Randevu kodu: {appointment.ConfirmationCode}. Gelemeyecekseniz lütfen iptal ediniz. {institution}");
    }

    public async Task AppointmentCancelledAsync(Patient patient, Appointment appointment, string polyclinicName, CancellationToken ct)
    {
        var institution = await InstitutionNameAsync(ct);
        var reason = appointment.CancelledBy == CancellationSource.Patient || string.IsNullOrWhiteSpace(appointment.CancellationReason)
            ? string.Empty
            : $" İptal nedeni: {appointment.CancellationReason}.";
        Enqueue(
            patient,
            NotificationType.AppointmentCancelled,
            $"{institution}: Randevunuz iptal edildi",
            $"Sayın {patient.FullName}, {Describe(appointment)} tarihli {polyclinicName} randevunuz iptal edilmiştir.{reason} {institution}");
    }

    public async Task WaitlistSlotAvailableAsync(Patient patient, string polyclinicName, DateOnly date, TimeOnly time, CancellationToken ct)
    {
        var institution = await InstitutionNameAsync(ct);
        Enqueue(
            patient,
            NotificationType.WaitlistSlotAvailable,
            $"{institution}: Beklediğiniz randevu açıldı",
            $"Sayın {patient.FullName}, bekleme listesinde olduğunuz {polyclinicName} polikliniğinde {date.ToString("d MMMM yyyy", Turkish)} {time:HH\\:mm} için randevu açıldı. Randevu almak için hasta portalını ziyaret ediniz; boş randevular ilk başvuran hastaya verilir. {institution}");
    }

    private void Enqueue(Patient patient, NotificationType type, string subject, string body)
    {
        var now = clock.UtcNow;
        if (!string.IsNullOrWhiteSpace(patient.Phone))
        {
            db.Notifications.Add(new Notification { PatientId = patient.Id, Type = type, Channel = NotificationChannel.Sms, Recipient = patient.Phone, Subject = subject, Body = body, CreatedAtUtc = now });
        }

        if (!string.IsNullOrWhiteSpace(patient.Email))
        {
            db.Notifications.Add(new Notification { PatientId = patient.Id, Type = type, Channel = NotificationChannel.Email, Recipient = patient.Email, Subject = subject, Body = body, CreatedAtUtc = now });
        }
    }

    private async Task<string> InstitutionNameAsync(CancellationToken ct)
        => await db.InstitutionProfiles.AsNoTracking().Select(p => p.Name).FirstOrDefaultAsync(ct) ?? "Klinova";

    private static string Describe(Appointment a) => $"{a.Date.ToString("d MMMM yyyy dddd", Turkish)} saat {a.StartTime:HH\\:mm}";
}

/// <summary>Sends pending notifications through the registered channel senders.</summary>
public sealed class NotificationDispatcher(IAppDbContext db, IEnumerable<INotificationSender> senders, IClock clock, ILogger<NotificationDispatcher> logger)
{
    public async Task<int> DispatchPendingAsync(int batchSize = 50, CancellationToken ct = default)
    {
        var pending = await db.Notifications
            .Where(n => n.Status == NotificationStatus.Pending)
            .OrderBy(n => n.Id)
            .Take(batchSize)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var notification in pending)
        {
            var sender = senders.FirstOrDefault(s => s.Channel == notification.Channel);
            if (sender is null)
            {
                notification.MarkFailed("Bu kanal için yapılandırılmış gönderici yok.");
                continue;
            }

            try
            {
                await sender.SendAsync(notification, ct);
                notification.MarkSent(clock.UtcNow);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Notification {Id} could not be sent via {Channel}.", notification.Id, notification.Channel);
                notification.MarkFailed(ex.Message);
            }
        }

        if (pending.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return sent;
    }

    public async Task RetryAsync(long id, CancellationToken ct = default)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw Common.NotFoundException.For("Bildirim", id);
        notification.Status = NotificationStatus.Pending;
        notification.Attempts = 0;
        await db.SaveChangesAsync(ct);
    }
}
