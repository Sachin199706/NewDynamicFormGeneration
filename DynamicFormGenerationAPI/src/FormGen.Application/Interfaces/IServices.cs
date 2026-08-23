using System.Collections.Generic;
using System.Threading.Tasks;
using FormGen.Application.Common;
using FormGen.Application.DTOs.Forms;
using FormGen.Application.DTOs.Rules;
using FormGen.Application.DTOs.Submissions;

namespace FormGen.Application.Interfaces
{
    public interface IFormService
    {
        Task<PagedResult<FormListItemDto>> GetFormsAsync(int page, int pageSize, string? search);
        Task<Result<FormVersionDto>> SaveVersionAsync(SaveFormVersionDto dto);
        Task<Result<FormVersionDto>> GetLatestVersionAsync(int formId);
        Task<Result<FormRenderDto>> GetRenderPayloadAsync(int formId, int formVersionId);
        Task<Result<bool>> PublishAsync(int formId, int formVersionId);
    }

    /// <summary>
    /// The Rule Engine — the shared contract also mirrored client-side in Angular's
    /// RuleEngineService (core/services/rule-engine.service.ts). Both interpret the same
    /// RuleType + RuleDetailsJson pairs so client UX and server enforcement never drift.
    /// </summary>
    public interface IRuleEngineService
    {
        Task<List<FormRuleDto>> GetRulesForVersionAsync(int formVersionId);
        Task<FormRuleDto> AddRuleAsync(int formVersionId, CreateFormRuleDto dto);
        Task UpdateRuleAsync(int ruleId, CreateFormRuleDto dto);
        Task DeleteRuleAsync(int ruleId);

        /// <summary>Evaluates all active rules for a version against submitted values. Pure function, no I/O.</summary>
        RuleEvaluationResultDto Evaluate(List<FormRuleDto> rules,
                                          IReadOnlyDictionary<string, object?> submittedValues,
                                          IReadOnlyDictionary<int, string> controlKeysById);
    }

    public interface ISubmissionService
    {
        Task<Result<int>> SubmitAsync(SubmitFormDto dto);
        Task<PagedResult<SubmissionListItemDto>> GetSubmissionsAsync(int formId, int page, int pageSize);
        Task<Result<SubmissionDetailDto>> GetDetailAsync(int submissionId);
    }
}
