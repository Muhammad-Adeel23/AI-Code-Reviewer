using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class ApplicationLookup
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public long? SeqKey { get; set; }

    public int? ParentId { get; set; }

    public int? TypeId { get; set; }

    public bool? IsActive { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public long CreatedBy { get; set; }

    public DateTimeOffset? UpdatedOn { get; set; }

    public long? UpdatedBy { get; set; }

    public string? Icon { get; set; }

    public string? ColorCode { get; set; }

    public string? LocaleJson { get; set; }

    public virtual ICollection<EmailNotification> EmailNotificationStatuses { get; set; } = new List<EmailNotification>();

    public virtual ICollection<EmailNotification> EmailNotificationTemplateSeqKeyNavigations { get; set; } = new List<EmailNotification>();

    public virtual ICollection<InvitationTemplate> InvitationTemplates { get; set; } = new List<InvitationTemplate>();

    public virtual ApplicationLookupType? Type { get; set; }
}
