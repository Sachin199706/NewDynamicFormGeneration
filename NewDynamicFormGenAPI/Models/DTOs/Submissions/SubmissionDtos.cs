namespace NewDynamicFormGenAPI.Models.DTOs.Submissions;

public class SubmitFormDto
{
    public int FormId { get; set; }
    public int FormVersionId { get; set; }
    /// <summary>ControlKey -> submitted value. File fields get their stored filename merged
    /// in here too, alongside the regular values — same request, no separate upload call.</summary>
    public Dictionary<string, object?> Values { get; set; } = new();
}

public class SubmissionListItemDto
{
    public int SubmissionId { get; set; }
    public int FormId { get; set; }
    public DateTime SubmittedOn { get; set; }
}

public class SubmissionDetailDto
{
    public int SubmissionId { get; set; }
    public int FormId { get; set; }
    public string FormName { get; set; } = null!;
    public int VersionNo { get; set; }
    public DateTime SubmittedOn { get; set; }
    public Dictionary<string, object?> Values { get; set; } = new();
}