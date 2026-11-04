using Klinova.Domain.Notifications;

namespace Klinova.Application.Notifications;

/// <summary>
/// Delivers notifications through one channel (SMS provider, SMTP server…).
/// Implementations throw on failure; the dispatcher records the error and retries.
/// </summary>
public interface INotificationSender
{
    NotificationChannel Channel { get; }

    Task SendAsync(Notification notification, CancellationToken cancellationToken);
}
