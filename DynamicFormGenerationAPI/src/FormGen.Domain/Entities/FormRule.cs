using System;

namespace FormGen.Domain.Entities
{
    /// <summary>
    /// One validation rule attached to one control on one form version.
    /// RuleType drives how RuleDetailsJson is interpreted — see Enums.RuleType and
    /// FormGen.Application.Services.RuleEngineService for the shared evaluation contract.
    /// </summary>
    public class FormRule
    {
        public int RuleId { get; set; }
        public int FormVersionId { get; set; }
        public FormVersion FormVersion { get; set; } = null!;

        public int ControlId { get; set; }
        public FormControl Control { get; set; } = null!;

        public string RuleType { get; set; } = null!;
        public string? RuleDetailsJson { get; set; }
        public string ErrorMessage { get; set; } = null!;
        public string Severity { get; set; } = "Error";     // Error | Warning
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }
    }
}
