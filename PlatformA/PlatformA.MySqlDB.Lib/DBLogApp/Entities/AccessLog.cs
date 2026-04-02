namespace PlatformA.MySqlDB.Lib.DBLogApp.Entities
{
    /// <summary>
    /// 유저의 인증 관련 행동 기록 (login / logout / refresh)
    /// </summary>
    public class AccessLog
    {
        public long Id { get; set; }
        public int PlayerId { get; set; }

        /// <summary>"login" | "logout" | "refresh"</summary>
        public string Action { get; set; } = string.Empty;

        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
