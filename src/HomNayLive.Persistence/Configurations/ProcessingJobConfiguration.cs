using HomNayLive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomNayLive.Persistence.Configurations;

public class ProcessingJobConfiguration : IEntityTypeConfiguration<ProcessingJob>
{
    public void Configure(EntityTypeBuilder<ProcessingJob> builder)
    {
        builder.ToTable("ProcessingJobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.JobType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.State).HasConversion<int>().IsRequired();
        builder.Property(x => x.StartAtUtc).HasColumnType("datetime2(0)");
        builder.Property(x => x.FinishAtUtc).HasColumnType("datetime2(0)");
        builder.Property(x => x.DiagnosticsReference).HasMaxLength(1024);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => new { x.JobType, x.State, x.CreatedAtUtc }).HasDatabaseName("IX_ProcessingJobs_JobType_State_CreatedAtUtc");
    }
}
