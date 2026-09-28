using HomNayLive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomNayLive.Persistence.Configurations;

public class TopicTrendObservationConfiguration : IEntityTypeConfiguration<TopicTrendObservation>
{
    public void Configure(EntityTypeBuilder<TopicTrendObservation> builder)
    {
        builder.ToTable("TopicTrendObservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderKind).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ProviderKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WindowType).HasConversion<int>().IsRequired();
        builder.Property(x => x.WindowStartUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.WindowEndUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.ObservedAtUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.Score).HasColumnType("decimal(9,4)");
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Topic).WithMany(x => x.TrendObservations).HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TopicId, x.ProviderKind, x.ProviderKey, x.WindowType, x.WindowStartUtc }).IsUnique().HasDatabaseName("UX_TopicTrendObservations_WindowInstance");
        builder.HasIndex(x => new { x.TopicId, x.WindowStartUtc }).HasDatabaseName("IX_TopicTrendObservations_TopicId_WindowStartUtc");
        builder.HasIndex(x => new { x.ProviderKind, x.ProviderKey, x.ObservedAtUtc }).HasDatabaseName("IX_TopicTrendObservations_ProviderKind_ProviderKey_ObservedAtUtc");
    }
}
