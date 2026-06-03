namespace PlatformA.SdlcDB.Lib.Entities
{
    public sealed class AiFailure
    {
        public long Id { get; set; }
        public long? JobId { get; set; }
        public AiJob? Job { get; set; }
        public string FailureType { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? LogExcerpt { get; set; }
        public bool? FixableByAi { get; set; }
        public int RetryCount { get; set; }
        public bool Resolved { get; set; }
        public DateTimeOffset? ResolvedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string? Metadata { get; set; }
    }
}
