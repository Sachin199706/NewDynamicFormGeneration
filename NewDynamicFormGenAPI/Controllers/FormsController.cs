using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using NewDynamicFormGenAPI.Models.DTOs.Forms;
using NewDynamicFormGenAPI.Models.Interfaces;

namespace NewDynamicFormGenAPI.API.Controllers;

// No auth in this application — every screen, including this one, is reachable by URL alone.

/// <summary>
/// API controller for form management and lifecycle operations.
/// </summary>
/// <remarks>
/// <para>
/// FormsController provides REST API endpoints for:
/// <list type="bullet">
///   <item><description>Creating, reading, updating form templates</description></item>
///   <item><description>Managing form versions and design iterations</description></item>
///   <item><description>Publishing forms to make them available for submissions</description></item>
///   <item><description>Retrieving form render payloads for frontend display</description></item>
///   <item><description>Accessing form and publication history</description></item>
///   <item><description>Dashboard and analytics queries</description></item>
/// </list>
/// </para>
/// <para>
/// All endpoints require no authentication; access control (if needed in the future)
/// should be added via [Authorize] attributes and authorization policies.
/// </para>
/// </remarks>
[ApiController]
[Route("api/forms")]
public class FormsController : ControllerBase
{
    private readonly IFormService _formService;
    private const int MaxDashboardPageSize = 50;
      private const int MaxPageSize = 50;
    /// <summary>
    /// Initializes a new instance of the <see cref="FormsController"/> class.
    /// </summary>
    /// <param name="formService">The form service dependency providing business logic operations.</param>
    public FormsController(IFormService formService)
    {
        _formService = formService;
    }

    /// <summary>
    /// Retrieves a paginated list of forms with optional search and date filtering.
    /// </summary>
    /// <param name="aNumPage">The page number (1-based) to retrieve. Default: 1.</param>
    /// <param name="aNumPageSize">The maximum number of forms per page. Default: 10.</param>
    /// <param name="search">Optional text to search in form name, code, or description.</param>
    /// <param name="fromDate">Optional start date to filter forms by creation date.</param>
    /// <param name="toDate">Optional end date to filter forms by creation date.</param>
    /// <returns>A paginated result containing form list items matching the criteria.</returns>
    /// <remarks>
    /// This endpoint is used by the form listing/dashboard interface in the form builder UI
    /// to display available forms with search and date range filtering.
    /// 
    /// Example: <c>GET /api/forms?page=1&pageSize=10&search=employee</c>
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> GetForms(
        [FromQuery(Name = "page"), Range(1, int.MaxValue)] int aNumPage = 1,
        [FromQuery(Name = "pageSize"), Range(1, int.MaxValue)] int aNumPageSize = 10,
        [FromQuery] string? search = null, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        var lobjResult = await _formService.GetFormsAsync(aNumPage, aNumPageSize, search, fromDate, toDate);
        return Ok(lobjResult);
    }

    /// <summary>
    /// Creates a new form template with the provided metadata.
    /// </summary>
    /// <param name="aObjDto">The form creation data including name, code, and description.</param>
    /// <returns>The newly created form details if successful; BadRequest if validation fails.</returns>
    /// <remarks>
    /// Creating a form creates the template container. The form does not have any structure
    /// until a version is created and saved with form controls and configuration.
    /// Form codes must be unique across the system.
    /// </remarks>
    [HttpPost]
    public async Task<IActionResult> CreateForm([FromBody] CreateFormDto aObjDto)
    {
        var lobjResult = await _formService.CreateFormAsync(aObjDto);
        return lobjResult.Success ? Ok(lobjResult) : BadRequest(lobjResult);
    }

    /// <summary>
    /// Updates an existing form's metadata.
    /// </summary>
    /// <param name="aNumFormId">The ID of the form to update.</param>
    /// <param name="aObjDto">The updated form metadata.</param>
    /// <returns>The updated form details if successful; NotFound if form does not exist.</returns>
    /// <remarks>
    /// This endpoint updates form-level information (name, code, description) but does not
    /// modify form versions or structure. To modify form controls, use the SaveVersion endpoint.
    /// </remarks>
    [HttpPut("{aNumFormId:int}")]
    public async Task<IActionResult> UpdateForm(int aNumFormId, [FromBody] CreateFormDto aObjDto)
    {
        var lobjResult = await _formService.UpdateFormAsync(aNumFormId, aObjDto);
        return lobjResult.Success ? Ok(lobjResult) : NotFound(lobjResult);
    }

    /// <summary>
    /// Retrieves the most recent version of a specific form.
    /// </summary>
    /// <param name="aNumFormId">The ID of the form.</param>
    /// <returns>The latest form version details if successful; NotFound if form does not exist.</returns>
    /// <remarks>
    /// The latest version may be in Draft or Published status. This endpoint is typically used
    /// by form editing interfaces to load the most recent version for modification.
    /// </remarks>
    [HttpGet("{aNumFormId:int}/versions/latest")]
    public async Task<IActionResult> GetLatestVersion(int aNumFormId)
    {
        var lobjResult = await _formService.GetLatestVersionAsync(aNumFormId);
        return lobjResult.Success ? Ok(lobjResult) : NotFound(lobjResult);
    }

    /// <summary>
    /// Retrieves a paginated list of all versions for a specific form with optional filtering.
    /// </summary>
    /// <param name="aNumFormId">The ID of the form to retrieve versions for.</param>
    /// <param name="page">The page number (1-based). Default: 1.</param>
    /// <param name="pageSize">The maximum items per page. Default: 10.</param>
    /// <param name="search">Optional text search in version descriptions.</param>
    /// <param name="fromDate">Optional start date to filter by creation date.</param>
    /// <param name="toDate">Optional end date to filter by creation date.</param>
    /// <param name="status">Optional status filter (Draft, Published, Archived).</param>
    /// <returns>A paginated result containing matchingform versions.</returns>
    /// <remarks>
    /// This endpoint enables designers to view the complete version history of a form,
    /// supporting version comparison, recovery, and status tracking workflows.
    /// </remarks>
    [HttpGet("{aNumFormId:int}/versions")]
    public async Task<IActionResult> GetVersions(int aNumFormId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null,
        [FromQuery] string? status = null)
    {
        var lobjResult = await _formService.GetVersionsAsync(aNumFormId, page, pageSize, search, fromDate, toDate, status);
        return Ok(lobjResult);
    }

    /// <summary>
    /// Saves a new form version with the complete form structure, controls, and configuration.
    /// </summary>
    /// <param name="aObjDto">The form version data including definition JSON, layout, and controls.</param>
    /// <returns>The saved version details if successful; BadRequest if validation fails.</returns>
    /// <remarks>
    /// <para>
    /// This endpoint enables form designers to save form revisions. Each save creates a new version
    /// or updates an existing one, capturing the complete form structure including:
    /// <list type="bullet">
    ///   <item><description>All form controls with configuration</description></item>
    ///   <item><description>Validation rules embedded in controls</description></item>
    ///   <item><description>Conditional logic rules</description></item>
    ///   <item><description>Layout and section organization</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The new version is created in Draft status and must be published before it is available
    /// for public submissions.
    /// </para>
    /// </remarks>
    [HttpPost("versions")]
    public async Task<IActionResult> SaveVersion([FromBody] SaveFormVersionDto aObjDto)
    {
        var lobjResult = await _formService.SaveVersionAsync(aObjDto);
        return lobjResult.Success ? Ok(lobjResult) : BadRequest(lobjResult);
    }

    /// <summary>
    /// Publishes a form version, making it available for end-users to submit.
    /// </summary>
    /// <param name="aNumFormId">The ID of the form.</param>
    /// <param name="aNumVersionId">The ID of the version to publish.</param>
    /// <returns>Success result if publish succeeds; BadRequest if operation fails.</returns>
    /// <remarks>
    /// <para>
    /// Publishing transitions a form version from Draft to Published status and records
    /// the publication event in the form's publish history for audit purposes.
    /// </para>
    /// <para>
    /// Once published, the form version becomes available for public submission.
    /// Only published versions can receive submissions from end-users.
    /// </para>
    /// </remarks>
    [HttpPut("{aNumFormId:int}/versions/{aNumVersionId:int}/publish")]
    public async Task<IActionResult> Publish(int aNumFormId, int aNumVersionId)
    {
        var lobjResult = await _formService.PublishAsync(aNumFormId, aNumVersionId);
        return lobjResult.Success ? Ok(lobjResult) : BadRequest(lobjResult);
    }

    /// <summary>
    /// Retrieves the complete form rendering payload for displaying the form to end-users.
    /// </summary>
    /// <param name="aNumFormId">The ID of the form to render.</param>
    /// <param name="aNumVersionId">The ID of the specific version to render.</param>
    /// <returns>A form render payload containing controls, layout, rules, and configuration.</returns>
    /// <remarks>
    /// <para>
    /// This endpoint provides the complete data needed for the frontend Angular application
    /// to render the form for filling. The payload includes:
    /// <list type="bullet">
    ///   <item><description>All controls with properties and configuration</description></item>
    ///   <item><description>Validation and conditional rules</description></item>
    ///   <item><description>Layout and section organization</description></item>
    ///   <item><description>Default values and control metadata</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This is "public-safe" data suitable for transmission to browsers and frontend applications.
    /// </para>
    /// </remarks>
    [HttpGet("{aNumFormId:int}/versions/{aNumVersionId:int}/render")]
    public async Task<IActionResult> GetRenderPayload(int aNumFormId, int aNumVersionId)
    {
        var lobjResult = await _formService.GetRenderPayloadAsync(aNumFormId, aNumVersionId);
        return lobjResult.Success ? Ok(lobjResult) : NotFound(lobjResult);
    }

    /// <summary>
    /// Retrieves all form versions across the entire system.
    /// </summary>
    /// <returns>A list of all form versions from all forms.</returns>
    /// <remarks>
    /// This endpoint is typically used by dashboard and administrative interfaces
    /// to display system-wide form version information and analytics.
    /// </remarks>
    [HttpGet("versions/all")]
    public async Task<IActionResult> GetAllVersions()
    {
        var lobjResult = await _formService.GetAllVersionsAsync();
        return Ok(lobjResult);
    }

    /// <summary>
    /// Retrieves form and version statistics for the dashboard.
    /// </summary>
    /// <returns>Dashboard statistics including total forms, draft count, and published count.</returns>
    /// <remarks>
    /// This endpoint provides high-level metrics displayed on the form management dashboard
    /// to give administrators a quick overview of form creation and publication activity.
    /// </remarks>
    [HttpGet("versions/dashboardcount")]
    public async Task<IActionResult> DashboardCount()
    {
        var lobjResult = await _formService.GetDashboardCountAsync();
        return Ok(lobjResult);
    }

    /// <summary>
    /// Retrieves the complete publication history for all forms in the system.
    /// </summary>
    /// <returns>A list of all form publication events ordered by publication date.</returns>
    /// <remarks>
    /// This endpoint provides an audit trail of form publication events, supporting
    /// compliance requirements, change management, and historical analysis.
    /// </remarks>
    [HttpGet("publish-history")]
    public async Task<IActionResult> GetPublishHistory([FromQuery(Name = "page"), Range(1, int.MaxValue)] int aNumPage = 1,[FromQuery(Name = "pageSize"), Range(1, MaxPageSize)] int aNumPageSize = 10,[FromQuery] string? search = null)
    {
        var lobjResult = await _formService.GetPublishHistoryAsync(aNumPage, aNumPageSize, search);
        return Ok(lobjResult);
    }

    /// <summary>
    /// Retrieves a specific form version by its unique identifier.
    /// </summary>
    /// <param name="aNumVersionId">The ID of the form version to retrieve.</param>
    /// <returns>The form version details if found; NotFound if version does not exist.</returns>
    /// <remarks>
    /// This endpoint retrieves a specific version by ID, useful for edit interfaces,
    /// version comparison workflows, and version recovery operations.
    /// </remarks>
    [HttpGet("versions/{aNumVersionId:int}")]
    public async Task<IActionResult> GetVersionById(int aNumVersionId)
    {
        var lobjResult = await _formService.GetVersionByIdAsync(aNumVersionId);
        return lobjResult.Success ? Ok(lobjResult) : NotFound(lobjResult);
    }

    /// <summary>
    /// Retrieves a paginated list of form versions across all forms for the dashboard table.
    /// </summary>
    /// <param name="aNumPage">The page number (1-based). Defaults to 1.</param>
    /// <param name="aNumPageSize">The number of versions per page (1 to 50). Defaults to 10.</param>
    /// <param name="search">Optional text to search in template name, version number, version description, or status.</param>
    /// <returns>A paginated result containing the matching form versions, newest first.</returns>
    /// <remarks>
    /// The summary counts shown above the table come from <see cref="DashboardCount"/> and are not affected by this search.
    /// </remarks>
    [HttpGet("versions/dashboard")]
    public async Task<IActionResult> GetDashboardVersions([FromQuery(Name = "page"), Range(1, int.MaxValue)] int aNumPage = 1, [FromQuery(Name = "pageSize"), Range(1, MaxDashboardPageSize)] int aNumPageSize = 10, [FromQuery] string? search = null)
    {
        var lobjResult = await _formService.GetDashboardVersionsAsync(aNumPage, aNumPageSize, search);
        return Ok(lobjResult);
    }
    [HttpGet("public/{aGuidPublicId:guid}/render")]
    public async Task<IActionResult> GetRenderPayload(Guid aGuidPublicId)
    {
        var lobjResult = await _formService.GetRenderPayloadAsync(aGuidPublicId);
        return lobjResult.Success ? Ok(lobjResult) : NotFound(lobjResult);
    }
}