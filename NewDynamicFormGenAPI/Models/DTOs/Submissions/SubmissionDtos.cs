
namespace NewDynamicFormGenAPI.Models.DTOs.Submissions;

public class SubmitFormDto
{
    public int FormId { get; set; }
    public int FormVersionId { get; set; }
    /// <summary>ControlKey -> submitted value (files handled separately via /api/files/upload).</summary>
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
