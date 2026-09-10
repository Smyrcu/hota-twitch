using HotaTwitch.Domain.Channels;
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
            // SQLite treats NULLs in a UNIQUE index as distinct, so the index below keeps holding
            // while any number of channels carry no token at all.
            channel.Property(record => record.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
            channel.Property(record => record.TokenHint).HasColumnName("token_hint").HasMaxLength(32);
            channel.Property(record => record.CreatedAt).HasColumnName("created_at");
            channel.Property(record => record.LastStateAt).HasColumnName("last_state_at");
            // SQLite has no decimal type, so EF warns that its default decimal mapping cannot be
            // ordered or compared in SQL. The scale is only ever read back, never queried on, so it
            // is stored as its invariant text and the warning does not apply.
            channel.Property(record => record.UiScale)
                .HasColumnName("ui_scale")
                .HasConversion<string>()
                .HasDefaultValue(ChannelSettings.DefaultUiScale);
            channel.HasIndex(record => record.TokenHash).IsUnique().HasDatabaseName("ix_channels_token_hash");
        });
    }
}
