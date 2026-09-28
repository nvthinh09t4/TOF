using HomNayLive.Domain.Enums;

namespace HomNayLive.Domain.Entities;

public class ProcessingJobAttempt
{
    public long Id { get; set; }
    public long ProcessingJobId { get; set; }
    public int AttemptNumber { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public ProcessingJobAttemptOutcome? Outcome { get; set; }
    public string? DiagnosticsReference { get; set; }

    public ProcessingJob ProcessingJob { get; set; } = null!;
}
