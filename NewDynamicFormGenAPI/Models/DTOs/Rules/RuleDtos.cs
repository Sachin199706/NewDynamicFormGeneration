namespace NewDynamicFormGenAPI.Models.DTOs.Rules;

public class FormRuleDto
{
    public string ControlKey { get; set; } = null!;
    public string RuleType { get; set; } = null!;
    public string? RuleDetailsJson { get; set; }
    public string ErrorMessage { get; set; } = null!;
    public string Severity { get; set; } = "Error";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateFormRuleDto
{
    public string ControlKey { get; set; } = null!;
    public string RuleType { get; set; } = null!;
    public string? RuleDetailsJson { get; set; }
    public string ErrorMessage { get; set; } = null!;
    public string Severity { get; set; } = "Error";
    public int DisplayOrder { get; set; }
}

public class RuleFailureDto
{
    public string ControlKey { get; set; } = null!;
    public string RuleType { get; set; } = null!;
    public string ErrorMessage { get; set; } = null!;
    public string Severity { get; set; } = "Error";
}

public class RuleEvaluationResultDto
{
    public bool IsValid { get; set; }
    public List<RuleFailureDto> Failures { get; set; } = new();
}

/// <summary>
/// The computed state of one control after all conditional rules have run.
/// Every control gets one of these; controls with no conditional rules keep
/// their defaults.
/// </summary>
public class ControlEffectsDto
{
    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public bool Required { get; set; }
}