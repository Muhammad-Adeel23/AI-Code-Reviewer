using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class EmailNotification
{
    public long Id { get; set; }

    public string To { get; set; } = null!;

    public string? From { get; set; }

    public string? Cc { get; set; }

    public string? Bcc { get; set; }

    public string Subject { get; set; } = null!;

    public string Body { get; set; } = null!;

    public long StatusId { get; set; }

    public int? Retries { get; set; }

    public string? Message { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public long? TemplateSeqKey { get; set; }

    public virtual ApplicationLookup Status { get; set; } = null!;

    public virtual ApplicationLookup? TemplateSeqKeyNavigation { get; set; }
}
