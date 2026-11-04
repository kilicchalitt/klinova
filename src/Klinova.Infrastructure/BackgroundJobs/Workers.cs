using Klinova.Application.Appointments;
using Klinova.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Klinova.Infrastructure.BackgroundJobs;

public sealed class BackgroundJobOptions
{
    public const string SectionName = "Klinova:BackgroundJobs";

    /// <summary>Disable in tests or when another instance of the application runs the jobs.</summary>
    public bool Enabled { get; set; } = true;

    public int NotificationIntervalSeconds { get; set; } = 30;

    public int MaintenanceIntervalMinutes { get; set; } = 10;
}

/// <summary>Runs a scoped job periodically and keeps running when an iteration fails.</summary>
internal abstract class PeriodicWorker(IServiceScopeFactory scopes, ILogger logger) : BackgroundService
{
    protected abstract TimeSpan Interval { get; }

    protected abstract Task RunOnceAsync(IServiceProvider services, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the application time to finish start-up (migrations, seeding).
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await RunOnceAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Background job {Job} failed.", GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

internal sealed class NotificationDispatchWorker(IServiceScopeFactory scopes, ILogger<NotificationDispatchWorker> logger, Microsoft.Extensions.Options.IOptions<BackgroundJobOptions> options)
    : PeriodicWorker(scopes, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromSeconds(Math.Max(5, options.Value.NotificationIntervalSeconds));

    protected override Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
        => services.GetRequiredService<NotificationDispatcher>().DispatchPendingAsync(ct: ct);
}

internal sealed class AppointmentMaintenanceWorker(IServiceScopeFactory scopes, ILogger<AppointmentMaintenanceWorker> logger, Microsoft.Extensions.Options.IOptions<BackgroundJobOptions> options)
    : PeriodicWorker(scopes, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromMinutes(Math.Max(1, options.Value.MaintenanceIntervalMinutes));

    protected override Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
        => services.GetRequiredService<AppointmentMaintenance>().RunAsync(ct);
}
