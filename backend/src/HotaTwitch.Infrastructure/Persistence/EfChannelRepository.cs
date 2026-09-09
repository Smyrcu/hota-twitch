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

    public async Task SaveAsync(Channel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);

        var record = await database.Channels
            .SingleOrDefaultAsync(candidate => candidate.ChannelId == channel.Id.Value, cancellationToken);

        if (record is null)
        {
            database.Channels.Add(channel.ToRecord());
        }
        else
        {
            channel.CopyInto(record);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(ChannelId channelId, CancellationToken cancellationToken) =>
        await database.Channels
            .Where(candidate => candidate.ChannelId == channelId.Value)
            .ExecuteDeleteAsync(cancellationToken);
}
