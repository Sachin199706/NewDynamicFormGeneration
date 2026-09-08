using FluentValidation;
using NewDynamicFormGenAPI.Models.DTOs.Rules;
using NewDynamicFormGenAPI.Models.Enums;

namespace NewDynamicFormGenAPI.Models.Validators;

public class CreateFormRuleDtoValidator : AbstractValidator<CreateFormRuleDto>
{
    private static readonly string[] ValidTypes =
    {
        RuleType.Required, RuleType.Length, RuleType.Pattern,
        RuleType.Range, RuleType.Format, RuleType.Date, RuleType.CompareFields, RuleType.Custom, RuleType.Visibility
    };

    public CreateFormRuleDtoValidator()
    {
        RuleFor(x => x.ControlKey).NotEmpty();
        RuleFor(x => x.RuleType).NotEmpty().Must(t => System.Array.Exists(ValidTypes, v => v == t))
            .WithMessage("RuleType must be one of: " + string.Join(", ", ValidTypes));
        RuleFor(x => x.ErrorMessage).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Severity).Must(s => s == RuleSeverity.Error || s == RuleSeverity.Warning);
    }
}