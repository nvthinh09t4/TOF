using HomNayLive.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomNayLive.Persistence;

public class HomNayLiveDbContext : DbContext
{
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<TopicAlias> TopicAliases => Set<TopicAlias>();
    public DbSet<TopicTrendObservation> TopicTrendObservations => Set<TopicTrendObservation>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<ArticleSourceLink> ArticleSourceLinks => Set<ArticleSourceLink>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<AiSummary> AiSummaries => Set<AiSummary>();
    public DbSet<AiSummarySourceLink> AiSummarySourceLinks => Set<AiSummarySourceLink>();
    public DbSet<TopicArticleLink> TopicArticleLinks => Set<TopicArticleLink>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();
    public DbSet<ProcessingJobAttempt> ProcessingJobAttempts => Set<ProcessingJobAttempt>();

    public HomNayLiveDbContext(DbContextOptions<HomNayLiveDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HomNayLiveDbContext).Assembly);
    }
}
