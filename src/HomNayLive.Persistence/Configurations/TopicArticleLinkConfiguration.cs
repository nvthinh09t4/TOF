using HomNayLive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomNayLive.Persistence.Configurations;

public class TopicArticleLinkConfiguration : IEntityTypeConfiguration<TopicArticleLink>
{
    public void Configure(EntityTypeBuilder<TopicArticleLink> builder)
    {
        builder.ToTable("TopicArticleLinks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LinkedAtUtc).HasColumnType("datetime2(0)").IsRequired();

        builder.HasOne(x => x.Topic).WithMany(x => x.TopicArticleLinks).HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Article).WithMany(x => x.TopicArticleLinks).HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TopicId, x.ArticleId }).IsUnique().HasDatabaseName("UX_TopicArticleLinks_TopicId_ArticleId");
        builder.HasIndex(x => x.ArticleId).HasDatabaseName("IX_TopicArticleLinks_ArticleId");
    }
}
