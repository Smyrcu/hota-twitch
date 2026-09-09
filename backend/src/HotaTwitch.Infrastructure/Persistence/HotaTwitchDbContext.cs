using Microsoft.EntityFrameworkCore;

namespace HotaTwitch.Infrastructure.Persistence;

internal sealed class HotaTwitchDbContext(DbContextOptions<HotaTwitchDbContext> options) : DbContext(options)
{
    public DbSet<ChannelRecord> Channels => Set<ChannelRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<ChannelRecord>(channel =>
        {
            channel.ToTable("channels");
            channel.HasKey(record => record.ChannelId);
            channel.Property(record => record.ChannelId).HasColumnName("channel_id").HasMaxLength(32);
            channel.Property(record => record.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
            channel.Property(record => record.TokenHint).HasColumnName("token_hint").HasMaxLength(32);
            channel.Property(record => record.CreatedAt).HasColumnName("created_at");
            channel.Property(record => record.LastStateAt).HasColumnName("last_state_at");
            channel.HasIndex(record => record.TokenHash).IsUnique().HasDatabaseName("ix_channels_token_hash");
        });
    }
}
