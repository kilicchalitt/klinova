using System.Globalization;
using Klinova.Application.Abstractions;
using Klinova.Domain.Appointments;
using Klinova.Domain.Notifications;
using Klinova.Domain.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Klinova.Application.Notifications;

/// <summary>Which external delivery channels are configured; set by the infrastructure layer.</summary>
public sealed class NotificationDeliveryOptions
{
    /// <summary>An SMS provider is configured; otherwise no SMS messages are queued.</summary>
    public bool SmsEnabled { get; set; }

    /// <summary>An SMTP server is configured; otherwise no e-mails are queued.</summary>
    public bool EmailEnabled { get; set; }
}

/// <summary>
/// Queues patient notifications (outbox). Messages are added to the current unit of
/// work and therefore saved in the same transaction as the change that caused them.
/// Patients with a portal account always receive the message under "Bildirimlerim";
/// SMS and e-mail are added only when a provider is configured.
/// </summary>
public sealed class NotificationService(IAppDbContext db, IClock clock, IOptions<NotificationDeliveryOptions> delivery)
{
    private readonly NotificationDeliveryOptions _delivery = delivery.Value;

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
        if (patient.UserId is not null)
        {
            db.Notifications.Add(new Notification
            {
                PatientId = patient.Id,
                Type = type,
                Channel = NotificationChannel.Portal,
                Recipient = "Hasta portalı",
                Subject = subject,
                Body = body,
                CreatedAtUtc = now,
                Status = NotificationStatus.Sent,
                SentAtUtc = now,
            });
        }

        if (_delivery.SmsEnabled && !string.IsNullOrWhiteSpace(patient.Phone))
        {
            db.Notifications.Add(new Notification { PatientId = patient.Id, Type = type, Channel = NotificationChannel.Sms, Recipient = patient.Phone, Subject = subject, Body = body, CreatedAtUtc = now });
        }

        if (_delivery.EmailEnabled && !string.IsNullOrWhiteSpace(patient.Email))
        {
            db.Notifications.Add(new Notification { PatientId = patient.Id, Type = type, Channel = NotificationChannel.Email, Recipient = patient.Email, Subject = subject, Body = body, CreatedAtUtc = now });
        }
    }

    private async Task<string> InstitutionNameAsync(CancellationToken ct)
        => await db.InstitutionProfiles.AsNoTracking().Select(p => p.Name).FirstOrDefaultAsync(ct) ?? "Klinova";

    private static string Describe(Appointment a) => $"{a.Date.ToString("d MMMM yyyy dddd", Turkish)} saat {a.StartTime:HH\\:mm}";
}

/// <summary>Portal inbox of a patient ("Bildirimlerim").</summary>
public sealed class PatientInbox(IAppDbContext db, IClock clock)
{
    public Task<int> UnreadCountAsync(int patientId, CancellationToken ct = default)
        => db.Notifications.CountAsync(n => n.PatientId == patientId && n.Channel == NotificationChannel.Portal && n.ReadAtUtc == null, ct);

    public Task<List<Notification>> ListAsync(int patientId, int take = 50, CancellationToken ct = default)
        => db.Notifications.AsNoTracking()
            .Where(n => n.PatientId == patientId && n.Channel == NotificationChannel.Portal)
            .OrderByDescending(n => n.Id)
            .Take(take)
            .ToListAsync(ct);

    public async Task MarkAllReadAsync(int patientId, CancellationToken ct = default)
    {
        var unread = await db.Notifications
            .Where(n => n.PatientId == patientId && n.Channel == NotificationChannel.Portal && n.ReadAtUtc == null)
            .ToListAsync(ct);
        if (unread.Count == 0)
        {
            return;
        }

        unread.ForEach(n => n.MarkRead(clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }
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
