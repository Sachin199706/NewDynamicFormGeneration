using Microsoft.AspNetCore.Mvc;
using NewDynamicFormGenAPI.Models.Common;
using NewDynamicFormGenAPI.Models.DTOs.Forms;
using NewDynamicFormGenAPI.Models.DTOs.Rules;
using NewDynamicFormGenAPI.Models.DTOs.Submissions;

namespace NewDynamicFormGenAPI.Models.Interfaces;

/// <summary>
/// Defines the contract for form management operations including creation, versioning, publishing, and retrieval.
/// </summary>
/// <remarks>
/// <para>
/// IFormService is the primary service for managing the form lifecycle. It orchestrates:
/// <list type="bullet">
///   <item><description>Form CRUD operations and metadata management</description></item>
///   <item><description>Form versioning and version history</description></item>
///   <item><description>Publishing forms to make them available for public use</description></item>
///   <item><description>Generating form payloads for rendering on the frontend</description></item>
///   <item><description>Dashboard and analytics queries</description></item>
/// </list>
/// </para>
/// <para>
/// This service typically delegates to repositories and rule engine services for validation execution
/// and uses DTOs to communicate between the API layer and business logic.
/// </para>
/// </remarks>
public interface IFormService
{
    /// <summary>
    /// Retrieves a paginated list of forms with optional search and date filtering.
    /// </summary>
    /// <param name="aNumPage">The page number (1-based) to retrieve.</param>
    /// <param name="aNumPageSize">The maximum number of forms per page.</param>
    /// <param name="aStrSearch">Optional text to search in form code or name.</param>
    /// <param name="fromDate">Optional start date to filter by creation date.</param>
    /// <param name="toDate">Optional end date to filter by creation date.</param>
    /// <returns>A paginated result containing form list items matching the criteria.</returns>
    /// <remarks>
    /// This method is used by the form listing/dashboard UI to display available forms with search and filtering.
    /// </remarks>
    Task<PagedResult<FormListItemDto>> GetFormsAsync(int aNumPage, int aNumPageSize, string? aStrSearch, DateTime? fromDate, DateTime? toDate);

    /// <summary>
    /// Creates a new form with the provided metadata.
    /// </summary>
    /// <param name="aObjDto">The form creation data including code, name, and description.</param>
    /// <returns>A result containing the newly created form details if successful.</returns>
    /// <remarks>
    /// Creating a form establishes a new form template container. The form itself does not have any structure
    /// until a version is created and populated with form controls.
    /// </remarks>
    Task<Result<FormListItemDto>> CreateFormAsync(CreateFormDto aObjDto);

    /// <summary>
    /// Updates the metadata of an existing form.
    /// </summary>
    /// <param name="aNumFormId">The ID of the form to update.</param>
    /// <param name="aObjDto">The updated form metadata.</param>
    /// <returns>A result containing the updated form details if successful.</returns>
    /// <remarks>
    /// This method updates form-level metadata (name, code, description) but does not modify form versions or structure.
    /// </remarks>
    Task<Result<FormListItemDto>> UpdateFormAsync(int aNumFormId, CreateFormDto aObjDto);

    /// <summary>
    /// Retrieves a paginated list of all versions for a specific form with optional filtering.
    /// </summary>
    /// <param name="aNumFormId">The ID of the form to retrieve versions for.</param>
    /// <param name="aNumPage">The page number (1-based) to retrieve.</param>
    /// <param name="aNumPageSize">The maximum number of versions per page.</param>
    /// <param name="aStrSearch">Optional text search in version descriptions.</param>
    /// <param name="fromDate">Optional start date to filter by creation date.</param>
    /// <param name="toDate">Optional end date to filter by creation date.</param>
    /// <param name="status">Optional status filter (Draft, Published, Archived).</param>
    /// <returns>A paginated result containing version list items matching the criteria.</returns>
    /// <remarks>
    /// Versions represent distinct revisions of a form. This method enables form designers to view
    /// the history of form changes and select specific versions for comparison or recovery.
    /// </remarks>
    Task<PagedResult<FormVersionListItemDto>> GetVersionsAsync(int aNumFormId, int aNumPage, int aNumPageSize, string? aStrSearch, DateTime? fromDate, DateTime? toDate, string? status);

    /// <summary>
    /// Saves a new form version with the provided structure and control definitions.
    /// </summary>
    /// <param name="aObjDto">The form version data including form definition JSON and layout configuration.</param>
    /// <returns>A result containing the saved version details if successful.</returns>
    /// <remarks>
    /// This method creates a new version within a form and persists the complete form structure
    /// as a JSON snapshot. The version is initially created in Draft status.
    /// Validation rules and control configurations are embedded within the form definition.
    /// </remarks>
    Task<Result<FormVersionDto>> SaveVersionAsync(SaveFormVersionDto aObjDto);

    /// <summary>
    /// Retrieves the most recent version of a specific form.
    /// </summary>
    /// <param name="aNumFormId">The ID of the form.</param>
    /// <returns>A result containing the latest form version details.</returns>
    /// <remarks>
    /// The "latest" version may be in Draft or Published status. This method is commonly used
    /// to populate form editing interfaces and determine which version to render for new submissions.
    /// </remarks>
    Task<Result<FormVersionDto>> GetLatestVersionAsync(int aNumFormId);

    /// <summary>
    /// Generates a complete form rendering payload for presentation to end-users.
    /// </summary>
    /// <param name="aNumFormId">The ID of the form to render.</param>
    /// <param name="aNumFormVersionId">The ID of the specific form version to render.</param>
    /// <returns>A result containing the form render data (controls, layout, rules).</returns>
    /// <remarks>
    /// <para>
    /// The render payload is constructed from the form definition JSON and includes:
    /// <list type="bullet">
    ///   <item><description>All controls with their properties and configuration</description></item>
    ///   <item><description>Validation and conditional rules for client-side enforcement</description></item>
    ///   <item><description>Layout and display settings</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This method transforms internal representation into a format optimized for the frontend Angular renderer.
    /// </para>
    /// </remarks>
    Task<Result<FormRenderDto>> GetRenderPayloadAsync(int aNumFormId, int aNumFormVersionId);

    /// <summary>
    /// Publishes a form version, making it available for end-users to submit.
    /// </summary>
    /// <param name="aNumFormId">The ID of the form.</param>
    /// <param name="aNumFormVersionId">The ID of the version to publish.</param>
    /// <returns>A result indicating success or failure of the publish operation.</returns>
    /// <remarks>
    /// <para>
    /// Publishing transitions a form version from Draft to Published status and records
    /// the publication in the form's publish history for audit purposes.
    /// </para>
    /// <para>
    /// Only published versions are available for submission by end-users. Previous published versions
    /// may be superseded or archived.
    /// </para>
    /// </remarks>
    Task<Result<bool>> PublishAsync(int aNumFormId, int aNumFormVersionId);

    /// <summary>
    /// Retrieves all versions across all forms in the system.
    /// </summary>
    /// <returns>A list of all form versions.</returns>
    /// <remarks>
    /// This method is typically used for analytics, export, or administrative dashboards that need
    /// a system-wide view of form versions.
    /// </remarks>
    Task<List<FormVersionListItemDto>> GetAllVersionsAsync();

    /// <summary>
    /// Retrieves dashboard statistics about forms in the system.
    /// </summary>
    /// <returns>Dashboard statistics including total, draft, and published form counts.</returns>
    /// <remarks>
    /// This data is typically displayed on administration dashboards to provide a quick overview
    /// of form creation and publication metrics.
    /// </remarks>
    Task<DashboardDTO> GetDashboardCountAsync();

    /// <summary>
    /// Retrieves a paginated list of publication history records across all forms, with optional text search.
    /// </summary>
    /// <param name="aNumPage">The page number (1-based) to retrieve.</param>
    /// <param name="aNumPageSize">The maximum number of publication events per page.</param>
    /// <param name="aStrSearch">Optional text to search in form name, version number, or version description.</param>
    /// <returns>A paginated result containing publish history entries, newest first.</returns>
    /// <remarks>
    /// This enables audit trails and compliance reporting for form publication governance.
    /// </remarks>
    Task<PagedResult<FormPublishHistoryItemDto>> GetPublishHistoryAsync(int aNumPage, int aNumPageSize, string? aStrSearch);

    /// <summary>
    /// Retrieves a specific form version by its ID.
    /// </summary>
    /// <param name="aNumFormVersionId">The ID of the form version to retrieve.</param>
    /// <returns>A result containing the form version details if found.</returns>
    /// <remarks>
    /// Direct version retrieval by ID is useful for edit/view operations and comparison workflows.
    /// </remarks>
    Task<Result<FormVersionDto>> GetVersionByIdAsync(int aNumFormVersionId);
    /// <summary>
    /// Retrieves a paginated list of form versions across all forms for the dashboard, with optional text search.
    /// </summary>
    /// <param name="aNumPage">The page number (1-based) to retrieve.</param>
    /// <param name="aNumPageSize">The maximum number of versions per page.</param>
    /// <param name="aStrSearch">Optional text to search in template name, version number, version description, or status.</param>
    /// <returns>A paginated result containing version list items matching the criteria, newest first.</returns>
    /// <remarks>
    /// This method backs the dashboard table. It is separate from <see cref="GetDashboardCountAsync"/>
    /// so that searching and paging the table never changes the summary counts.
    /// </remarks>
    Task<PagedResult<FormVersionListItemDto>> GetDashboardVersionsAsync(int aNumPage, int aNumPageSize, string? aStrSearch);
    /// <param name="aGuidPublicId">The public identifier of the form version to render.</param>
    Task<Result<FormRenderDto>> GetRenderPayloadAsync(Guid aGuidPublicId);
}

/// <summary>
/// Defines the contract for business rule management and execution on forms.
/// </summary>
/// <remarks>
/// <para>
/// IRuleEngineService manages the validation and conditional rules that enforce
/// business logic and control behavior on forms. Rules are version-specific and embedded
/// within <see cref="FormVersion.FormDefinitionJson"/>, but this service provides operations for
/// <list type="bullet">
///   <item><description>Retrieving rules for a form version</description></item>
///   <item><description>Adding new rules to a version</description></item>
///   <item><description>Updating existing rules by ID</description></item>
///   <item><description>Deleting rules</description></item>
/// </list>
/// </para>
/// <para>
/// Rule types include validation rules (Required, Length, Format) and conditional rules
/// (Visibility, EnableDisable, RequiredOptional). See <see cref="Enums.RuleType"/> for the complete taxonomy.
/// </para>
/// </remarks>
public interface IRuleEngineService
{
    /// <summary>
    /// Retrieves all rules configured for a specific form version.
    /// </summary>
    /// <param name="aNumFormVersionId">The ID of the form version.</param>
    /// <returns>A list of all rules applied to the version's controls.</returns>
    /// <remarks>
    /// Rules are extracted from the form definition JSON and presented in a structured format
    /// for review or modification by form designers.
    /// </remarks>
    Task<List<FormRuleDto>> GetRulesForVersionAsync(int aNumFormVersionId);

    /// <summary>
    /// Adds a new validation or conditional rule to a form version.
    /// </summary>
    /// <param name="aNumFormVersionId">The ID of the form version.</param>
    /// <param name="aObjDto">The rule definition including type, control references, and configuration.</param>
    /// <returns>The newly created rule DTO with assigned ID.</returns>
    /// <remarks>
    /// This method inserts a new rule into the form version's definition and persists the change.
    /// The rule is immediately available to enforce during form validation and conditional rendering.
    /// </remarks>
    Task<FormRuleDto> AddRuleAsync(int aNumFormVersionId, CreateFormRuleDto aObjDto);

    /// <summary>
    /// Updates an existing rule in a form version.
    /// </summary>
    /// <param name="aNumFormVersionId">The ID of the form version.</param>
    /// <param name="aStrRuleId">The unique identifier of the rule to update.</param>
    /// <param name="aObjDto">The updated rule configuration.</param>
    /// <returns>The updated rule DTO, or <c>null</c> if the rule was not found.</returns>
    /// <remarks>
    /// Updates modify rule behavior without affecting the rule ID, allowing continuity of rule references.
    /// </remarks>
    Task<FormRuleDto?> UpdateRuleAsync(int aNumFormVersionId, string aStrRuleId, CreateFormRuleDto aObjDto);

    /// <summary>
    /// Deletes a rule from a form version.
    /// </summary>
    /// <param name="aNumFormVersionId">The ID of the form version.</param>
    /// <param name="aStrRuleId">The unique identifier of the rule to delete.</param>
    /// <returns>A completed task indicating the rule has been removed.</returns>
    /// <remarks>
    /// Deleting a rule immediately stops its enforcement on form validation and behavior.
    /// Conditional rules affecting visibility or required status are removed from active form logic.
    /// </remarks>
    Task DeleteRuleAsync(int aNumFormVersionId, string aStrRuleId);
}

/// <summary>
/// Defines the contract for form submission handling, including data capture, retrieval, and analysis.
/// </summary>
/// <remarks>
/// <para>
/// ISubmissionService manages the lifecycle of user-submitted form data. It handles:
/// <list type="bullet">
///   <item><description>Capturing and validating submitted form data</description></item>
///   <item><description>Storing submissions with file attachments</description></item>
///   <item><description>Retrieving submission details and displaying form responses</description></item>
///   <item><description>Managing submission read/unread status for workflow</description></item>
///   <item><description>Providing submission statistics and analytics</description></item>
/// </list>
/// </para>
/// <para>
/// Submissions are linked to specific form versions, enabling historical tracking of which form
/// structure was in use at submission time.
/// </para>
/// </remarks>
public interface ISubmissionService
{
    /// <summary>
    /// Submits a completed form with data and optional file attachments.
    /// </summary>
    /// <param name="aObjDto">The submission data including form ID, version ID, and submitted field values.</param>
    /// <param name="aObjFiles">Any file uploads included in the form submission.</param>
    /// <returns>A result containing the newly created submission ID if successful.</returns>
    /// <remarks>
    /// <para>
    /// This method:
    /// <list type="bullet">
    ///   <item><description>Validates submitted data against form rules</description></item>
    ///   <item><description>Stores files using the file storage service</description></item>
    ///   <item><description>Persists the submission JSON snapshot</description></item>
    ///   <item><description>Generates and stores the unique submission code</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// If validation fails, the method returns a failure result with validation error messages.
    /// </para>
    /// </remarks>
    Task<Result<int>> SubmitAsync(SubmitFormDto aObjDto, IFormFileCollection aObjFiles);

    /// <summary>
    /// Retrieves the complete details of a specific form submission.
    /// </summary>
    /// <param name="aNumSubmissionId">The ID of the submission to retrieve.</param>
    /// <returns>A result containing the submission details including submitted values and metadata.</returns>
    /// <remarks>
    /// This method is used to display individual submission details to administrators or authorized users
    /// reviewing submitted form responses.
    /// </remarks>
    Task<Result<SubmissionDetailDto>> GetDetailAsync(int aNumSubmissionId);

    /// <summary>
    /// Marks a submission as read, indicating it has been reviewed.
    /// </summary>
    /// <param name="aNumSubmissionId">The ID of the submission to mark as read.</param>
    /// <returns>A result indicating success or failure of the operation.</returns>
    /// <remarks>
    /// This flag supports submission workflow and notifications, enabling administrators to track
    /// which submissions have been reviewed.
    /// </remarks>
    Task<Result<bool>> MarkAsReadAsync(int aNumSubmissionId);

    /// <summary>
    /// Retrieves a paginated list of submissions with filtering and search capabilities.
    /// </summary>
    /// <param name="aObjFilter">Filter criteria including form ID, date range, and read status.</param>
    /// <returns>A paginated result containing matching submissions.</returns>
    /// <remarks>
    /// This method supports the submission listing/review interface, enabling administrators to
    /// search, filter, and navigate through form responses.
    /// </remarks>
    Task<PagedResult<SubmissionOverviewItemDto>> GetAllSubmissionsAsync(SubmissionFilterDto aObjFilter);

    /// <summary>
    /// Retrieves system-wide submission statistics.
    /// </summary>
    /// <returns>Statistics about total submissions, unread counts, and submission metrics.</returns>
    /// <remarks>
    /// This method returns aggregated statistics across all forms in the system, useful for
    /// dashboard and administrative reporting.
    /// </remarks>
    Task<SubmissionStatsDto> GetStatsAsync();

    /// <summary>
    /// Retrieves submission statistics for a specific form.
    /// </summary>
    /// <param name="anumID">The ID of the form to retrieve statistics for.</param>
    /// <returns>Statistics specific to the requested form including submission count and unread count.</returns>
    /// <remarks>
    /// This method provides form-specific metrics for analytics and status monitoring.
    /// </remarks>
    Task<SubmissionStatsDto> GetStatsAsync(int anumID);
    /// <summary>
    /// Submits a completed form identified by the public identifier of its version.
    /// </summary>
    /// <param name="aGuidPublicId">The public identifier of the form version that was filled in.</param>
    /// <param name="aObjValues">The submitted field values keyed by control key.</param>
    /// <param name="aObjFiles">Any file uploads included in the form submission.</param>
    /// <returns>A result containing the newly created submission ID if successful.</returns>
    /// <remarks>
    /// This is the entry point for the shareable fill link. It looks up the form and version
    /// from the public identifier and then follows the same steps as <see cref="SubmitAsync"/>.
    /// </remarks>
    Task<Result<int>> SubmitByPublicIdAsync(Guid aGuidPublicId, Dictionary<string, object?> aObjValues, IFormFileCollection aObjFiles);
}
