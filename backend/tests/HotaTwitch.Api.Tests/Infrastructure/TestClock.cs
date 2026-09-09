using HotaTwitch.Application.Abstractions;

namespace HotaTwitch.Api.Tests.Infrastructure;

internal sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = new(2026, 9, 9, 22, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan amount) => UtcNow += amount;
}
