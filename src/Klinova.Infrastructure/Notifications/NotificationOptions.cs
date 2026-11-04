namespace Klinova.Infrastructure.Notifications;

/// <summary>Delivery settings bound from <c>Klinova:Notifications</c>.</summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Klinova:Notifications";

    public EmailOptions Email { get; set; } = new();

    public SmsOptions Sms { get; set; } = new();
}

public sealed class EmailOptions
{
    /// <summary>When false, e-mails are only written to the log.</summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string From { get; set; } = string.Empty;

    public string FromName { get; set; } = "Klinova";
}

public sealed class SmsOptions
{
    /// <summary>"Log" (default, nothing is sent) or "Webhook".</summary>
    public string Provider { get; set; } = "Log";

    /// <summary>
    /// HTTPS endpoint receiving <c>POST {"to": "...", "message": "..."}</c>. Put a small adapter
    /// in front of your SMS operator's API (most Turkish operators offer an HTTP API).
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Sent as the <c>X-Api-Key</c> header.</summary>
    public string? ApiKey { get; set; }
}
