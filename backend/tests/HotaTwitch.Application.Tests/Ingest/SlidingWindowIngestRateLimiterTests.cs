using AwesomeAssertions;
using HotaTwitch.Application.Ingest;
using HotaTwitch.Application.Tests.Doubles;
using HotaTwitch.Domain.Channels;
using Xunit;

namespace HotaTwitch.Application.Tests.Ingest;

public sealed class SlidingWindowIngestRateLimiterTests
{
    private static readonly TokenHash Token = new("a");
    private static readonly TokenHash OtherToken = new("b");

    private readonly MutableClock clock = new(new DateTimeOffset(2026, 9, 9, 22, 0, 0, TimeSpan.Zero));

    [Fact]
    public void TryAcquire_TwoRequestsInOneSecond_BothSucceed()
    {
        var limiter = new SlidingWindowIngestRateLimiter(clock);

        limiter.TryAcquire(Token).Should().BeTrue();
        limiter.TryAcquire(Token).Should().BeTrue();
    }

    [Fact]
    public void TryAcquire_ThirdRequestInOneSecond_Fails()
    {
        var limiter = new SlidingWindowIngestRateLimiter(clock);
        limiter.TryAcquire(Token);
        limiter.TryAcquire(Token);

        limiter.TryAcquire(Token).Should().BeFalse();
    }

    [Fact]
    public void TryAcquire_AfterTheWindowHasPassed_SucceedsAgain()
    {
        var limiter = new SlidingWindowIngestRateLimiter(clock);
        limiter.TryAcquire(Token);
        limiter.TryAcquire(Token);

        clock.Advance(TimeSpan.FromSeconds(1));

        limiter.TryAcquire(Token).Should().BeTrue();
    }

    [Fact]
    public void TryAcquire_SlidingWindow_LetsThroughTwoRequestsPerSecondOnly()
    {
        var limiter = new SlidingWindowIngestRateLimiter(clock);
        limiter.TryAcquire(Token);
        clock.Advance(TimeSpan.FromMilliseconds(900));
        limiter.TryAcquire(Token);

        limiter.TryAcquire(Token).Should().BeFalse("the first request is still inside the window");

        clock.Advance(TimeSpan.FromMilliseconds(100));
        limiter.TryAcquire(Token).Should().BeTrue("the first request has left the window");
    }

    [Fact]
    public void TryAcquire_DifferentTokens_AreCountedApart()
    {
        var limiter = new SlidingWindowIngestRateLimiter(clock);
        limiter.TryAcquire(Token);
        limiter.TryAcquire(Token);

        limiter.TryAcquire(OtherToken).Should().BeTrue();
    }
}
