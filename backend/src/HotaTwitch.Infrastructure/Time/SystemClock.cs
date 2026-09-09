using HotaTwitch.Application.Abstractions;

namespace HotaTwitch.Infrastructure.Time;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
