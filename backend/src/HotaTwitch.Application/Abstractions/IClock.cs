namespace HotaTwitch.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
