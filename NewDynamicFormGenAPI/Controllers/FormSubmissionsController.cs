using Microsoft.AspNetCore.Mvc;
using NewDynamicFormGenAPI.Models.DTOs.Submissions;
using NewDynamicFormGenAPI.Models.Interfaces;
using System.Text.Json;

namespace NewDynamicFormGenAPI.API.Controllers;

/// <summary>
/// API controller for form submission lifecycle management and analytics.
/// </summary>
/// <remarks>
/// <para>
/// FormSubmissionsController provides endpoints for form fill-in and submission capture,
/// including submission detail retrieval, marking submissions as read, and analytics queries.
/// The controller handles both form data and file uploads in a single submission operation.
/// </para>
/// <para>
/// This controller supports two primary workflows:
/// <list type="bullet">
/// <item><description>Public form fill (end-users submit forms via public URL)</description></item>
/// <item><description>Admin submission review (viewing submitted responses and analytics)</description></item>
/// </list>
/// </para>
/// <para>
/// All endpoints are publicly accessible without authentication.
/// If access control is required in the future, authorization attributes should be added.
/// </para>
/// </remarks>
[ApiController]
[Route("api")]
public class FormSubmissionsController : ControllerBase
{
    private readonly ISubmissionService _submissionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormSubmissionsController"/> class.
    /// </summary>
    /// <param name="submissionService">The submission service for handling form responses.</param>
    public FormSubmissionsController(ISubmissionService submissionService)
    {
        _submissionService = submissionService;
    }

    /// <summary>
    /// Submits a completed form with form data and optional file attachments.
    /// </summary>
    /// <param name="aNumFormId">The form ID being submitted.</param>
    /// <param name="values">
    /// A JSON string containing the form control values as key-value pairs.
    /// Expected format: <c>{"controlKey1": "value1", "controlKey2": "value2", ...}</c>
    /// </param>
    /// <param name="formVersionId">The specific form version ID that was filled in by the user.</param>
    /// <returns>
    /// An HTTP 200 OK response containing a <see cref="Result"/> object with submission details
    /// if the submission is valid; otherwise, HTTP 400 Bad Request with validation error messages.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This endpoint is the primary form submission entry point for end-users. It accepts:
    /// <list type="bullet">
    /// <item><description>Form control values as a JSON string in the values parameter</description></item>
    /// <item><description>File attachments via the multipart/form-data request body</description></item>
    /// <item><description>Form ID and version ID for tracking and versioning</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Processing steps:
    /// <list type="number">
    /// <item><description>Deserialize form values from the JSON string</description></item>
    /// <item><description>Validate control values against form rules for the specified version</description></item>
    /// <item><description>Process and store file attachments</description></item>
    /// <item><description>Create submission record with JSON snapshot of form data</description></item>
    /// <item><description>Return submission confirmation or validation errors</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Request content type: <c>multipart/form-data</c> with fields: values (JSON string), formVersionId (integer)
    /// </para>
    /// </remarks>
    [HttpPost("forms/{aNumFormId:int}/submissions")]
    public async Task<IActionResult> Submit(int aNumFormId, [FromForm] string values, [FromForm] int formVersionId)
    {
        var lobjDto = new SubmitFormDto
        {
            FormId = aNumFormId,
            FormVersionId = formVersionId,
            Values = JsonSerializer.Deserialize<Dictionary<string, object?>>(values) ?? new Dictionary<string, object?>()
        };

        var lobjResult = await _submissionService.SubmitAsync(lobjDto, Request.Form.Files);
        return lobjResult.Success ? Ok(lobjResult) : BadRequest(lobjResult);
    }

    /// <summary>
    /// Retrieves the complete details of a specific form submission.
    /// </summary>
    /// <param name="submissionId">The submission ID to retrieve.</param>
    /// <returns>
    /// An HTTP 200 OK response containing the <see cref="SubmissionDetailDto"/> with full submission data
    /// if found; otherwise, HTTP 404 Not Found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This endpoint returns comprehensive submission information including:
    /// <list type="bullet">
    /// <item><description>Submission metadata (SubmissionId, FormId, SubmissionDate, SubmissionCode)</description></item>
    /// <item><description>Form version reference and version-specific controls/rules</description></item>
    /// <item><description>Form data snapshot as JSON</description></item>
    /// <item><description>File attachments list with URLs</description></item>
    /// <item><description>Read status for admin workflows</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Used by admin screens to view and review submitted form responses.
    /// </para>
    /// </remarks>
    [HttpGet("submissions/{submissionId:int}")]
    public async Task<IActionResult> GetDetail(int submissionId)
    {
        var result = await _submissionService.GetDetailAsync(submissionId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Marks a submission as read by an administrator.
    /// </summary>
    /// <param name="aNumSubmissionId">The submission ID to mark as read.</param>
    /// <returns>
    /// An HTTP 200 OK response with confirmation if the submission is found and updated;
    /// otherwise, HTTP 404 Not Found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This endpoint updates the IsRead flag for a submission, indicating that an administrator
    /// has reviewed the submission. This is typically used in admin dashboards to track which
    /// submissions have been reviewed and which require attention.
    /// </para>
    /// <para>
    /// The operation is idempotent; marking an already-read submission returns success.
    /// </para>
    /// </remarks>
    [HttpPut("submissions/{aNumSubmissionId:int}/mark-read")]
    public async Task<IActionResult> MarkAsRead(int aNumSubmissionId)
    {
        var lobjResult = await _submissionService.MarkAsReadAsync(aNumSubmissionId);
        return lobjResult.Success ? Ok(lobjResult) : NotFound(lobjResult);
    }

    /// <summary>
    /// Retrieves a paginated list of submissions with optional filtering and search.
    /// </summary>
    /// <param name="aObjFilter">
    /// Query filter object containing:
    /// <list type="bullet">
    /// <item><description>FormId (optional) – Filter submissions by form</description></item>
    /// <item><description>Page – Page number (1-based)</description></item>
    /// <item><description>PageSize – Results per page</description></item>
    /// <item><description>SearchTerm (optional) – Search submission code or control values</description></item>
    /// <item><description>FromDate, ToDate (optional) – Date range filter</description></item>
    /// </list>
    /// </param>
    /// <returns>
    /// An HTTP 200 OK response containing a <see cref="PagedResult{SubmissionListItemDto}"/>
    /// with the filtered submission list and pagination metadata.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This endpoint supports admin submission listing and searching. It returns a paginated result set
    /// with brief submission summaries (SubmissionListItemDto) for efficient list display.
    /// </para>
    /// <para>
    /// Pagination is enforced; large datasets are automatically split into pages to prevent
    /// excessive data transfer and improve UI responsiveness.
    /// </para>
    /// </remarks>
    [HttpGet("submissions")]
    public async Task<IActionResult> GetAllSubmissions([FromQuery] SubmissionFilterDto aObjFilter)
    {
        var lobjResult = await _submissionService.GetAllSubmissionsAsync(aObjFilter);
        return Ok(lobjResult);
    }

    /// <summary>
    /// Retrieves submission statistics for the entire system or a specific form.
    /// </summary>
    /// <returns>
    /// An HTTP 200 OK response containing a <see cref="SubmissionStatsDto"/> with system-wide
    /// submission counts and metrics.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This overload returns global statistics across all forms and submissions, including:
    /// <list type="bullet">
    /// <item><description>Total submissions submitted</description></item>
    /// <item><description>Submissions marked as read (reviewed)</description></item>
    /// <item><description>Submissions pending review (unread)</description></item>
    /// <item><description>Aggregate counts by form</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Used in admin dashboard and real-time notification scenarios.
    /// </para>
    /// </remarks>
    [HttpGet("submissions/stats")]
    public async Task<IActionResult> GetStats()
    {
        var lobjResult = await _submissionService.GetStatsAsync();
        return Ok(lobjResult);
    }

    /// <summary>
    /// Retrieves submission statistics for a specific form.
    /// </summary>
    /// <param name="inumID">The form ID to retrieve submission statistics for.</param>
    /// <returns>
    /// An HTTP 200 OK response containing a <see cref="SubmissionStatsDto"/> with form-specific
    /// submission counts and metrics.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This overload returns statistics specific to a single form, including:
    /// <list type="bullet">
    /// <item><description>Submissions submitted for the form</description></item>
    /// <item><description>Submissions marked as read (reviewed)</description></item>
    /// <item><description>Submissions pending review (unread)</description></item>
    /// <item><description>Time-based trends if tracked in the database</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Used in form detail views to show submission analytics and engagement metrics.
    /// </para>
    /// </remarks>
    [HttpGet("submissions/stats/{inumID:int}")]
    public async Task<IActionResult> GetStats(int inumID)
    {
        var lobjResult = await _submissionService.GetStatsAsync(inumID);
        return Ok(lobjResult);
    }

    [HttpPost("forms/public/{aGuidPublicId:guid}/submissions")]
    public async Task<IActionResult> Submit(Guid aGuidPublicId, [FromForm] string values)
    {
        var lobjValues = JsonSerializer.Deserialize<Dictionary<string, object?>>(values) ?? new Dictionary<string, object?>();

        var lobjResult = await _submissionService.SubmitByPublicIdAsync(aGuidPublicId, lobjValues, Request.Form.Files);
        return lobjResult.Success ? Ok(lobjResult) : BadRequest(lobjResult);
    }
}
