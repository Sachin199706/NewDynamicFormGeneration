namespace NewDynamicFormGenAPI.Models.DTOs.Submissions;

/// <summary>
/// Data transfer object for submitting a completed form with user-entered data.
/// </summary>
/// <remarks>
/// <para>
/// SubmitFormDto is the payload sent from the frontend when a user completes and submits a form.
/// It contains:
/// <list type="bullet">
///   <item><description>Form and version identifiers indicating which form was submitted</description></item>
///   <item><description>Submitted control values as key/value pairs</description></item>
///   <item><description>File references for file upload controls</description></item>
/// </list>
/// </para>
/// <para>
/// Files are handled through the standard ASP.NET Core file upload mechanism (IFormFile collection)
/// rather than being embedded in this DTO, but file field values in the Values dictionary
/// contain the stored file identifiers after the file storage service processes them.
/// </para>
/// </remarks>
public class SubmitFormDto
{
    /// <summary>
    /// Gets or sets the ID of the form being submitted.
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the form version being submitted.
    /// </summary>
    /// <remarks>
    /// This enables tracking which specific form revision the user filled out.
    /// </remarks>
    public int FormVersionId { get; set; }

    /// <summary>
    /// Gets or sets the submitted form data as a dictionary mapping control keys to submitted values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dictionary structure maps control key (unique identifier) to submitted value:
    /// <list type="bullet">
    ///   <item><description>For simple controls: the entered value</description></item>
    ///   <item><description>For list controls: the selected option value</description></item>
    ///   <item><description>For file controls: the stored filename (assigned after upload)</description></item>
    ///   <item><description>For unsubmitted/hidden controls: null or omitted from dictionary</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// File uploads are processed during form submission: files are saved by the file storage service,
    /// and their stored filenames are merged into this Values dictionary alongside regular submitted values.
    /// This ensures the submission snapshot contains all data including file references in a single location.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> Values { get; set; } = new();
}

/// <summary>
/// Data transfer object for listing submissions in a summary view.
/// </summary>
/// <remarks>
/// Used in paginated submission lists to display quick overview information about each submission.
/// </remarks>
public class SubmissionListItemDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this submission.
    /// </summary>
    public int SubmissionId { get; set; }

    /// <summary>
    /// Gets or sets the form ID for the submitted form.
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this submission was received.
    /// </summary>
    public DateTime SubmittedOn { get; set; }

    /// <summary>
    /// Gets or sets the human-readable submission code (e.g., "EmployeeForm-v2-1045").
    /// </summary>
    /// <remarks>
    /// This code uniquely identifies the submission and is suitable for display in reports and links.
    /// </remarks>
    public string SubmissionCode { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether this submission has been read/reviewed.
    /// </summary>
    public bool IsRead { get; set; }
}

/// <summary>
/// Data transfer object containing complete submission details for review or display.
/// </summary>
/// <remarks>
/// SubmissionDetailDto provides the full context and submitted values for viewing a specific submission
/// in detail, used by submission review interfaces.
/// </remarks>
public class SubmissionDetailDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this submission.
    /// </summary>
    public int SubmissionId { get; set; }

    /// <summary>
    /// Gets or sets the form ID.
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// Gets or sets the form name for display.
    /// </summary>
    public string FormName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the form version number that was submitted.
    /// </summary>
    public int VersionNo { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this submission was received.
    /// </summary>
    public DateTime SubmittedOn { get; set; }

    /// <summary>
    /// Gets or sets the complete submitted form data including all control values.
    /// </summary>
    /// <remarks>
    /// The dictionary maps control keys to their submitted values, enabling display and analysis
    /// of user responses.
    /// </remarks>
    public Dictionary<string, object?> Values { get; set; } = new();
}

/// <summary>
/// Data transfer object for submissions in overview list views.
/// </summary>
/// <remarks>
/// Provides high-level information about a submission suitable for dashboard and list displays.
/// </remarks>
public class SubmissionOverviewItemDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this submission.
    /// </summary>
    public int SubmissionId { get; set; }

    /// <summary>
    /// Gets or sets the submission code.
    /// </summary>
    public string SubmissionCode { get; set; } = null!;

    /// <summary>
    /// Gets or sets the form ID.
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// Gets or sets the form version ID that was submitted.
    /// </summary>
    public int FormVersionId { get; set; }

    /// <summary>
    /// Gets or sets the form name.
    /// </summary>
    public string FormName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the version number of the form that was submitted.
    /// </summary>
    public int VersionNo { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this submission was received.
    /// </summary>
    public DateTime SubmittedOn { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this submission has been read.
    /// </summary>
    public bool IsRead { get; set; }
    /// <summary>
    /// Gets or sets the public identifier of the form version, used to build the fill link.
    /// </summary>
    public string PublicId { get; set; } = string.Empty;
}

/// <summary>
/// Data transfer object containing submission statistics and metrics.
/// </summary>
/// <remarks>
/// SubmissionStatsDto provides summary counts and metrics used for dashboard and monitoring displays.
/// </remarks>
public class SubmissionStatsDto
{
    /// <summary>
    /// Gets or sets the total count of submissions.
    /// </summary>
    public int TotalSubmissions { get; set; }

    /// <summary>
    /// Gets or sets the count of submissions that have not been read/reviewed.
    /// </summary>
    public int UnreadSubmissions { get; set; }

    /// <summary>
    /// Gets or sets the count of submissions that have been read/reviewed.
    /// </summary>
    public int ReadSubmissions { get; set; }
}

/// <summary>
/// Data transfer object for filtering and searching submissions.
/// </summary>
/// <remarks>
/// <para>
/// SubmissionFilterDto encapsulates filter and search criteria for retrieving a subset of submissions,
/// along with pagination and sorting parameters. All filter fields are optional; omitted fields mean
/// "no filter for that criterion."
/// </para>
/// </remarks>
public class SubmissionFilterDto
{
    /// <summary>
    /// Gets or sets an optional text search string.
    /// </summary>
    /// <remarks>
    /// The search is performed against submission code or form name fields.
    /// </remarks>
    public string? Search { get; set; }

    /// <summary>
    /// Gets or sets an optional form ID to filter by.
    /// </summary>
    /// <remarks>
    /// When provided, only submissions for the specified form are returned.
    /// </remarks>
    public int? FormId { get; set; }

    /// <summary>
    /// Gets or sets an optional filter by read/unread status.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><c>null</c> - Include all submissions regardless of read status</description></item>
    ///   <item><description><c>true</c> - Include only read submissions</description></item>
    ///   <item><description><c>false</c> - Include only unread submissions</description></item>
    /// </list>
    /// </remarks>
    public bool? IsRead { get; set; }

    /// <summary>
    /// Gets or sets an optional start date for filtering by submission time.
    /// </summary>
    /// <remarks>
    /// Submissions on or after this date are included.
    /// </remarks>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Gets or sets an optional end date for filtering by submission time.
    /// </summary>
    /// <remarks>
    /// Submissions on or before this date are included.
    /// </remarks>
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Gets or sets the field to sort results by.
    /// </summary>
    /// <remarks>
    /// Default value is "submittedOn". Other valid values depend on the backend implementation
    /// (e.g., "status", "formName").
    /// </remarks>
    public string SortBy { get; set; } = "submittedOn";

    /// <summary>
    /// Gets or sets the sort direction.
    /// </summary>
    /// <remarks>
    /// Valid values: "asc" (ascending) or "desc" (descending).
    /// Default is "desc".
    /// </remarks>
    public string SortDirection { get; set; } = "desc";

    /// <summary>
    /// Gets or sets the page number (1-based) to retrieve.
    /// </summary>
    /// <remarks>
    /// Default value is 1 (first page).
    /// </remarks>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum number of submissions per page.
    /// </summary>
    /// <remarks>
    /// Default value is 10 items per page.
    /// </remarks>
    public int PageSize { get; set; } = 10;
}
