using HomNayLive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomNayLive.Persistence.Configurations;

public class TopicAliasConfiguration : IEntityTypeConfiguration<TopicAlias>
{
    public void Configure(EntityTypeBuilder<TopicAlias> builder)
    {
        builder.ToTable("TopicAliases");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TopicId).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NormalizedValue).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.NormalizedValue).IsUnique().HasDatabaseName("UX_TopicAliases_NormalizedValue");
        builder.HasIndex(x => x.TopicId).HasDatabaseName("IX_TopicAliases_TopicId");
        builder.HasOne(x => x.Topic).WithMany(x => x.TopicAliases).HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
    }
}
