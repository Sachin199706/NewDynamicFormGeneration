namespace NewDynamicFormGenAPI.Models.Entities;

/// <summary>
/// Represents a control type definition in the form builder's toolbox catalog.
/// </summary>
/// <remarks>
/// <para>
/// ControlType replaces the legacy controls.xml file with a database-driven approach, enabling
/// dynamic management of available form control types without code changes.
/// </para>
/// <para>
/// Each control type defines the metadata for a specific kind of form control (e.g., TextBox, Dropdown, DatePicker)
/// that can be added to a form version. The control type catalog drives the form builder UI toolbox and the
/// form rendering engine.
/// </para>
/// </remarks>
public class ControlType
{
    /// <summary>
    /// Gets or sets the unique identifier for this control type.
    /// </summary>
    public int ControlTypeId { get; set; }

    /// <summary>
    /// Gets or sets the unique code identifier for the control type.
    /// </summary>
    /// <remarks>
    /// Examples: "TextBox", "Number", "Dropdown", "DatePicker", "CheckBox", "RadioButton".
    /// This code is used to identify control types in form definitions and is referenced by the frontend renderer.
    /// </remarks>
    public string ControlCode { get; set; } = null!;

    /// <summary>
    /// Gets or sets the human-readable display name for this control type.
    /// </summary>
    /// <remarks>
    /// This name is used in the form builder UI toolbox to allow developers and form designers to select
    /// and insert controls into form definitions.
    /// </remarks>
    public string ControlName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the category or grouping label for this control type.
    /// </summary>
    /// <remarks>
    /// Categories organize controls in the toolbox (e.g., "Input", "Selection", "Layout", "Data").
    /// </remarks>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the Angular component selector name for rendering this control.
    /// </summary>
    /// <remarks>
    /// This value is used by the Angular frontend to determine which component to instantiate
    /// when rendering a form control of this type. Examples: "app-text-input", "app-dropdown", "app-date-picker".
    /// </remarks>
    public string? ComponentName { get; set; }

    /// <summary>
    /// Gets or sets the default properties as JSON for controls of this type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This JSON template defines the default configuration for newly created controls of this type.
    /// It may include properties such as:
    /// <list type="bullet">
    ///   <item><description>Validation rules (required, min/max, patterns)</description></item>
    ///   <item><description>Display options (placeholder, label, help text)</description></item>
    ///   <item><description>Formatting rules (currency, date format, number precision)</description></item>
    ///   <item><description>Data source configuration for list-based controls</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public string? DefaultPropertiesJson { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this control type is currently available for use in form builder.
    /// </summary>
    /// <remarks>
    /// Inactive control types are hidden from the form builder toolbox and cannot be used in new forms,
    /// though existing forms may still contain controls of inactive types.
    /// </remarks>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the display order for this control type in the form builder toolbox.
    /// </summary>
    /// <remarks>
    /// Controls are sorted by this value in ascending order when displaying the toolbox to form designers.
    /// Lower values appear first in the toolbox UI.
    /// </remarks>
    public int DisplayOrder { get; set; }
}
