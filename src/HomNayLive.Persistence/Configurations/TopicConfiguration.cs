using HomNayLive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomNayLive.Persistence.Configurations;

public class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("Topics");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.FirstSeenAtUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.NormalizedName).IsUnique().HasDatabaseName("UX_Topics_NormalizedName");
        builder.HasIndex(x => x.FirstSeenAtUtc).HasDatabaseName("IX_Topics_FirstSeenAtUtc");
    }
}
