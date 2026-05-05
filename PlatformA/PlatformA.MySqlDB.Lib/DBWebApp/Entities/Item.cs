namespace PlatformA.MySqlDB.Lib.DBWebApp.Entities
{
    /// <summary>
    /// 테이블: item
    /// 플레이어 보유 아이템 정보.
    /// </summary>
    public class Item
    {
        public long Pid { get; set; }    // pid BIGINT PK
        public long Tid { get; set; }    // tid BIGINT (아이템 템플릿 ID)
        public string Name { get; set; } = string.Empty; // name VARCHAR(50)
        public long Uid { get; set; }    // uid BIGINT (소유자 User.Pid)
        public int Grade { get; set; }   // grade INT
    }
}
