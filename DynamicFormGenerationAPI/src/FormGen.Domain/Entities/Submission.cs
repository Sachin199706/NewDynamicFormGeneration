using System;
using System.Collections.Generic;

namespace FormGen.Domain.Entities
{
    public class FormSubmission
    {
        public int SubmissionId { get; set; }
        public int FormId { get; set; }
        public Form Form { get; set; } = null!;
        public int FormVersionId { get; set; }
        public DateTime SubmittedOn { get; set; } = DateTime.UtcNow;

        /// <summary>Full raw snapshot of submitted key/value pairs, for quick re-display.</summary>
        public string JsonData { get; set; } = "{}";

        public ICollection<FormSubmissionValue> Values { get; set; } = new List<FormSubmissionValue>();
        public ICollection<FormFile> Files { get; set; } = new List<FormFile>();
    }

    /// <summary>Normalized, per-field row — used for reporting/queries across submissions.</summary>
    public class FormSubmissionValue
    {
        public int SubmissionValueId { get; set; }
        public int SubmissionId { get; set; }
        public FormSubmission Submission { get; set; } = null!;
        public int ControlId { get; set; }
        public string? Value { get; set; }
    }

    public class FormFile
    {
        public int FileId { get; set; }
        public int SubmissionId { get; set; }
        public FormSubmission Submission { get; set; } = null!;
        public int ControlId { get; set; }
        public string FileName { get; set; } = null!;
        public string StoragePath { get; set; } = null!;
        public string? ContentType { get; set; }
        public long? FileSizeBytes { get; set; }
        public DateTime UploadedOn { get; set; } = DateTime.UtcNow;
    }
}
