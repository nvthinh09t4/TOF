using HomNayLive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomNayLive.Persistence.Configurations;

public class AiSummarySourceLinkConfiguration : IEntityTypeConfiguration<AiSummarySourceLink>
{
    public void Configure(EntityTypeBuilder<AiSummarySourceLink> builder)
    {
        builder.ToTable("AiSummarySourceLinks");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.AiSummary).WithMany(x => x.GroundingLinks).HasForeignKey(x => x.AiSummaryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Article).WithMany(x => x.AiSummarySourceLinks).HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AiSummaryId, x.ArticleId }).IsUnique().HasDatabaseName("UX_AiSummarySourceLinks_AiSummaryId_ArticleId");
    }
}
