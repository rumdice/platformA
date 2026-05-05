namespace PlatformA.MySqlDB.Lib.DBWebApp.Entities
{
    /// <summary>
    /// 테이블: user
    /// 게임 내 유저 정보.
    /// </summary>
    public class User
    {
        public long Pid { get; set; }    // pid BIGINT PK
        public long Uid { get; set; }    // uid BIGINT (플랫폼 Player.Id 참조)
        public string Name { get; set; } = string.Empty; // name VARCHAR(50)
        public int Level { get; set; }   // level INT
    }
}
