using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class ApplicationLookupType
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public bool? IsActive { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public long CreatedBy { get; set; }

    public DateTimeOffset? UpdatedOn { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual ICollection<ApplicationLookup> ApplicationLookups { get; set; } = new List<ApplicationLookup>();
}
