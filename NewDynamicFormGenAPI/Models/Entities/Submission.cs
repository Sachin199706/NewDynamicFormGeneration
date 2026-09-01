namespace NewDynamicFormGenAPI.Models.Entities;

public class FormSubmission
{
    public int SubmissionId { get; set; }
    public int FormId { get; set; }
    public Form Form { get; set; } = null!;
    public int FormVersionId { get; set; }
    public DateTime SubmittedOn { get; set; } = DateTime.UtcNow;

    /// <summary>Full raw snapshot of submitted key/value pairs, for quick re-display.</summary>
    public string JsonData { get; set; } = "{}";

    public bool IsRead { get; set; } = false;

    /// <summary>{FormCode}-v{VersionNo}-{SubmissionId} — built after insert, once SubmissionId exists.</summary>
    public string SubmissionCode { get; set; } = null!;
}

