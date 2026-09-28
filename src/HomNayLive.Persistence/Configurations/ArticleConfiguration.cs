using HomNayLive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomNayLive.Persistence.Configurations;

public class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("Articles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CanonicalUrl).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(500);
        builder.Property(x => x.ContentHash).HasMaxLength(64);
        builder.Property(x => x.PublicationAtUtc).HasColumnType("datetime2(0)");
        builder.Property(x => x.LifecycleState).HasConversion<int>().IsRequired();
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.CanonicalUrl).IsUnique().HasDatabaseName("UX_Articles_CanonicalUrl");
        builder.HasIndex(x => x.ContentHash).HasDatabaseName("IX_Articles_ContentHash");
    }
}
