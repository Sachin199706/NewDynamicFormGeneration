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
}

