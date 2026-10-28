using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class Setting
{
    public long Id { get; set; }

    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public long? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedOn { get; set; }

    public long? UpdatedBy { get; set; }

    public bool IsShow { get; set; }
}
