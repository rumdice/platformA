namespace PlatformA.SdlcDB.Lib.Entities;

public sealed class AiModelRun
{
    public long Id { get; set; }
    public long? JobId { get; set; }
    public AiJob? Job { get; set; }
    public long? StepId { get; set; }
    public AiJobStep? Step { get; set; }
    public string? ModelName { get; set; }
    public string? Provider { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? CacheReadTokens { get; set; }
    public long? CacheCreationTokens { get; set; }
    public long? TotalTokens { get; set; }
    public decimal? EstimatedCost { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? RawUsage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
