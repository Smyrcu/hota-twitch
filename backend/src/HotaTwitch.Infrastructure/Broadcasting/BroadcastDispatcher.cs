using HotaTwitch.Application.Broadcasting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HotaTwitch.Infrastructure.Broadcasting;

/// <summary>
/// Drains <see cref="BroadcastCoalescer"/> often enough that a state document reaches viewers
/// promptly, while the coalescer itself keeps the one-per-second rule.
/// </summary>
internal sealed class BroadcastDispatcher(BroadcastCoalescer coalescer, ILogger<BroadcastDispatcher> logger)
    : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await coalescer.PublishDueAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            BroadcastDispatcherLog.Stopped(logger);
        }
    }
}

internal static partial class BroadcastDispatcherLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Broadcast dispatcher stopped.")]
    public static partial void Stopped(ILogger logger);
}
