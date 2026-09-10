using HotaTwitch.Application.Abstractions;
using HotaTwitch.Domain.Channels;
using Microsoft.EntityFrameworkCore;

namespace HotaTwitch.Infrastructure.Persistence;

internal sealed class EfChannelRepository(HotaTwitchDbContext database) : IChannelRepository
{
    public async Task<Channel?> FindByTokenHashAsync(TokenHash tokenHash, CancellationToken cancellationToken)
    {
        var record = await database.Channels
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash.Value, cancellationToken);

        return record?.ToDomain();
    }

    public async Task<Channel?> FindByIdAsync(ChannelId channelId, CancellationToken cancellationToken)
    {
        var record = await database.Channels
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.ChannelId == channelId.Value, cancellationToken);

        return record?.ToDomain();
    }

    public Task SaveAsync(Channel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return WriteOrInsertAsync(channel, token => TryWriteTokenAsync(channel, token), cancellationToken);
    }

    public async Task<bool> TouchLastStateAsync(Channel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);

        if (channel.TokenHash is not { } tokenHash)
        {
            return false;
        }

        var updated = await database.Channels
            .Where(candidate => candidate.ChannelId == channel.Id.Value && candidate.TokenHash == tokenHash.Value)
            .ExecuteUpdateAsync(row => row.SetProperty(record => record.LastStateAt, channel.LastStateAt), cancellationToken);

        return updated > 0;
    }

    public Task SaveSettingsAsync(Channel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return WriteOrInsertAsync(channel, token => TryWriteSettingsAsync(channel, token), cancellationToken);
    }

    /// <summary>
    /// Applies <paramref name="write"/> to the channel's row, inserting the row when it has none.
    /// Issuing a token and storing settings both reach a channel that does not exist yet, so the
    /// two can find it missing at the same moment; whichever insert lands first wins and the other
    /// call writes onto that row instead of failing.
    /// </summary>
    private async Task WriteOrInsertAsync(
        Channel channel,
        Func<CancellationToken, Task<bool>> write,
        CancellationToken cancellationToken)
    {
        if (await write(cancellationToken))
        {
            return;
        }

        database.Channels.Add(channel.ToRecord());

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            if (!await write(cancellationToken))
            {
                throw;
            }
        }
    }

    private async Task<bool> TryWriteTokenAsync(Channel channel, CancellationToken cancellationToken)
    {
        var record = await database.Channels
            .SingleOrDefaultAsync(candidate => candidate.ChannelId == channel.Id.Value, cancellationToken);

        if (record is null)
        {
            return false;
        }

        channel.CopyInto(record);
        await database.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<bool> TryWriteSettingsAsync(Channel channel, CancellationToken cancellationToken)
    {
        var uiScale = channel.Settings.UiScale;

        var updated = await database.Channels
            .Where(candidate => candidate.ChannelId == channel.Id.Value)
            .ExecuteUpdateAsync(row => row.SetProperty(record => record.UiScale, uiScale), cancellationToken);

        return updated > 0;
    }
}
