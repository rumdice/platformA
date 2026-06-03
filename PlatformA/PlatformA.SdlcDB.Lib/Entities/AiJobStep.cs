namespace PlatformA.SdlcDB.Lib.Entities;

public sealed class AiJobStep
{
    public long Id { get; set; }
    public long JobId { get; set; }
    public AiJob? Job { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int? DurationSec { get; set; }
    public string? ResultJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
