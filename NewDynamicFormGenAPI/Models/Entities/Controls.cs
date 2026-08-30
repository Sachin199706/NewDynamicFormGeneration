namespace NewDynamicFormGenAPI.Models.Entities;

/// <summary>The toolbox catalog — replaces the old controls.xml, now DB-driven.</summary>
public class ControlType
{
    public int ControlTypeId { get; set; }
    public string ControlCode { get; set; } = null!;   // TextBox, Number, Dropdown, ...
    public string ControlName { get; set; } = null!;
    public string? Category { get; set; }
    public string? ComponentName { get; set; }          // Angular component selector
    public string? DefaultPropertiesJson { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}