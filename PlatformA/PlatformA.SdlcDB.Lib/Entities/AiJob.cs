namespace PlatformA.SdlcDB.Lib.Entities
{
    public sealed class AiJob
    {
        public long Id { get; set; }
        public int? Sprint { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? PrUrl { get; set; }
        public bool TestGenerated { get; set; }
        public bool ReviewCompleted { get; set; }
        public bool AdrRequired { get; set; }
        public int? DurationSec { get; set; }
        public long? ConsumeTokens { get; set; }
        public long? CacheTokens { get; set; }
        public int RetryCount { get; set; }
        public string? LastError { get; set; }
        public string? SourceJson { get; set; }
        public string? Impact { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateTimeOffset InsertedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public ICollection<AiJobStep> Steps { get; set; } = new List<AiJobStep>();
        public ICollection<AiFailure> Failures { get; set; } = new List<AiFailure>();
        public ICollection<AiModelRun> ModelRuns { get; set; } = new List<AiModelRun>();
    }
}
