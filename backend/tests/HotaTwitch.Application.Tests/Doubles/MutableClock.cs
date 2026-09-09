using HotaTwitch.Application.Abstractions;

namespace HotaTwitch.Application.Tests.Doubles;

internal sealed class MutableClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = now;

    public void Advance(TimeSpan amount) => UtcNow += amount;
}
