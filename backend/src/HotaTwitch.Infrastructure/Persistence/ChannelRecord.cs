namespace HotaTwitch.Infrastructure.Persistence;

/// <summary>A row of the <c>channels</c> table. Kept apart from the domain entity on purpose.</summary>
internal sealed class ChannelRecord
{
    public required string ChannelId { get; set; }

    public string? TokenHash { get; set; }

    public string? TokenHint { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastStateAt { get; set; }

    public required decimal UiScale { get; set; }
}
