using System.ComponentModel.DataAnnotations;

namespace Klinova.Domain.Notifications;

public enum NotificationChannel
{
    [Display(Name = "SMS")]
    Sms = 0,

    [Display(Name = "E-posta")]
    Email = 1,

    /// <summary>Shown to the patient under "Bildirimlerim" in the patient portal; needs no provider.</summary>
    [Display(Name = "Hasta Portalı")]
    Portal = 2,
}

public enum NotificationStatus
{
    [Display(Name = "Gönderilecek")]
    Pending = 0,

    [Display(Name = "Gönderildi")]
    Sent = 1,

    [Display(Name = "Başarısız")]
    Failed = 2,
}

public enum NotificationType
{
    [Display(Name = "Randevu onayı")]
    AppointmentBooked = 0,

    [Display(Name = "Randevu hatırlatma")]
    AppointmentReminder = 1,

    [Display(Name = "Randevu iptali")]
    AppointmentCancelled = 2,

    [Display(Name = "Bekleme listesi")]
    WaitlistSlotAvailable = 3,
}

/// <summary>
/// A message queued for delivery (outbox pattern). A background dispatcher sends
/// pending messages through the configured SMS / e-mail providers and retries failures.
/// </summary>
public class Notification
{
    public const int MaxAttempts = 5;

    public long Id { get; set; }

    public int? PatientId { get; set; }

    public NotificationType Type { get; set; }

    public NotificationChannel Channel { get; set; }

    public string Recipient { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public NotificationStatus Status { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    /// <summary>When the patient opened the message in the portal (portal channel only).</summary>
    public DateTime? ReadAtUtc { get; set; }

    public void MarkSent(DateTime nowUtc)
    {
        Status = NotificationStatus.Sent;
        SentAtUtc = nowUtc;
        Attempts++;
        LastError = null;
    }

    public void MarkRead(DateTime nowUtc) => ReadAtUtc ??= nowUtc;

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length > 500 ? error[..500] : error;
        Status = Attempts >= MaxAttempts ? NotificationStatus.Failed : NotificationStatus.Pending;
    }
}
