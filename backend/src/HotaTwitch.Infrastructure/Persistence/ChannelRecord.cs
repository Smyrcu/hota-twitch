namespace HotaTwitch.Infrastructure.Persistence;

/// <summary>A row of the <c>channels</c> table. Kept apart from the domain entity on purpose.</summary>
internal sealed class ChannelRecord
{
    public required string ChannelId { get; set; }

    public required string TokenHash { get; set; }

    public required string TokenHint { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastStateAt { get; set; }
}
