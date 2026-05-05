namespace PlatformA.MySqlDB.Lib.DBWebApp.Entities
{
    /// <summary>
    /// 테이블: shop
    /// 상점 상품 정보.
    /// </summary>
    public class Shop
    {
        public long Pid { get; set; }    // pid BIGINT PK
        public long Tid { get; set; }    // tid BIGINT (상품 템플릿 ID)
        public string Name { get; set; } = string.Empty; // name VARCHAR(50)
        public long Uid { get; set; }    // uid BIGINT (소유자 User.Pid)
    }
}
