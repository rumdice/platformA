using System;
using System.Collections.Generic;

namespace PlatformA.MySqlDB.Lib.DBWebApp.Entities
{
    /// <summary>
    /// EF Core 마이그레이션 이력 테이블. 자동 관리되며 직접 수정하지 않습니다.
    /// </summary>
    public partial class EfmigrationsHistory
    {
        public string MigrationId { get; set; } = null!;

        public string ProductVersion { get; set; } = null!;
    }
}
