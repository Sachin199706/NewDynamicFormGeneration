using Microsoft.AspNetCore.Mvc;
using NewDynamicFormGenAPI.Models.Common;
using NewDynamicFormGenAPI.Models.DTOs.Forms;
using NewDynamicFormGenAPI.Models.DTOs.Rules;
using NewDynamicFormGenAPI.Models.DTOs.Submissions;

namespace NewDynamicFormGenAPI.Models.Interfaces;

public interface IFormService
{
    Task<PagedResult<FormListItemDto>> GetFormsAsync(int aNumPage, int aNumPageSize, string? aStrSearch, DateTime? fromDate, DateTime? toDate);
    Task<Result<FormListItemDto>> CreateFormAsync(CreateFormDto aObjDto);
    Task<Result<FormVersionDto>> SaveVersionAsync(SaveFormVersionDto aObjDto);
    Task<Result<FormVersionDto>> GetLatestVersionAsync(int aNumFormId);
    Task<Result<FormRenderDto>> GetRenderPayloadAsync(int aNumFormId, int aNumFormVersionId);
    Task<Result<bool>> PublishAsync(int aNumFormId, int aNumFormVersionId);
    Task<List<FormVersionListItemDto>> GetAllVersionsAsync();
    Task<DashboardDTO> GetDashboardCountAsync();
    Task<List<FormPublishHistoryItemDto>> GetPublishHistoryAsync();
    Task<Result<FormVersionDto>> GetVersionByIdAsync(int aNumFormVersionId);
}

/// <summary>
/// The Rule Engine — the shared contract also mirrored client-side in Angular's
/// RuleEngineService (core/services/rule-engine.service.ts). Both interpret the same
/// RuleType + RuleDetailsJson pairs so client UX and server enforcement never drift.
/// </summary>
public interface IRuleEngineService
{
    Task<List<FormRuleDto>> GetRulesForVersionAsync(int aNumFormVersionId);
    Task<FormRuleDto> AddRuleAsync(int aNumFormVersionId, CreateFormRuleDto aObjDto);
    Task DeleteRuleAsync(int aNumFormVersionId, string aStrControlKey, string aStrRuleType);
    RuleEvaluationResultDto Evaluate(List<FormRuleDto> aArrRules, IReadOnlyDictionary<string, object?> aObjSubmittedValues);
}

public interface ISubmissionService
{
    Task<Result<int>> SubmitAsync(SubmitFormDto aObjDto, IFormFileCollection aObjFiles);
    Task<Result<SubmissionDetailDto>> GetDetailAsync(int aNumSubmissionId);
    Task<Result<bool>> MarkAsReadAsync(int aNumSubmissionId);
    Task<PagedResult<SubmissionOverviewItemDto>> GetAllSubmissionsAsync(SubmissionFilterDto aObjFilter);
    Task<SubmissionStatsDto> GetStatsAsync();
    Task<SubmissionStatsDto> GetStatsAsync(int anumID);
}
