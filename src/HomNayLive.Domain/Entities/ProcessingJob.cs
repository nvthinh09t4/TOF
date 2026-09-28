using HomNayLive.Domain.Enums;

namespace HomNayLive.Domain.Entities;

public class ProcessingJob
{
    public long Id { get; set; }
    public string JobType { get; set; } = string.Empty;
    public ProcessingJobState State { get; set; }
    public DateTime? StartAtUtc { get; set; }
    public DateTime? FinishAtUtc { get; set; }
    public string? DiagnosticsReference { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<ProcessingJobAttempt> Attempts { get; set; } = new List<ProcessingJobAttempt>();
}
