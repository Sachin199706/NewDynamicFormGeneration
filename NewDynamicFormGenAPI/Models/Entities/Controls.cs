

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

public class FormControl
{
    public int ControlId { get; set; }
    public int FormVersionId { get; set; }
    public FormVersion FormVersion { get; set; } = null!;

    public string ControlKey { get; set; } = null!;     // stable field key, e.g. "employeeName"
    public int ControlTypeId { get; set; }
    public ControlType ControlType { get; set; } = null!;

    public string? ControlName { get; set; }
    public string? Label { get; set; }
    public string? Placeholder { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsVisible { get; set; } = true;
    public int DisplayOrder { get; set; }

    public int? ParentControlId { get; set; }
    public FormControl? ParentControl { get; set; }

    public string? PropertiesJson { get; set; }         // control-specific overrides
    public string? ValidationJson { get; set; }         // denormalized cache of active rules (perf)

    public int? DataSourceId { get; set; }
    public FormDataSource? DataSource { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }

    public ICollection<FormRule> Rules { get; set; } = new List<FormRule>();
}

public class FormLayout
{
    public int LayoutId { get; set; }
    public int FormVersionId { get; set; }
    public FormVersion FormVersion { get; set; } = null!;

    public string LayoutType { get; set; } = null!;     // Section|Row|Column|Tab|Accordion|Panel|Group
    public int? ParentLayoutId { get; set; }
    public FormLayout? ParentLayout { get; set; }
    public string? Name { get; set; }
    public int DisplayOrder { get; set; }
    public string? PropertiesJson { get; set; }         // e.g. {"columnSpan":6,"collapsible":true}
}
