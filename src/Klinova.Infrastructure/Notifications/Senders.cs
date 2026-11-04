using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using Klinova.Application.Notifications;
using Klinova.Domain.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Klinova.Infrastructure.Notifications;

/// <summary>
/// Development / unconfigured fallback: records that a message would have been sent.
/// Only the type and a masked recipient are logged at Information level, so that
/// health data does not end up in log files; the full text is logged at Debug level.
/// </summary>
internal abstract class LoggingSender(ILogger logger) : INotificationSender
{
    public abstract NotificationChannel Channel { get; }

    public Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("[{Channel}] {Type} → {Recipient} (not delivered: no provider configured)", Channel, notification.Type, Mask(notification.Recipient));
        logger.LogDebug("[{Channel}] {Subject}: {Body}", Channel, notification.Subject, notification.Body);
        return Task.CompletedTask;
    }

    internal static string Mask(string recipient)
        => recipient.Length <= 4 ? "****" : new string('*', recipient.Length - 4) + recipient[^4..];
}

internal sealed class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : LoggingSender(logger)
{
    public override NotificationChannel Channel => NotificationChannel.Sms;
}

internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : LoggingSender(logger)
{
    public override NotificationChannel Channel => NotificationChannel.Email;
}

/// <summary>Sends e-mail through an SMTP server.</summary>
internal sealed class SmtpEmailSender(IOptions<NotificationOptions> options) : INotificationSender
{
    private readonly EmailOptions _options = options.Value.Email;

    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.From, _options.FromName),
            Subject = notification.Subject,
            Body = notification.Body,
            IsBodyHtml = false,
        };
        message.To.Add(notification.Recipient);

        using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.EnableSsl };
        if (!string.IsNullOrEmpty(_options.UserName))
        {
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}

/// <summary>Posts SMS messages to an HTTP endpoint (operator API or an adapter).</summary>
internal sealed class WebhookSmsSender(HttpClient http, IOptions<NotificationOptions> options) : INotificationSender
{
    private readonly SmsOptions _options = options.Value.Sms;

    public NotificationChannel Channel => NotificationChannel.Sms;

    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.WebhookUrl)
        {
            Content = JsonContent.Create(new { to = notification.Recipient, message = notification.Body }),
        };
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            request.Headers.Add("X-Api-Key", _options.ApiKey);
        }

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
