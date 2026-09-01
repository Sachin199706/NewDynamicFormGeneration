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
    public string SubmissionCode { get; set; } = null!;
    public bool IsRead { get; set; }
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

public class SubmissionOverviewItemDto
{
    public int SubmissionId { get; set; }
    public string SubmissionCode { get; set; } = null!;
    public int FormId { get; set; }
    public string FormName { get; set; } = null!;
    public int VersionNo { get; set; }
    public DateTime SubmittedOn { get; set; }
    public bool IsRead { get; set; }
}

public class SubmissionStatsDto
{
    public int TotalSubmissions { get; set; }
    public int UnreadSubmissions { get; set; }
    public int ReadSubmissions { get; set; }
}
public class SubmissionFilterDto
{
    public string? Search { get; set; }        // matches SubmissionCode or FormName
    public int? FormId { get; set; }
    public bool? IsRead { get; set; }           // null = All, true = Read, false = Unread
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}