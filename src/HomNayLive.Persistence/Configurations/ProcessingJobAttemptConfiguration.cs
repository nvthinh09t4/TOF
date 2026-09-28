using HomNayLive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomNayLive.Persistence.Configurations;

public class ProcessingJobAttemptConfiguration : IEntityTypeConfiguration<ProcessingJobAttempt>
{
    public void Configure(EntityTypeBuilder<ProcessingJobAttempt> builder)
    {
        builder.ToTable("ProcessingJobAttempts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AttemptNumber).IsRequired();
        builder.Property(x => x.StartedAtUtc).HasColumnType("datetime2(0)");
        builder.Property(x => x.FinishedAtUtc).HasColumnType("datetime2(0)");
        builder.Property(x => x.Outcome).HasConversion<int>();
        builder.Property(x => x.DiagnosticsReference).HasMaxLength(1024);

        builder.HasOne(x => x.ProcessingJob).WithMany(x => x.Attempts).HasForeignKey(x => x.ProcessingJobId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ProcessingJobId, x.AttemptNumber }).IsUnique().HasDatabaseName("UX_ProcessingJobAttempts_ProcessingJobId_AttemptNumber");
    }
}
