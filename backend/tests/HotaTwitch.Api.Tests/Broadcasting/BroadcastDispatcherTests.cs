using AwesomeAssertions;
using HotaTwitch.Api.Tests.Infrastructure;
using HotaTwitch.Application.Abstractions;
using HotaTwitch.Application.Broadcasting;
using HotaTwitch.Domain.Channels;
using HotaTwitch.Infrastructure.Broadcasting;
using HotaTwitch.Infrastructure.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HotaTwitch.Api.Tests.Broadcasting;

public sealed class BroadcastDispatcherTests
{
    private static readonly ChannelId Channel = new(HotaTwitchApiFactory.ChannelId);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task ExecuteAsync_PublisherTimesOut_KeepsBroadcastingForTheNextDocument()
    {
        var publisher = new TimingOutOncePublisher();
        var coalescer = new BroadcastCoalescer(publisher, new SystemClock(), NullLogger<BroadcastCoalescer>.Instance);
        using var dispatcher = new BroadcastDispatcher(coalescer, NullLogger<BroadcastDispatcher>.Instance);
        var cancellationToken = TestContext.Current.CancellationToken;

        await dispatcher.StartAsync(cancellationToken);
        try
        {
            coalescer.Submit(Channel, "gz:first");
            await publisher.TimedOut.Task.WaitAsync(Patience, cancellationToken);

            coalescer.Submit(Channel, "gz:second");
            var delivered = await publisher.Delivered.Task.WaitAsync(Patience, cancellationToken);

            delivered.Should().Be("gz:second");
            dispatcher.ExecuteTask!.IsCompleted.Should().BeFalse("a timed-out broadcast must not end the dispatcher");
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>Fails the first broadcast the way HttpClient reports its own timeout, then behaves.</summary>
    private sealed class TimingOutOncePublisher : IPubSubPublisher
    {
        private int attempts;

        public TaskCompletionSource TimedOut { get; } = new();

        public TaskCompletionSource<string> Delivered { get; } = new();

        public Task PublishAsync(ChannelId channelId, string message, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                TimedOut.TrySetResult();
                throw new TaskCanceledException(
                    "The request was canceled due to the configured HttpClient.Timeout",
                    new TimeoutException());
            }

            Delivered.TrySetResult(message);
            return Task.CompletedTask;
        }
    }
}
