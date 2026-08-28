using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json;
using NewDynamicFormGenAPI.Models.Interfaces;
using NewDynamicFormGenAPI.Models.DTOs.Rules;
using NewDynamicFormGenAPI.Models.Entities;
using NewDynamicFormGenAPI.Models.Enums;


namespace NewDynamicFormGenAPI.Models.Services;

/// <summary>
/// Server-side implementation of the Rule Engine described in ARCHITECTURE.md §5.
/// This is the authoritative validator — it is invoked again by SubmissionService even
/// though the client already validated, because client-side checks can be bypassed.
/// </summary>
public class RuleEngineService : IRuleEngineService
{
    private readonly IUnitOfWork _uow;

    public RuleEngineService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<List<FormRuleDto>> GetRulesForVersionAsync(int aNumFormVersionId)
    {
        var larrRules = _uow.Repository<FormRule>().Query()
            .Where(r => r.FormVersionId == aNumFormVersionId && r.IsActive)
            .OrderBy(r => r.DisplayOrder)
            .ToList();

        var larrControlIds = larrRules.Select(r => r.ControlId).Distinct().ToList();
        var lobjControlKeys = _uow.Repository<FormControl>().Query()
            .Where(c => larrControlIds.Contains(c.ControlId))
            .ToDictionary(c => c.ControlId, c => c.ControlKey);

        return larrRules.Select(r => Map(r, lobjControlKeys.GetValueOrDefault(r.ControlId, ""))).ToList();
    }

    public async Task<FormRuleDto> AddRuleAsync(int aNumFormVersionId, CreateFormRuleDto aObjDto)
    {
        var lobjEntity = new FormRule
        {
            FormVersionId = aNumFormVersionId,
            ControlId = aObjDto.ControlId,
            RuleType = aObjDto.RuleType,
            RuleDetailsJson = aObjDto.RuleDetailsJson,
            ErrorMessage = aObjDto.ErrorMessage,
            Severity = string.IsNullOrWhiteSpace(aObjDto.Severity) ? RuleSeverity.Error : aObjDto.Severity,
            DisplayOrder = aObjDto.DisplayOrder,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };
        await _uow.Repository<FormRule>().AddAsync(lobjEntity);
        await _uow.SaveChangesAsync();

        var lobjControl = await _uow.Repository<FormControl>().GetByIdAsync(aObjDto.ControlId);
        return Map(lobjEntity, lobjControl?.ControlKey ?? "");
    }

    public async Task UpdateRuleAsync(int aNumRuleId, CreateFormRuleDto aObjDto)
    {
        var lobjRepo = _uow.Repository<FormRule>();
        var lobjEntity = await lobjRepo.GetByIdAsync(aNumRuleId)
            ?? throw new KeyNotFoundException($"Rule {aNumRuleId} not found");

        lobjEntity.ControlId = aObjDto.ControlId;
        lobjEntity.RuleType = aObjDto.RuleType;
        lobjEntity.RuleDetailsJson = aObjDto.RuleDetailsJson;
        lobjEntity.ErrorMessage = aObjDto.ErrorMessage;
        lobjEntity.Severity = aObjDto.Severity;
        lobjEntity.DisplayOrder = aObjDto.DisplayOrder;
        lobjEntity.ModifiedDate = DateTime.UtcNow;

        lobjRepo.Update(lobjEntity);
        await _uow.SaveChangesAsync();
    }

    public async Task DeleteRuleAsync(int aNumRuleId)
    {
        var lobjRepo = _uow.Repository<FormRule>();
        var lobjEntity = await lobjRepo.GetByIdAsync(aNumRuleId);
        if (lobjEntity == null) return;
        lobjRepo.Remove(lobjEntity);
        await _uow.SaveChangesAsync();
    }

    /// <summary>
    /// Pure evaluation — same semantics as the Angular RuleEngineService's ValidatorFns.
    /// Keep both implementations' switch cases in lockstep when adding a new RuleType.
    /// </summary>
    public RuleEvaluationResultDto Evaluate(List<FormRuleDto> aArrRules, IReadOnlyDictionary<string, object?> aObjSubmittedValues,
        IReadOnlyDictionary<int, string> aObjControlKeysById)
    {
        var lobjResult = new RuleEvaluationResultDto { IsValid = true };
        var lobjVisibility = ComputeVisibility(aArrRules, aObjSubmittedValues);

        foreach (var rule in aArrRules.Where(r => r.IsActive).OrderBy(r => r.DisplayOrder))
        {
            // Visibility rules are UI-only (show/hide) — they never gate submission.
            // The client is the source of truth for what was actually visible when submitted.
            if (rule.RuleType == RuleType.Visibility)
                continue;

            // A field currently hidden by a Visibility rule is exempt from every other
            // rule too (Required, MinLength, etc.) — the user had no way to fill it in.
            // A field with NO Visibility rule is never in this dictionary, so it's
            // completely unaffected and behaves exactly as before.
            if (lobjVisibility.TryGetValue(rule.ControlId, out var lboolVisible) && !lboolVisible)
                continue;

            var lstrKey = rule.ControlKey;
            aObjSubmittedValues.TryGetValue(lstrKey, out var lobjRawValue);
            var lstrStringValue = lobjRawValue?.ToString() ?? string.Empty;

            bool lboolPassed = rule.RuleType switch
            {
                RuleType.Required => !string.IsNullOrWhiteSpace(lstrStringValue),
                RuleType.MinLength => EvaluateMinLength(rule, lstrStringValue),
                RuleType.MaxLength => EvaluateMaxLength(rule, lstrStringValue),
                RuleType.Regex => EvaluateRegex(rule, lstrStringValue),
                RuleType.Range => EvaluateRange(rule, lstrStringValue),
                RuleType.Email => EvaluateEmail(lstrStringValue),
                RuleType.Date => EvaluateDate(rule, lstrStringValue),
                RuleType.CrossField => EvaluateCrossField(rule, lstrStringValue, aObjSubmittedValues),
                RuleType.Custom => true,
                _ => true
            };

            if (rule.RuleType != RuleType.Required && string.IsNullOrWhiteSpace(lstrStringValue))
                continue;

            if (!lboolPassed)
            {
                lobjResult.Failures.Add(new RuleFailureDto
                {
                    ControlId = rule.ControlId,
                    ControlKey = lstrKey,
                    RuleType = rule.RuleType,
                    ErrorMessage = rule.ErrorMessage,
                    Severity = rule.Severity
                });

                if (rule.Severity == RuleSeverity.Error)
                    lobjResult.IsValid = false;
            }
        }

        return lobjResult;
    }

    // ---- individual rule evaluators ----

    /// <summary>
    /// Server-side mirror of the client's computeVisibility() — evaluates every active
    /// Visibility rule against the submitted values, returns ControlId -> should-be-visible.
    /// A control with no Visibility rule simply isn't in this dictionary at all.
    /// </summary>
    private static Dictionary<int, bool> ComputeVisibility(List<FormRuleDto> aArrRules,IReadOnlyDictionary<string, object?> aObjSubmittedValues)
    {
        var lobjVisibility = new Dictionary<int, bool>();

        foreach (var rule in aArrRules.Where(r => r.IsActive && r.RuleType == RuleType.Visibility))
        {
            var lstrTriggerKey = GetString(rule.RuleDetailsJson, "triggerControlKey");
            var lstrOperator = GetString(rule.RuleDetailsJson, "operator") ?? "==";
            var lstrTriggerValue = GetString(rule.RuleDetailsJson, "triggerValue") ?? "";
            var lstrAction = GetString(rule.RuleDetailsJson, "action") ?? "Show";

            if (string.IsNullOrEmpty(lstrTriggerKey)) continue;

            aObjSubmittedValues.TryGetValue(lstrTriggerKey, out var lobjRaw);
            var lstrActual = lobjRaw?.ToString() ?? string.Empty;

            bool lboolConditionMet;
            if (double.TryParse(lstrActual, out var lnumActual) && double.TryParse(lstrTriggerValue, out var lnumTrigger))
                lboolConditionMet = lstrOperator == "!=" ? lnumActual != lnumTrigger : lnumActual == lnumTrigger;
            else
                lboolConditionMet = lstrOperator == "!=" ? lstrActual != lstrTriggerValue : lstrActual == lstrTriggerValue;

            var lboolShouldShow = lstrAction == "Hide" ? !lboolConditionMet : lboolConditionMet;
            lobjVisibility[rule.ControlId] = lboolShouldShow;
        }

        return lobjVisibility;
    }

    private static bool EvaluateMinLength(FormRuleDto aObjRule, string aStrValue)
    {
        var lnumMin = GetInt(aObjRule.RuleDetailsJson, "min") ?? 0;
        return aStrValue.Length >= lnumMin;
    }

    private static bool EvaluateMaxLength(FormRuleDto aObjRule, string aStrValue)
    {
        var lnumMax = GetInt(aObjRule.RuleDetailsJson, "max") ?? int.MaxValue;
        return aStrValue.Length <= lnumMax;
    }

    private static bool EvaluateRegex(FormRuleDto aObjRule, string aStrValue)
    {
        var lstrPattern = GetString(aObjRule.RuleDetailsJson, "pattern");
        if (string.IsNullOrEmpty(lstrPattern)) return true;
        return Regex.IsMatch(aStrValue, lstrPattern);
    }

    private static bool EvaluateRange(FormRuleDto aObjRule, string aStrValue)
    {
        if (!double.TryParse(aStrValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var lnumNum))
            return false;
        var lnumMin = GetDouble(aObjRule.RuleDetailsJson, "min") ?? double.MinValue;
        var lnumMax = GetDouble(aObjRule.RuleDetailsJson, "max") ?? double.MaxValue;
        return lnumNum >= lnumMin && lnumNum <= lnumMax;
    }

    private static bool EvaluateEmail(string aStrValue)
    {
        const string lstrPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(aStrValue, lstrPattern);
    }

    private static bool EvaluateDate(FormRuleDto aObjRule, string aStrValue)
    {
        if (!DateTime.TryParse(aStrValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lobjDate))
            return false;

        var lstrOperator = GetString(aObjRule.RuleDetailsJson, "operator"); // e.g. "<=Today", ">=Today"
        if (string.IsNullOrEmpty(lstrOperator)) return true;

        return lstrOperator switch
        {
            "<=Today" => lobjDate.Date <= DateTime.UtcNow.Date,
            ">=Today" => lobjDate.Date >= DateTime.UtcNow.Date,
            "<Today" => lobjDate.Date < DateTime.UtcNow.Date,
            ">Today" => lobjDate.Date > DateTime.UtcNow.Date,
            _ => true
        };
    }

    private static bool EvaluateCrossField(FormRuleDto aObjRule, string aStrValue,
        IReadOnlyDictionary<string, object?> aObjSubmittedValues)
    {
        var lstrCompareKey = GetString(aObjRule.RuleDetailsJson, "compareControlKey");
        var lstrOp = GetString(aObjRule.RuleDetailsJson, "operator") ?? "==";
        if (string.IsNullOrEmpty(lstrCompareKey)) return true;

        aObjSubmittedValues.TryGetValue(lstrCompareKey, out var lobjCompareRaw);
        var lstrCompareValue = lobjCompareRaw?.ToString() ?? string.Empty;

        // try numeric compare first, fall back to string/date compare
        if (double.TryParse(aStrValue, out var lnumA) && double.TryParse(lstrCompareValue, out var lnumB))
        {
            return lstrOp switch
            {
                "==" => lnumA == lnumB,
                "!=" => lnumA != lnumB,
                "<" => lnumA < lnumB,
                "<=" => lnumA <= lnumB,
                ">" => lnumA > lnumB,
                ">=" => lnumA >= lnumB,
                _ => true
            };
        }

        if (DateTime.TryParse(aStrValue, out var lobjDa) && DateTime.TryParse(lstrCompareValue, out var lobjDb))
        {
            return lstrOp switch
            {
                "==" => lobjDa == lobjDb,
                "!=" => lobjDa != lobjDb,
                "<" => lobjDa < lobjDb,
                "<=" => lobjDa <= lobjDb,
                ">" => lobjDa > lobjDb,
                ">=" => lobjDa >= lobjDb,
                _ => true
            };
        }

        return lstrOp == "==" ? aStrValue == lstrCompareValue : aStrValue != lstrCompareValue;
    }

    // ---- RuleDetailsJson helpers ----

    private static string? GetString(string? aStrJson, string aStrProp)
    {
        if (string.IsNullOrWhiteSpace(aStrJson)) return null;
        using var lobjDoc = JsonDocument.Parse(aStrJson);
        return lobjDoc.RootElement.TryGetProperty(aStrProp, out var lobjEl) ? lobjEl.GetString() : null;
    }

    private static int? GetInt(string? aStrJson, string aStrProp)
    {
        if (string.IsNullOrWhiteSpace(aStrJson)) return null;
        using var lobjDoc = JsonDocument.Parse(aStrJson);
        return lobjDoc.RootElement.TryGetProperty(aStrProp, out var lobjEl) && lobjEl.TryGetInt32(out var lnumV) ? lnumV : null;
    }

    private static double? GetDouble(string? aStrJson, string aStrProp)
    {
        if (string.IsNullOrWhiteSpace(aStrJson)) return null;
        using var lobjDoc = JsonDocument.Parse(aStrJson);
        return lobjDoc.RootElement.TryGetProperty(aStrProp, out var lobjEl) && lobjEl.TryGetDouble(out var lnumV) ? lnumV : null;
    }

    private static FormRuleDto Map(FormRule aObjR, string aStrControlKey) => new()
    {
        RuleId = aObjR.RuleId,
        FormVersionId = aObjR.FormVersionId,
        ControlId = aObjR.ControlId,
        ControlKey = aStrControlKey,
        RuleType = aObjR.RuleType,
        RuleDetailsJson = aObjR.RuleDetailsJson,
        ErrorMessage = aObjR.ErrorMessage,
        Severity = aObjR.Severity,
        DisplayOrder = aObjR.DisplayOrder,
        IsActive = aObjR.IsActive
    };
}