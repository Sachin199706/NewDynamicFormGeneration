
using NewDynamicFormGenAPI.Models.DTOs.Rules;

namespace NewDynamicFormGenAPI.Models.DTOs.Forms;

/// <summary>
/// Data transfer object for listing forms in a summary view.
/// </summary>
/// <remarks>
/// Used in paginated form listings to display form metadata without full definition payloads.
/// Contains essential information for form identification and status determination.
/// </remarks>
public class FormListItemDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the form.
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// Gets or sets the unique code identifier for the form, used for human-readable reference.
    /// </summary>
    public string FormCode { get; set; } = null!;

    /// <summary>
    /// Gets or sets the display name of the form.
    /// </summary>
    public string FormName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the optional description or summary of the form's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the date when the form was created.
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the most recent date when the form or its versions were modified.
    /// </summary>
    public DateTime ModifiedDate { get; set; }
}

/// <summary>
/// Data transfer object for form creation and update requests.
/// </summary>
/// <remarks>
/// This DTO is used by API endpoints to accept form creation and edit requests from clients.
/// It contains only the fields that can be modified by form designers.
/// </remarks>
public class CreateFormDto
{
    /// <summary>
    /// Gets or sets the display name for the form.
    /// </summary>
    public string FormName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the unique code identifier for the form.
    /// </summary>
    /// <remarks>
    /// The form code must be unique across the system and is typically used
    /// in URLs and internal references.
    /// </remarks>
    public string FormCode { get; set; } = null!;

    /// <summary>
    /// Gets or sets an optional description of the form's purpose or usage.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Data transfer object representing a single form control with its configuration and rules.
/// </summary>
/// <remarks>
/// <para>
/// FormControlDto encapsulates a control instance as it appears on a form. Each control has:
/// <list type="bullet">
///   <item><description>Identity and type information</description></item>
///   <item><description>Display properties and configuration</description></item>
///   <item><description>Validation and conditional rules</description></item>
///   <item><description>Layout positioning and parent/child relationships</description></item>
/// </list>
/// </para>
/// </remarks>
public class FormControlDto
{
    /// <summary>
    /// Gets or sets the section key that groups this control within a logical section of the form.
    /// </summary>
    /// <remarks>
    /// <c>null</c> indicates the control is not part of any named section.
    /// Sections provide organizational grouping in the form UI.
    /// </remarks>
    public string? SectionKey { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for this control instance on the form.
    /// </summary>
    public int ControlId { get; set; }

    /// <summary>
    /// Gets or sets the unique key identifier for this control used in form data binding.
    /// </summary>
    /// <remarks>
    /// The control key is used as the property name when capturing submitted form values
    /// and must be unique within a form version.
    /// </remarks>
    public string ControlKey { get; set; } = null!;

    /// <summary>
    /// Gets or sets the control type code indicating which type of control this is.
    /// </summary>
    /// <remarks>
    /// Examples: "TextBox", "Number", "Dropdown", "DatePicker".
    /// See <see cref="Entities.ControlType.ControlCode"/>.
    /// </remarks>
    public string ControlTypeCode { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user-friendly label text displayed next to or above the control.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the placeholder text displayed inside the control as a hint.
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// Gets or sets the default value to populate the control with on initial display.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this control must be filled (is mandatory).
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this control is read-only and cannot be edited.
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this control is visible in the form UI.
    /// </summary>
    /// <remarks>
    /// Hidden controls are not displayed but may still be submitted with default or programmatic values.
    /// Visibility can change dynamically based on conditional rules.
    /// </remarks>
    public bool IsVisible { get; set; }

    /// <summary>
    /// Gets or sets the display order of this control relative to other controls on the form.
    /// </summary>
    /// <remarks>
    /// Controls are rendered in ascending order of DisplayOrder. Lower values appear first.
    /// </remarks>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Gets or sets the row position of this control in a grid layout, or <c>null</c> if not applicable.
    /// </summary>
    public int? LayoutRowIndex { get; set; }

    /// <summary>
    /// Gets or sets the column position of this control in a grid layout, or <c>null</c> if not applicable.
    /// </summary>
    public int? LayoutColumnIndex { get; set; }

    /// <summary>
    /// Gets or sets the ID of the parent control if this control is a child of another control (e.g., nested controls).
    /// </summary>
    /// <remarks>
    /// <c>null</c> indicates this control is a top-level control with no parent.
    /// This enables hierarchical form structures.
    /// </remarks>
    public int? ParentControlId { get; set; }

    /// <summary>
    /// Gets or sets control-specific properties and configuration as JSON.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This JSON may contain:
    /// <list type="bullet">
    ///   <item><description>List data sources for dropdown/select controls</description></item>
    ///   <item><description>Formatting rules (currency, date format, etc.)</description></item>
    ///   <item><description>Validation parameters (min/max length, patterns)</description></item>
    ///   <item><description>Display options specific to the control type</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public string? PropertiesJson { get; set; }

    /// <summary>
    /// Gets or sets the collection of validation and conditional rules applied to this control.
    /// </summary>
    public List<FormRuleDto> Rules { get; set; } = new();
}

/// <summary>
/// Data transfer object for saving a form version with its complete structure and configuration.
/// </summary>
/// <remarks>
/// <para>
/// SaveFormVersionDto is used when form designers save a form revision. It contains:
/// <list type="bullet">
///   <item><description>The form version ID (null for new versions, populated for updates)</description></item>
///   <item><description>The complete form structure as JSON</description></item>
///   <item><description>Layout configuration and settings</description></item>
///   <item><description>Control definitions with rules and properties</description></item>
/// </list>
/// </para>
/// </remarks>
public class SaveFormVersionDto
{
    /// <summary>
    /// Gets or sets the ID of the form this version belongs to.
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// Gets or sets the form version ID if updating an existing version, or <c>null</c> if creating a new version.
    /// </summary>
    /// <remarks>
    /// When <c>null</c>, a new version is created. When populated, the existing version is updated.
    /// </remarks>
    public int? FormVersionId { get; set; }

    /// <summary>
    /// Gets or sets an optional description of changes in this version.
    /// </summary>
    public string? VersionDescription { get; set; }

    /// <summary>
    /// Gets or sets the complete form structure as JSON, including all controls and embedded rules.
    /// </summary>
    public string FormDefinitionJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the layout configuration as JSON (e.g., column count for grid layout).
    /// </summary>
    public string? LayoutDefinitionJson { get; set; }

    /// <summary>
    /// Gets or sets the collection of form controls with their configuration.
    /// </summary>
    public List<FormControlDto> Controls { get; set; } = new();
}

/// <summary>
/// Data transfer object containing complete form version details for editing or viewing.
/// </summary>
/// <remarks>
/// FormVersionDto provides a full representation of a form version including structure,
/// controls, layout, and metadata for form builder edit views.
/// </remarks>
public class FormVersionDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this form version.
    /// </summary>
    public int FormVersionId { get; set; }

    /// <summary>
    /// Gets or sets the parent form ID.
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// Gets or sets the name of the parent form.
    /// </summary>
    public string FormName { get; set; }

    /// <summary>
    /// Gets or sets the description or changelog for this version.
    /// </summary>
    public string VersionDescription { get; set; }

    /// <summary>
    /// Gets or sets the version number.
    /// </summary>
    public int VersionNo { get; set; }

    /// <summary>
    /// Gets or sets the current status of this version (Draft, Published, Archived).
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Gets or sets the complete form definition as JSON.
    /// </summary>
    public string FormDefinitionJson { get; set; } = null!;

    /// <summary>
    /// Gets or sets the layout configuration as JSON.
    /// </summary>
    public string LayoutDefinitionJson { get; set; }

    /// <summary>
    /// Gets or sets the collection of all controls on this form version.
    /// </summary>
    public List<FormControlDto> Controls { get; set; } = new();

    /// <summary>
    /// Gets or sets the date when this version was created.
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the collection of sections that group controls logically.
    /// </summary>
    public List<FormSectionDto> Sections { get; set; } = new();
}

/// <summary>
/// Data transfer object for the public-facing form rendering payload.
/// </summary>
/// <remarks>
/// <para>
/// FormRenderDto is the optimized payload sent to the frontend for form presentation to end-users.
/// It includes:
/// <list type="bullet">
///   <item><description>Form metadata (ID, name, version)</description></item>
///   <item><description>Controls and layout configuration</description></item>
///   <item><description>Validation and conditional rules for client-side enforcement</description></item>
///   <item><description>Section groupings for organized display</description></item>
/// </list>
/// </para>
/// <para>
/// This DTO is "public-safe" in that it excludes sensitive metadata and contains only
/// information necessary to render and submit the form.
/// </para>
/// </remarks>
public class FormRenderDto
{
    /// <summary>
    /// Gets or sets the form ID.
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// Gets or sets the form version ID being rendered.
    /// </summary>
    public int FormVersionId { get; set; }

    /// <summary>
    /// Gets or sets the form name for display to users.
    /// </summary>
    public string FormName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the layout configuration JSON.
    /// </summary>
    public string? LayoutDefinitionJson { get; set; }

    /// <summary>
    /// Gets or sets all controls in the form.
    /// </summary>
    public List<FormControlDto> Controls { get; set; } = new();

    /// <summary>
    /// Gets or sets all validation and conditional rules for the form.
    /// </summary>
    public List<FormRuleDto> Rules { get; set; } = new();

    /// <summary>
    /// Gets or sets the organized sections containing controls.
    /// </summary>
    public List<FormSectionDto> Sections { get; set; } = new();
    /// <summary>
    /// Gets or sets the public identifier of the form version being rendered.
    /// </summary>
    /// <remarks>
    /// The numeric form and version IDs are deliberately not part of this payload.
    /// </remarks>
    public string PublicId { get; set; } = string.Empty;
}

/// <summary>
/// Data transfer object for form version list items in summary views.
/// </summary>
/// <remarks>
/// Used for paginated version listings showing form revision history.
/// </remarks>
public class FormVersionListItemDto
{
    /// <summary>
    /// Gets or sets the parent form ID.
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// Gets or sets the form version ID.
    /// </summary>
    public int FormVersionId { get; set; }

    /// <summary>
    /// Gets or sets the form name.
    /// </summary>
    public string FormName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the version number.
    /// </summary>
    public int VersionNo { get; set; }

    /// <summary>
    /// Gets or sets the version description.
    /// </summary>
    public string VersionDescription { get; set; }

    /// <summary>
    /// Gets or sets the current status of the version.
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Gets or sets the date when the version was last modified.
    /// </summary>
    public DateTime ModifiedDate { get; set; }
    public string PublicId { get; set; } = string.Empty;
}


/// <summary>
/// Data transfer object for form publication history entries.
/// </summary>
/// <remarks>
/// Represents a record of when a form version was published, used for audit and governance reporting.
/// </remarks>
public class FormPublishHistoryItemDto
{
    /// <summary>
    /// Gets or sets the form ID.
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// Gets or sets the form version ID that was published.
    /// </summary>
    public int FormVersionId { get; set; }

    /// <summary>
    /// Gets or sets the form name.
    /// </summary>
    public string FormName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the version description.
    /// </summary>
    public string VersionDescription { get; set; } = null!;

    /// <summary>
    /// Gets or sets the version number.
    /// </summary>
    public int VersionNo { get; set; }

    /// <summary>
    /// Gets or sets the date when the version was published.
    /// </summary>
    public DateTime PublishedOn { get; set; }
    public string PublicId { get; set; } = string.Empty;

}

/// <summary>
/// Data transfer object for dashboard analytics and statistics.
/// </summary>
/// <remarks>
/// Provides a comprehensive summary of forms and versions in the system for dashboard displays.
/// </remarks>
public class DashboardDTO
{
    /// <summary>
    /// Gets or sets the total count of forms in the system.
    /// </summary>
    public int TotalForms { get; set; }

    /// <summary>
    /// Gets or sets the total count of form versions across all forms.
    /// </summary>
    public int TotalVersions { get; set; }

    /// <summary>
    /// Gets or sets the count of archived forms.
    /// </summary>
    public int ArchivedForms { get; set; }

    /// <summary>
    /// Gets or sets the count of forms that have at least one draft version.
    /// </summary>
    public int DraftForms { get; set; }

    /// <summary>
    /// Gets or sets the count of forms that have at least one published version.
    /// </summary>
    public int PublishedForms { get; set; }

    /// <summary>
    /// Gets or sets the list of recently modified form versions.
    /// </summary>
    public List<FormVersionListItemDto> RecentForms { get; set; } = new();
}

/// <summary>
/// Data transfer object representing a logical section or tab that groups form controls.
/// </summary>
/// <remarks>
/// <para>
/// FormSectionDto organizes controls into named groups displayed as sections or tabs in the UI.
/// Sections are flat by design (sections do not contain sections, only controls).
/// </para>
/// <para>
/// This design is backward compatible: forms created before sections existed parse correctly
/// as they simply have controls with <c>SectionKey = null</c>.
/// </para>
/// </remarks>
public class FormSectionDto
{
    /// <summary>
    /// Gets or sets the unique key identifier for this section.
    /// </summary>
    /// <remarks>
    /// The section key is used to reference the section in form definitions and control assignments.
    /// </remarks>
    public string SectionKey { get; set; } = null!;

    /// <summary>
    /// Gets or sets the display title for this section shown in the form UI.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Gets or sets the display order of this section relative to other sections.
    /// </summary>
    /// <remarks>
    /// Sections are rendered in ascending order of DisplayOrder.
    /// </remarks>
    public int DisplayOrder { get; set; }
}
