using FluentValidation;
using NewDynamicFormGenAPI.Models.DTOs.Rules;
using NewDynamicFormGenAPI.Models.Enums;

namespace NewDynamicFormGenAPI.Models.Validators;

public class CreateFormRuleDtoValidator : AbstractValidator<CreateFormRuleDto>
{
    private static readonly string[] ValidTypes =
    {
        RuleType.Required, RuleType.MinLength, RuleType.MaxLength, RuleType.Regex,
        RuleType.Range, RuleType.Email, RuleType.Date, RuleType.CrossField, RuleType.Custom, RuleType.Visibility
    };

    public CreateFormRuleDtoValidator()
    {
        RuleFor(x => x.ControlId).GreaterThan(0);
        RuleFor(x => x.RuleType).NotEmpty().Must(t => System.Array.Exists(ValidTypes, v => v == t))
            .WithMessage("RuleType must be one of: " + string.Join(", ", ValidTypes));
        RuleFor(x => x.ErrorMessage).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Severity).Must(s => s == RuleSeverity.Error || s == RuleSeverity.Warning);
    }
}
