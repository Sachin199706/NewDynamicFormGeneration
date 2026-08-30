using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using NewDynamicFormGenAPI.Models.Interfaces;
using NewDynamicFormGenAPI.Models.DTOs.Rules;
using NewDynamicFormGenAPI.Models.Entities;
using NewDynamicFormGenAPI.Models.Enums;

namespace NewDynamicFormGenAPI.Models.Services;

/// <summary>
/// Rules now live embedded inside each control's JSON, inside FormVersions.FormDefinitionJson —
/// there is no FormRules table anymore. Reading/writing a rule means parsing the whole
/// FormDefinitionJson blob, finding the right control by ControlKey, and mutating its
/// "rules" array in place.
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
        var lobjVersion = await _uow.Repository<FormVersion>().GetByIdAsync(aNumFormVersionId);
        if (lobjVersion == null) return new List<FormRuleDto>();

        return FlattenRules(lobjVersion.FormDefinitionJson);
    }

    public async Task<FormRuleDto> AddRuleAsync(int aNumFormVersionId, CreateFormRuleDto aObjDto)
    {
        var lobjRepo = _uow.Repository<FormVersion>();
        var lobjVersion = await lobjRepo.GetByIdAsync(aNumFormVersionId)
            ?? throw new KeyNotFoundException($"FormVersion {aNumFormVersionId} not found");

        var lobjRoot = JsonNode.Parse(lobjVersion.FormDefinitionJson)!.AsObject();
        var lobjControlsArr = lobjRoot["controls"]?.AsArray() ?? new JsonArray();

        JsonObject? lobjTargetControl = null;
        foreach (var lobjNode in lobjControlsArr)
        {
            if (lobjNode is JsonObject lobjObj &&
                string.Equals(lobjObj["controlKey"]?.GetValue<string>(), aObjDto.ControlKey, StringComparison.OrdinalIgnoreCase))
            {
                lobjTargetControl = lobjObj;
                break;
            }
        }

        if (lobjTargetControl == null)
            throw new KeyNotFoundException($"Control '{aObjDto.ControlKey}' not found on this version.");

        var lobjRulesArr = lobjTargetControl["rules"]?.AsArray();
        if (lobjRulesArr == null)
        {
            lobjRulesArr = new JsonArray();
            lobjTargetControl["rules"] = lobjRulesArr;
        }

        var lstrSeverity = string.IsNullOrWhiteSpace(aObjDto.Severity) ? RuleSeverity.Error : aObjDto.Severity;

        lobjRulesArr.Add(new JsonObject
        {
            ["ruleType"] = aObjDto.RuleType,
            ["ruleDetailsJson"] = aObjDto.RuleDetailsJson,
            ["errorMessage"] = aObjDto.ErrorMessage,
            ["severity"] = lstrSeverity,
            ["displayOrder"] = aObjDto.DisplayOrder,
            ["isActive"] = true
        });

        lobjVersion.FormDefinitionJson = lobjRoot.ToJsonString();
        lobjRepo.Update(lobjVersion);
        await _uow.SaveChangesAsync();

        return new FormRuleDto
        {
            ControlKey = aObjDto.ControlKey,
            RuleType = aObjDto.RuleType,
            RuleDetailsJson = aObjDto.RuleDetailsJson,
            ErrorMessage = aObjDto.ErrorMessage,
            Severity = lstrSeverity,
            DisplayOrder = aObjDto.DisplayOrder,
            IsActive = true
        };
    }

    public async Task DeleteRuleAsync(int aNumFormVersionId, string aStrControlKey, string aStrRuleType)
    {
        var lobjRepo = _uow.Repository<FormVersion>();
        var lobjVersion = await lobjRepo.GetByIdAsync(aNumFormVersionId);
        if (lobjVersion == null) return;

        var lobjRoot = JsonNode.Parse(lobjVersion.FormDefinitionJson)!.AsObject();
        var lobjControlsArr = lobjRoot["controls"]?.AsArray();
        if (lobjControlsArr == null) return;

        foreach (var lobjNode in lobjControlsArr)
        {
            if (lobjNode is not JsonObject lobjObj) continue;
            if (!string.Equals(lobjObj["controlKey"]?.GetValue<string>(), aStrControlKey, StringComparison.OrdinalIgnoreCase)) continue;

            var lobjRulesArr = lobjObj["rules"]?.AsArray();
            if (lobjRulesArr == null) break;

            for (int i = lobjRulesArr.Count - 1; i >= 0; i--)
            {
                if (lobjRulesArr[i] is JsonObject lobjRuleObj &&
                    string.Equals(lobjRuleObj["ruleType"]?.GetValue<string>(), aStrRuleType, StringComparison.OrdinalIgnoreCase))
                {
                    lobjRulesArr.RemoveAt(i);
                }
            }
            break;
        }

        lobjVersion.FormDefinitionJson = lobjRoot.ToJsonString();
        lobjRepo.Update(lobjVersion);
        await _uow.SaveChangesAsync();
    }

    /// <summary>Flattens every control's embedded rules array into one flat list, tagging each with its ControlKey.</summary>
    private static List<FormRuleDto> FlattenRules(string aStrFormDefinitionJson)
    {
        var larrResult = new List<FormRuleDto>();
        if (string.IsNullOrWhiteSpace(aStrFormDefinitionJson)) return larrResult;

        try
        {
            using var lobjDoc = JsonDocument.Parse(aStrFormDefinitionJson);
            if (!lobjDoc.RootElement.TryGetProperty("controls", out var lobjControlsEl)) return larrResult;

            foreach (var lobjControlEl in lobjControlsEl.EnumerateArray())
            {
                if (!lobjControlEl.TryGetProperty("controlKey", out var lobjKeyEl)) continue;
                var lstrControlKey = lobjKeyEl.GetString() ?? "";

                if (!lobjControlEl.TryGetProperty("rules", out var lobjRulesEl)) continue;

                foreach (var lobjRuleEl in lobjRulesEl.EnumerateArray())
                {
                    larrResult.Add(new FormRuleDto
                    {
                        ControlKey = lstrControlKey,
                        RuleType = lobjRuleEl.TryGetProperty("ruleType", out var t) ? t.GetString() ?? "" : "",
                        RuleDetailsJson = lobjRuleEl.TryGetProperty("ruleDetailsJson", out var d) ? d.GetString() : null,
                        ErrorMessage = lobjRuleEl.TryGetProperty("errorMessage", out var e) ? e.GetString() ?? "" : "",
                        Severity = lobjRuleEl.TryGetProperty("severity", out var s) ? s.GetString() ?? "Error" : "Error",
                        DisplayOrder = lobjRuleEl.TryGetProperty("displayOrder", out var o) && o.TryGetInt32(out var ov) ? ov : 0,
                        IsActive = !lobjRuleEl.TryGetProperty("isActive", out var a) || a.GetBoolean()
                    });
                }
            }
        }
        catch { /* malformed JSON — return whatever was parsed so far */ }

        return larrResult.OrderBy(r => r.DisplayOrder).ToList();
    }

    /// <summary>
    /// Pure evaluation — same semantics as the Angular RuleEngineService's ValidatorFns.
    /// Keep both implementations' switch cases in lockstep when adding a new RuleType.
    /// </summary>
    public RuleEvaluationResultDto Evaluate(List<FormRuleDto> aArrRules, IReadOnlyDictionary<string, object?> aObjSubmittedValues)
    {
        var lobjResult = new RuleEvaluationResultDto { IsValid = true };
        var lobjVisibility = ComputeVisibility(aArrRules, aObjSubmittedValues);

        foreach (var rule in aArrRules.Where(r => r.IsActive).OrderBy(r => r.DisplayOrder))
        {
            if (rule.RuleType == RuleType.Visibility)
                continue;

            if (lobjVisibility.TryGetValue(rule.ControlKey, out var lboolVisible) && !lboolVisible)
                continue;

            aObjSubmittedValues.TryGetValue(rule.ControlKey, out var lobjRawValue);
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
                    ControlKey = rule.ControlKey,
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

    private static Dictionary<string, bool> ComputeVisibility(List<FormRuleDto> aArrRules, IReadOnlyDictionary<string, object?> aObjSubmittedValues)
    {
        var lobjVisibility = new Dictionary<string, bool>();

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

            lobjVisibility[rule.ControlKey] = lstrAction == "Hide" ? !lboolConditionMet : lboolConditionMet;
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

        var lstrOperator = GetString(aObjRule.RuleDetailsJson, "operator");
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

    private static bool EvaluateCrossField(FormRuleDto aObjRule, string aStrValue, IReadOnlyDictionary<string, object?> aObjSubmittedValues)
    {
        var lstrCompareKey = GetString(aObjRule.RuleDetailsJson, "compareControlKey");
        var lstrOp = GetString(aObjRule.RuleDetailsJson, "operator") ?? "==";
        if (string.IsNullOrEmpty(lstrCompareKey)) return true;

        aObjSubmittedValues.TryGetValue(lstrCompareKey, out var lobjCompareRaw);
        var lstrCompareValue = lobjCompareRaw?.ToString() ?? string.Empty;

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
}