namespace NewDynamicFormGenAPI.Models.DTOs.Rules;

public class FormRuleDto
{
    public int RuleId { get; set; }
    public int FormVersionId { get; set; }
    public int ControlId { get; set; }
    public string ControlKey { get; set; } = null!;   // convenience for the client engine
    public string RuleType { get; set; } = null!;
    public string? RuleDetailsJson { get; set; }
    public string ErrorMessage { get; set; } = null!;
    public string Severity { get; set; } = "Error";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateFormRuleDto
{
    public int ControlId { get; set; }
    public string RuleType { get; set; } = null!;
    public string? RuleDetailsJson { get; set; }
    public string ErrorMessage { get; set; } = null!;
    public string Severity { get; set; } = "Error";
    public int DisplayOrder { get; set; }
}

/// <summary>One field failure produced by evaluating rules against submitted values.</summary>
public class RuleFailureDto
{
    public int ControlId { get; set; }
    public string ControlKey { get; set; } = null!;
    public string RuleType { get; set; } = null!;
    public string ErrorMessage { get; set; } = null!;
    public string Severity { get; set; } = "Error";
}

public class RuleEvaluationResultDto
{
    public bool IsValid { get; set; }
    public System.Collections.Generic.List<RuleFailureDto> Failures { get; set; } = new();
}
