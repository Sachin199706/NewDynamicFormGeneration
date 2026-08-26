
namespace NewDynamicFormGenAPI.Models.DTOs.Forms;

public class FormListItemDto
{
    public int FormId { get; set; }
    public string FormCode { get; set; } = null!;
    public string FormName { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    public int? CurrentVersionId { get; set; }
    public int? CurrentVersionNo { get; set; }
    public DateTime ModifiedDate { get; set; }
}

public class CreateFormDto
{
    public string FormName { get; set; } = null!;
    public string? Description { get; set; }
}

public class FormControlDto
{
    public int ControlId { get; set; }
    public string ControlKey { get; set; } = null!;
    public string ControlTypeCode { get; set; } = null!;
    public string? Label { get; set; }
    public string? Placeholder { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsVisible { get; set; }
    public int DisplayOrder { get; set; }
    public int? ParentControlId { get; set; }
    public string? PropertiesJson { get; set; }
    public int? DataSourceId { get; set; }
}

public class FormLayoutDto
{
    public int LayoutId { get; set; }
    public string LayoutType { get; set; } = null!;
    public int? ParentLayoutId { get; set; }
    public string? Name { get; set; }
    public int DisplayOrder { get; set; }
    public string? PropertiesJson { get; set; }
}

public class SaveFormVersionDto
{
    public int? FormId { get; set; }         // null => create new Form + v1
    public string? FormName { get; set; }
    public string FormDefinitionJson { get; set; } = "{}";
    public string? LayoutDefinitionJson { get; set; }
    public List<FormControlDto> Controls { get; set; } = new();
    public List<FormLayoutDto> Layouts { get; set; } = new();
}

public class FormVersionDto
{
    public int FormVersionId { get; set; }
    public int FormId { get; set; }
    public string FormName { get; set; }
    public int VersionNo { get; set; }
    public string Status { get; set; } = null!;
    public string FormDefinitionJson { get; set; } = null!;
    public string? LayoutDefinitionJson { get; set; }
    public List<FormControlDto> Controls { get; set; } = new();
    public List<FormLayoutDto> Layouts { get; set; } = new();
    public DateTime CreatedDate { get; set; }
}

/// <summary>Public-safe payload for the "fill in the form" screen: controls + layout + active rules.</summary>
public class FormRenderDto
{
    public int FormId { get; set; }
    public int FormVersionId { get; set; }
    public string FormName { get; set; } = null!;
    public string? LayoutDefinitionJson { get; set; }
    public List<FormControlDto> Controls { get; set; } = new();
    public List<FormLayoutDto> Layouts { get; set; } = new();
    public List<Rules.FormRuleDto> Rules { get; set; } = new();
}

public class FormVersionListItemDto
{
    public int FormId { get; set; }
    public int FormVersionId { get; set; }
    public string FormName { get; set; } = null!;
    public int VersionNo { get; set; }
    public string Status { get; set; } = null!;
    public DateTime ModifiedDate { get; set; }
}
public class FormPublishHistoryItemDto
{
    public int FormId { get; set; }
    public int FormVersionId { get; set; }
    public string FormName { get; set; } = null!;
    public int VersionNo { get; set; }
    public DateTime PublishedOn { get; set; }
}