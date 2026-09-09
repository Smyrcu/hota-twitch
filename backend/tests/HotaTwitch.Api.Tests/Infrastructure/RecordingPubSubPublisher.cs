using System.Collections.Concurrent;
using HotaTwitch.Application.Abstractions;
using HotaTwitch.Domain.Channels;

namespace HotaTwitch.Api.Tests.Infrastructure;

internal sealed class RecordingPubSubPublisher : IPubSubPublisher
{
    private readonly ConcurrentQueue<(ChannelId Channel, string Message)> broadcasts = new();

    public IReadOnlyList<(ChannelId Channel, string Message)> Broadcasts => [.. broadcasts];

    public Task PublishAsync(ChannelId channelId, string message, CancellationToken cancellationToken)
    {
        broadcasts.Enqueue((channelId, message));
        return Task.CompletedTask;
    }
}
