using HomNayLive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomNayLive.Persistence.Configurations;

public class AiSummaryConfiguration : IEntityTypeConfiguration<AiSummary>
{
    public void Configure(EntityTypeBuilder<AiSummary> builder)
    {
        builder.ToTable("AiSummaries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.ProviderKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ModelKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.GeneratedAtUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.ScopeTopic).WithMany(x => x.Summaries).HasForeignKey(x => x.ScopeTopicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ScopeArticle).WithMany(x => x.Summaries).HasForeignKey(x => x.ScopeArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ScopeTopicId).HasDatabaseName("IX_AiSummaries_ScopeTopicId");
        builder.HasIndex(x => x.ScopeArticleId).HasDatabaseName("IX_AiSummaries_ScopeArticleId");

        builder.ToTable(tb =>
        {
            tb.HasCheckConstraint("CK_AiSummaries_ScopeXor", "(ScopeTopicId IS NULL AND ScopeArticleId IS NOT NULL) OR (ScopeTopicId IS NOT NULL AND ScopeArticleId IS NULL)");
        });
    }
}
