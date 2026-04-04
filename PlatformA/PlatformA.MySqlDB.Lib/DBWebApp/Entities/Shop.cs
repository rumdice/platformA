using System;
using System.Collections.Generic;

namespace PlatformA.MySqlDB.Lib.DBWebApp.Entities;

public partial class Shop
{
    public long? Pid { get; set; }

    public long? Tid { get; set; }

    public string? Name { get; set; }

    public long? Uid { get; set; }
}
