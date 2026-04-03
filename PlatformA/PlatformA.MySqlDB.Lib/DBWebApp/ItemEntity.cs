using System;
using System.Collections.Generic;

namespace PlatformA.MySqlDB.Lib.DBWebApp;

public partial class ItemEntity
{
    public long? pid { get; set; }

    public long? tid { get; set; }

    public string? name { get; set; }

    public long? uid { get; set; }

    public int? grade { get; set; }
}
