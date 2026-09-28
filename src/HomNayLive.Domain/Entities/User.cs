namespace HomNayLive.Domain.Entities;

public class User
{
    public long Id { get; set; }
    public string IdentityReference { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
