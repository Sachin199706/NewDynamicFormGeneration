namespace NewDynamicFormGenAPI.Models.Entities;

public class Form
{
    public int FormId { get; set; }
    public string FormCode { get; set; } = null!;
    public string FormName { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = "Draft";
    public int? CurrentVersionId { get; set; }
    public FormVersion? CurrentVersion { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }

    public ICollection<FormVersion> Versions { get; set; } = new List<FormVersion>();
    public ICollection<FormSubmission> Submissions { get; set; } = new List<FormSubmission>();
}

public class FormVersion
{
    public int FormVersionId { get; set; }
    public int FormId { get; set; }
    public Form Form { get; set; } = null!;
    public int VersionNo { get; set; }
    public string? VersionName { get; set; }
    public string Status { get; set; } = "Draft";

    /// <summary>Full snapshot: every control on the canvas, each with its own embedded
    /// "rules" array — this is the single source of truth now, no FormControls/FormRules tables.</summary>
    public string FormDefinitionJson { get; set; } = "{}";

    /// <summary>Just the column-count setting, e.g. {"columnLayout": 4}.</summary>
    public string? LayoutDefinitionJson { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedDate { get; set; }
}