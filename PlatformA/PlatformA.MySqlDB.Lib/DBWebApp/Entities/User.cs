using System;
using System.Collections.Generic;

namespace PlatformA.MySqlDB.Lib.DBWebApp.Entities;

public partial class User
{
    public long? Pid { get; set; }

    public long? Uid { get; set; }

    public string? Name { get; set; }

    public int? Level { get; set; }
}
