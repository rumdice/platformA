using System;
using System.Collections.Generic;

namespace PlatformA.MySqlDB.Lib.DBWebApp;

public partial class UserEntity
{
    public long? pid { get; set; }

    public long? uid { get; set; }

    public string? name { get; set; }

    public int? level { get; set; }
}
