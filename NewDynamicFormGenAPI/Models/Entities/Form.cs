namespace NewDynamicFormGenAPI.Models.Entities;

/// <summary>
/// Represents a form template that can have multiple versions and accept submissions.
/// </summary>
/// <remarks>
/// <para>
/// A Form serves as the top-level container for form metadata and workflow. Each form can have multiple versions
/// to track revisions and manage form updates over time. Forms can also receive multiple submissions from users
/// filling out the form through the application interface.
/// </para>
/// <para>
/// Form definitions are stored as JSON metadata within <see cref="FormVersion.FormDefinitionJson"/>,
/// enabling dynamic control generation and rendering without requiring schema changes.
/// </para>
/// </remarks>
public class Form
{
    /// <summary>
    /// Gets or sets the unique identifier for the form.
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// Gets or sets the unique code identifier for the form, used as a human-readable reference.
    /// </summary>
    /// <remarks>This code is typically used in URLs and reports to identify forms.</remarks>
    public string FormCode { get; set; } = null!;

    /// <summary>
    /// Gets or sets the display name of the form.
    /// </summary>
    public string FormName { get; set; } = null!;

    /// <summary>
    /// Gets or sets an optional description or summary of the form's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the form was created in UTC.
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date and time when the form metadata was last modified in UTC, or <c>null</c> if never modified.
    /// </summary>
    public DateTime? ModifiedDate { get; set; }

    /// <summary>
    /// Gets or sets the collection of all versions associated with this form.
    /// </summary>
    /// <remarks>
    /// Each version represents a distinct revision of the form. Forms can have multiple versions to track
    /// evolution and allow switching between different form configurations.
    /// </remarks>
    public ICollection<FormVersion> Versions { get; set; } = new List<FormVersion>();

    /// <summary>
    /// Gets or sets the collection of all submissions received for this form.
    /// </summary>
    /// <remarks>
    /// Submissions can span multiple form versions. The <see cref="FormSubmission.FormVersionId"/>
    /// property indicates which version of the form was used for each submission.
    /// </remarks>
    public ICollection<FormSubmission> Submissions { get; set; } = new List<FormSubmission>();

}

/// <summary>
/// Represents a specific version of a form, containing the complete form definition and configuration.
/// </summary>
/// <remarks>
/// <para>
/// FormVersion is the cornerstone of the dynamic form generation system. Each version contains:
/// <list type="bullet">
///   <item><description>A complete snapshot of all form controls with their configuration as JSON</description></item>
///   <item><description>Layout settings such as column configuration</description></item>
///   <item><description>Embedded validation and business rules for each control</description></item>
///   <item><description>Status tracking (Draft, Published, Archived)</description></item>
/// </list>
/// </para>
/// <para>
/// The JSON-based approach allows arbitrary control properties and rules to be stored without schema changes,
/// enabling rapid iteration and extensibility of form capabilities.
/// </para>
/// </remarks>
public class FormVersion
{
    /// <summary>
    /// Gets or sets the unique identifier for this form version.
    /// </summary>
    public int FormVersionId { get; set; }

    /// <summary>
    /// Gets or sets the parent form identifier.
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the parent <see cref="Form"/>.
    /// </summary>
    public Form Form { get; set; } = null!;

    /// <summary>
    /// Gets or sets the version number, typically an incrementing integer for this form.
    /// </summary>
    /// <remarks>Version numbers are used to track the evolution of a form's structure.</remarks>
    public int VersionNo { get; set; }

    /// <summary>
    /// Gets or sets an optional description of changes or purpose for this version.
    /// </summary>
    public string? VersionDescription { get; set; }

    /// <summary>
    /// Gets or sets the current status of this form version.
    /// </summary>
    /// <remarks>
    /// Common status values include "Draft" (unpublished), "Published" (active for use), 
    /// or "Archived" (superseded). The actual set of valid statuses should be documented
    /// as part of the business rules for the application.
    /// </remarks>
    public string Status { get; set; } = "Draft";

    /// <summary>
    /// Gets or sets the complete form definition as JSON.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This JSON document is the single source of truth for form structure. It contains a complete snapshot
    /// of all controls on the form, where each control includes:
    /// <list type="bullet">
    ///   <item><description>Control type and unique identifier</description></item>
    ///   <item><description>Display properties (label, placeholder, help text)</description></item>
    ///   <item><description>Configuration (list options, default values, formatting)</description></item>
    ///   <item><description>Validation rules (required, min/max, custom expressions)</description></item>
    ///   <item><description>Conditional rendering rules (show/hide based on other control values)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This approach replaces the legacy separate FormControls and FormRules tables, simplifying the data model
    /// and enabling atomic updates of form definitions.
    /// </para>
    /// </remarks>
    public string FormDefinitionJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the layout configuration as JSON, typically containing grid or column settings.
    /// </summary>
    /// <remarks>
    /// Example value: <c>{"columnLayout": 4}</c> indicates a 4-column grid layout.
    /// This property is kept separate from the form definition for cleaner separation of structural
    /// concerns (layout) from form content (controls and rules).
    /// </remarks>
    public string? LayoutDefinitionJson { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this form version was created in UTC.
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date and time when this form version was published in UTC, or <c>null</c> if not yet published.
    /// </summary>
    public DateTime? PublishedDate { get; set; }

    /// <summary>
    /// Gets or sets the random public identifier used in the shareable fill link.
    /// </summary>
    /// <remarks>
    /// The fill link carries this value in place of the numeric form and version IDs,
    /// so a link cannot be guessed by counting up from another one.
    /// </remarks>
    public Guid PublicId { get; set; } = Guid.NewGuid();
}
