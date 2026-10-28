using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class InvitationTemplate
{
    public int Id { get; set; }

    public string TemplateName { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string TemplateBody { get; set; } = null!;

    public long? SeqKey { get; set; }

    public virtual ApplicationLookup? SeqKeyNavigation { get; set; }
}
