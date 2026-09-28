using HomNayLive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomNayLive.Persistence.Configurations;

public class ArticleSourceLinkConfiguration : IEntityTypeConfiguration<ArticleSourceLink>
{
    public void Configure(EntityTypeBuilder<ArticleSourceLink> builder)
    {
        builder.ToTable("ArticleSourceLinks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OriginalUrl).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.DiscoveredAtUtc).HasColumnType("datetime2(0)").IsRequired();

        builder.HasOne(x => x.Article).WithMany(x => x.ArticleSourceLinks).HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Source).WithMany(x => x.ArticleSourceLinks).HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ArticleId, x.SourceId }).IsUnique().HasDatabaseName("UX_ArticleSourceLinks_ArticleId_SourceId");
        builder.HasIndex(x => x.SourceId).HasDatabaseName("IX_ArticleSourceLinks_SourceId");
        builder.HasIndex(x => x.ArticleId).HasDatabaseName("IX_ArticleSourceLinks_ArticleId");
    }
}
