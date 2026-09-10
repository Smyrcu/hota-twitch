using AwesomeAssertions;
using HotaTwitch.Domain.Channels;
using HotaTwitch.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace HotaTwitch.Api.Tests.Persistence;

/// <summary>
/// The service migrates at startup, so the schema has to arrive both on a database that has never
/// existed and on one a previous release left behind.
/// </summary>
public sealed class ChannelMigrationTests : IDisposable
{
    private const string CreateChannels = "20260909190237_CreateChannels";

    private static readonly DateTimeOffset Now = new(2026, 9, 9, 22, 0, 0, TimeSpan.Zero);

    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"hota-twitch-migration-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(databasePath);
    }

    [Fact]
    public async Task Migrate_FreshDatabase_StoresAChannelWithTheDefaultScale()
    {
        await using var database = NewContext();
        await database.Database.MigrateAsync(TestContext.Current.CancellationToken);

        database.Channels.Add(Channel.Create(new ChannelId("42"), Now).ToRecord());
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        var stored = await ReadAsync("42");
        stored.Settings.UiScale.Should().Be(1m);
        stored.HasToken.Should().BeFalse();
    }

    [Fact]
    public async Task Migrate_DatabaseCreatedByThePreviousSchema_GivesExistingChannelsTheDefaultScale()
    {
        await MigrateToAsync(CreateChannels);
        await InsertLegacyChannelAsync("42", "d3ad", "hts_ab…");

        await using (var database = NewContext())
        {
            await database.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        var stored = await ReadAsync("42");
        stored.Settings.UiScale.Should().Be(1m);
        stored.TokenHint.Should().Be("hts_ab…");
        stored.TokenHash.Should().Be(new TokenHash("d3ad"));
        stored.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Migrate_DatabaseCreatedByThePreviousSchema_LetsAChannelDropItsToken()
    {
        await MigrateToAsync(CreateChannels);
        await InsertLegacyChannelAsync("42", "d3ad", "hts_ab…");

        await using (var database = NewContext())
        {
            await database.Database.MigrateAsync(TestContext.Current.CancellationToken);
            var record = await database.Channels.SingleAsync(row => row.ChannelId == "42", TestContext.Current.CancellationToken);
            record.TokenHash = null;
            record.TokenHint = null;
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await ReadAsync("42")).HasToken.Should().BeFalse();
    }

    [Fact]
    public async Task Migrate_FreshDatabase_KeepsTokenlessChannelsApartUnderTheUniqueIndex()
    {
        await using var database = NewContext();
        await database.Database.MigrateAsync(TestContext.Current.CancellationToken);

        database.Channels.Add(Channel.Create(new ChannelId("42"), Now).ToRecord());
        database.Channels.Add(Channel.Create(new ChannelId("43"), Now).ToRecord());
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        var count = await database.Channels.CountAsync(TestContext.Current.CancellationToken);
        count.Should().Be(2, "SQLite treats NULLs in a unique index as distinct");
    }

    [Fact]
    public async Task Migrate_FreshDatabase_StillRefusesTwoChannelsSharingAToken()
    {
        await using var database = NewContext();
        await database.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var token = StreamerToken.Generate();
        database.Channels.Add(WithToken("42", token).ToRecord());
        database.Channels.Add(WithToken("43", token).ToRecord());

        var save = async () => await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        await save.Should().ThrowAsync<DbUpdateException>();
    }

    private static Channel WithToken(string channelId, StreamerToken token)
    {
        var channel = Channel.Create(new ChannelId(channelId), Now);
        channel.RotateToken(token);
        return channel;
    }

    private HotaTwitchDbContext NewContext() =>
        new(new DbContextOptionsBuilder<HotaTwitchDbContext>().UseSqlite($"Data Source={databasePath}").Options);

    private async Task MigrateToAsync(string migration)
    {
        await using var database = NewContext();
        await database.GetService<IMigrator>().MigrateAsync(migration, TestContext.Current.CancellationToken);
    }

    private async Task InsertLegacyChannelAsync(string channelId, string tokenHash, string tokenHint)
    {
        await using var database = NewContext();
        await database.Database.ExecuteSqlRawAsync(
            "INSERT INTO channels (channel_id, token_hash, token_hint, created_at, last_state_at) VALUES ({0}, {1}, {2}, {3}, NULL)",
            [channelId, tokenHash, tokenHint, "2026-09-09 22:00:00+00:00"],
            TestContext.Current.CancellationToken);
    }

    private async Task<Channel> ReadAsync(string channelId)
    {
        await using var database = NewContext();
        var record = await database.Channels
            .AsNoTracking()
            .SingleAsync(row => row.ChannelId == channelId, TestContext.Current.CancellationToken);

        return record.ToDomain();
    }
}
