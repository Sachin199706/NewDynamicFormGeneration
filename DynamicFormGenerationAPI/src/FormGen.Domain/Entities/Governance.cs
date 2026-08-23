using System;

namespace FormGen.Domain.Entities
{
    public class FormPublishHistory
    {
        public int PublishHistoryId { get; set; }
        public int FormId { get; set; }
        public int FormVersionId { get; set; }
        public DateTime PublishedOn { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }
    }

    public class FormAuditLog
    {
        public int AuditLogId { get; set; }
        public int? FormId { get; set; }
        public string Action { get; set; } = null!;   // Created | Updated | Published | Archived | Deleted
        public string? Details { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
