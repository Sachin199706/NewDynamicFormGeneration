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

    private const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    private const string PhonePattern = @"^\+?[0-9\s\-()]{7,15}$";
    private const string UrlPattern = @"^https?://[^\s/$.?#].[^\s]*$";
    private const string NumberPattern = @"^-?\d+(\.\d+)?$";
    private const string AlphanumericPattern = @"^[A-Za-z0-9]+$";

    public RuleEngineService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    /// <summary>
    /// Maps a stored rule name onto its current equivalent. Rules written before issue #10
    /// still carry the old names inside FormDefinitionJson — normalising on read means no
    /// data migration, and old forms keep validating exactly as they did.
    /// MinLength/MaxLength both become Length; their details JSON already carries only
    /// the half they set, and EvaluateLength treats a missing bound as unbounded.
    /// </summary>
    private static string NormalizeRuleType(string aStrRuleType) => aStrRuleType switch
    {
        RuleType.Legacy.MinLength => RuleType.Length,
        RuleType.Legacy.MaxLength => RuleType.Length,
        RuleType.Legacy.Regex => RuleType.Pattern,
        RuleType.Legacy.Email => RuleType.Format,
        RuleType.Legacy.CrossField => RuleType.CompareFields,
        _ => aStrRuleType
    };

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

    public List<RuleFailureDto> EvaluateFileRules(List<FormRuleDto> aArrRules, IFormFileCollection aObjFiles)
    {
        var larrFailures = new List<RuleFailureDto>();

        foreach (var rule in aArrRules.Where(r => r.IsActive && NormalizeRuleType(r.RuleType) == RuleType.File))
        {
            // FormData appends each file under its controlKey, so that is what matches here.
            var lobjFile = aObjFiles.FirstOrDefault(f =>
                string.Equals(f.Name, rule.ControlKey, StringComparison.OrdinalIgnoreCase));

            // Nothing uploaded — that is Required's job to complain about, not this rule's.
            if (lobjFile == null) continue;

            var larrAllowed = GetStringArray(rule.RuleDetailsJson, "allowedExtensions");
            var lnumMaxSizeKb = GetInt(rule.RuleDetailsJson, "maxSizeKb");

            var lboolPassed = true;

            if (larrAllowed.Count > 0)
            {
                var lstrExtension = Path.GetExtension(lobjFile.FileName).TrimStart('.');
                lboolPassed = larrAllowed.Any(e =>
                    string.Equals(e.TrimStart('.'), lstrExtension, StringComparison.OrdinalIgnoreCase));
            }

            if (lboolPassed && lnumMaxSizeKb.HasValue && lobjFile.Length > (long)lnumMaxSizeKb.Value * 1024)
                lboolPassed = false;

            if (!lboolPassed)
            {
                larrFailures.Add(new RuleFailureDto
                {
                    ControlKey = rule.ControlKey,
                    RuleType = rule.RuleType,
                    ErrorMessage = rule.ErrorMessage,
                    Severity = rule.Severity
                });
            }
        }

        return larrFailures;
    }

    private static List<string> GetStringArray(string? aStrJson, string aStrProp)
    {
        var larrResult = new List<string>();
        if (string.IsNullOrWhiteSpace(aStrJson)) return larrResult;

        using var lobjDoc = JsonDocument.Parse(aStrJson);
        if (!lobjDoc.RootElement.TryGetProperty(aStrProp, out var lobjEl) || lobjEl.ValueKind != JsonValueKind.Array)
            return larrResult;

        foreach (var lobjItem in lobjEl.EnumerateArray())
        {
            var lstrValue = lobjItem.GetString();
            if (!string.IsNullOrWhiteSpace(lstrValue)) larrResult.Add(lstrValue);
        }

        return larrResult;
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

            var lstrRuleType = NormalizeRuleType(rule.RuleType);

            // Only Required cares about an empty value — everything else is skipped, so a blank
            // optional field never trips a pattern or range check. This must come BEFORE evaluation.
            if (lstrRuleType != RuleType.Required && string.IsNullOrWhiteSpace(lstrStringValue))
                continue;

            bool lboolPassed = lstrRuleType switch
            {
                RuleType.Required => !string.IsNullOrWhiteSpace(lstrStringValue),
                RuleType.Length => EvaluateLength(rule, lstrStringValue),
                RuleType.Range => EvaluateRange(rule, lstrStringValue),
                RuleType.Pattern => EvaluatePattern(rule, lstrStringValue),
                RuleType.Format => EvaluateFormat(rule, lstrStringValue),
                RuleType.Date => EvaluateDate(rule, lstrStringValue),
                RuleType.CompareFields => EvaluateCompareFields(rule, lstrStringValue, aObjSubmittedValues),
                RuleType.File => true,   // enforced in SubmissionService, before the file reaches disk
                RuleType.Custom => true,
                _ => true
            };

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

    /// <summary>One rule covering both bounds. A missing bound means unbounded, which is
    /// also how legacy MinLength/MaxLength rules land here — each set only its own half.</summary>
    private static bool EvaluateLength(FormRuleDto aObjRule, string aStrValue)
    {
        var lnumMin = GetInt(aObjRule.RuleDetailsJson, "min") ?? 0;
        var lnumMax = GetInt(aObjRule.RuleDetailsJson, "max") ?? int.MaxValue;
        return aStrValue.Length >= lnumMin && aStrValue.Length <= lnumMax;
    }

    private static bool EvaluateRange(FormRuleDto aObjRule, string aStrValue)
    {
        if (!double.TryParse(aStrValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var lnumNum))
            return false;
        var lnumMin = GetDouble(aObjRule.RuleDetailsJson, "min") ?? double.MinValue;
        var lnumMax = GetDouble(aObjRule.RuleDetailsJson, "max") ?? double.MaxValue;
        return lnumNum >= lnumMin && lnumNum <= lnumMax;
    }

    /// <summary>
    /// The pattern is free text typed into the Rule Builder, so it is never trusted:
    /// an invalid pattern throws and a pathological one can backtrack indefinitely.
    /// Both are contained here rather than surfacing as a 500 on submit.
    /// </summary>
    private static bool EvaluatePattern(FormRuleDto aObjRule, string aStrValue)
    {
        var lstrPattern = GetString(aObjRule.RuleDetailsJson, "pattern");
        if (string.IsNullOrEmpty(lstrPattern)) return true;

        try
        {
            return Regex.IsMatch(aStrValue, lstrPattern, RegexOptions.None, TimeSpan.FromMilliseconds(250));
        }
        catch (ArgumentException)
        {
            return true;   // malformed pattern — matches the client, which also passes
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }

    /// <summary>Generalises the old Email rule — legacy Email rules have no "format" key
    /// in their details, so Email is the default.</summary>
    private static bool EvaluateFormat(FormRuleDto aObjRule, string aStrValue)
    {
        var lstrFormat = GetString(aObjRule.RuleDetailsJson, "format") ?? FormatType.Email;

        var lstrPattern = lstrFormat switch
        {
            FormatType.Email => EmailPattern,
            FormatType.Phone => PhonePattern,
            FormatType.Url => UrlPattern,
            FormatType.Number => NumberPattern,
            FormatType.Alphanumeric => AlphanumericPattern,
            _ => null
        };

        if (lstrPattern is null) return true;

        return Regex.IsMatch(aStrValue, lstrPattern, RegexOptions.None, TimeSpan.FromMilliseconds(250));
    }

    /// <summary>
    /// "Today" is the user's today, not the server's. DateTime.UtcNow.Date put the two
    /// out of step for anyone east of UTC — in IST the server's date lags local until
    /// 05:30, so a >=Today rule passed in the browser and failed on submit.
    /// </summary>
    private static bool EvaluateDate(FormRuleDto aObjRule, string aStrValue)
    {
        if (!DateTime.TryParse(aStrValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lobjDate))
            return false;

        var lstrOperator = GetString(aObjRule.RuleDetailsJson, "operator");
        if (string.IsNullOrEmpty(lstrOperator)) return true;

        var ldtToday = DateTime.Now.Date;

        return lstrOperator switch
        {
            "<=Today" => lobjDate.Date <= ldtToday,
            ">=Today" => lobjDate.Date >= ldtToday,
            "<Today" => lobjDate.Date < ldtToday,
            ">Today" => lobjDate.Date > ldtToday,
            _ => true
        };
    }

    private static bool EvaluateCompareFields(FormRuleDto aObjRule, string aStrValue, IReadOnlyDictionary<string, object?> aObjSubmittedValues)
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