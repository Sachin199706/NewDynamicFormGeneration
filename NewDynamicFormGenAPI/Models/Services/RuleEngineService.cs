using NewDynamicFormGenAPI.Models.DTOs.Forms;
using NewDynamicFormGenAPI.Models.DTOs.Rules;
using NewDynamicFormGenAPI.Models.Entities;
using NewDynamicFormGenAPI.Models.Enums;
using NewDynamicFormGenAPI.Models.Interfaces;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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
    private const string PhonePattern = @"^\+?[0-9\s\-()]{10,15}$";
    private const string UrlPattern = @"^https?://[^\s/$.?#].[^\s]*$";
    private const string NumberPattern = @"^-?\d+(\.\d+)?$";
    private const string AlphanumericPattern = @"^[A-Za-z0-9]+$";

    public RuleEngineService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    #region Public Methods

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

        var lobjRulesArr = GetOrCreateRulesArray(lobjRoot, aObjDto.ControlKey)
            ?? throw new KeyNotFoundException($"Control '{aObjDto.ControlKey}' not found on this version.");

        var lstrSeverity = string.IsNullOrWhiteSpace(aObjDto.Severity) ? RuleSeverity.Error : aObjDto.Severity;
        var lstrRuleId = Guid.NewGuid().ToString();

        lobjRulesArr.Add(new JsonObject
        {
            ["ruleId"] = lstrRuleId,
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
            RuleId = lstrRuleId,
            ControlKey = aObjDto.ControlKey,
            RuleType = aObjDto.RuleType,
            RuleDetailsJson = aObjDto.RuleDetailsJson,
            ErrorMessage = aObjDto.ErrorMessage,
            Severity = lstrSeverity,
            DisplayOrder = aObjDto.DisplayOrder,
            IsActive = true
        };
    }

    public async Task<FormRuleDto?> UpdateRuleAsync(int aNumFormVersionId, string aStrRuleId, CreateFormRuleDto aObjDto)
    {
        var lobjRepo = _uow.Repository<FormVersion>();
        var lobjVersion = await lobjRepo.GetByIdAsync(aNumFormVersionId);
        if (lobjVersion == null) return null;

        var lobjRoot = JsonNode.Parse(lobjVersion.FormDefinitionJson)!.AsObject();
        var (lobjRulesArr, lnumIndex, _) = FindRuleById(lobjRoot, aStrRuleId);
        if (lobjRulesArr == null || lnumIndex < 0) return null;

        var lstrSeverity = string.IsNullOrWhiteSpace(aObjDto.Severity) ? RuleSeverity.Error : aObjDto.Severity;

        // The edit may move the rule to a different control, so it is removed from the old
        // control's array and appended to the target's rather than updated in place.
        var lstrExistingId = (lobjRulesArr[lnumIndex] as JsonObject)?["ruleId"]?.GetValue<string>();
        var lstrRuleId = string.IsNullOrEmpty(lstrExistingId) ? Guid.NewGuid().ToString() : lstrExistingId;

        lobjRulesArr.RemoveAt(lnumIndex);

        var lobjTargetRules = GetOrCreateRulesArray(lobjRoot, aObjDto.ControlKey)
            ?? throw new KeyNotFoundException($"Control '{aObjDto.ControlKey}' not found on this version.");

        lobjTargetRules.Add(new JsonObject
        {
            ["ruleId"] = lstrRuleId,
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
            RuleId = lstrRuleId,
            ControlKey = aObjDto.ControlKey,
            RuleType = aObjDto.RuleType,
            RuleDetailsJson = aObjDto.RuleDetailsJson,
            ErrorMessage = aObjDto.ErrorMessage,
            Severity = lstrSeverity,
            DisplayOrder = aObjDto.DisplayOrder,
            IsActive = true
        };
    }

    public async Task DeleteRuleAsync(int aNumFormVersionId, string aStrRuleId)
    {
        var lobjRepo = _uow.Repository<FormVersion>();
        var lobjVersion = await lobjRepo.GetByIdAsync(aNumFormVersionId);
        if (lobjVersion == null) return;

        var lobjRoot = JsonNode.Parse(lobjVersion.FormDefinitionJson)!.AsObject();
        var (lobjRulesArr, lnumIndex, _) = FindRuleById(lobjRoot, aStrRuleId);
        if (lobjRulesArr == null || lnumIndex < 0) return;

        lobjRulesArr.RemoveAt(lnumIndex);

        lobjVersion.FormDefinitionJson = lobjRoot.ToJsonString();
        lobjRepo.Update(lobjVersion);
        await _uow.SaveChangesAsync();
    }

    /// <summary>
    /// Conditional rules change form state rather than failing a submission, so their
    /// output is a per-control effect map rather than a pass/fail.
    /// </summary>
    public Dictionary<string, ControlEffectsDto> ComputeEffects(List<FormRuleDto> aArrRules,IReadOnlyDictionary<string, object?> aObjSubmittedValues,List<FormControlDto> aArrControls)
    {
        var lobjEffects = new Dictionary<string, ControlEffectsDto>(StringComparer.OrdinalIgnoreCase);

        // Every control starts visible and enabled; required comes from its own definition.
        foreach (var lobjControl in aArrControls)
        {
            lobjEffects[lobjControl.ControlKey] = new ControlEffectsDto
            {
                Visible = true,
                Enabled = true,
                Required = lobjControl.IsRequired
            };
        }

        // A stored Required rule is another way of saying the control is required by default,
        // so it seeds the same flag — and a Required/Optional rule below can then override it.
        foreach (var rule in aArrRules.Where(r => r.IsActive && NormalizeRuleType(r.RuleType) == RuleType.Required))
        {
            if (lobjEffects.TryGetValue(rule.ControlKey, out var lobjTarget))
                lobjTarget.Required = true;
        }

        foreach (var rule in aArrRules.Where(r => r.IsActive && IsConditional(r.RuleType)).OrderBy(r => r.DisplayOrder))
        {
            var larrConditions = ParseConditions(rule.RuleDetailsJson);
            if (larrConditions.Count == 0) continue;

            var lstrLogic = GetString(rule.RuleDetailsJson, "logic") ?? ConditionLogic.And;
            var lstrAction = GetString(rule.RuleDetailsJson, "action") ?? ConditionalAction.Show;

            var lboolConditionMet = EvaluateConditions(larrConditions, lstrLogic, aObjSubmittedValues);

            if (!lobjEffects.TryGetValue(rule.ControlKey, out var lobjEffect))
            {
                lobjEffect = new ControlEffectsDto();
                lobjEffects[rule.ControlKey] = lobjEffect;
            }

            switch (lstrAction)
            {
                case ConditionalAction.Show: lobjEffect.Visible = lboolConditionMet; break;
                case ConditionalAction.Hide: lobjEffect.Visible = !lboolConditionMet; break;
                case ConditionalAction.Enable: lobjEffect.Enabled = lboolConditionMet; break;
                case ConditionalAction.Disable: lobjEffect.Enabled = !lboolConditionMet; break;
                case ConditionalAction.Required: lobjEffect.Required = lboolConditionMet; break;
                case ConditionalAction.Optional: lobjEffect.Required = !lboolConditionMet; break;
            }
        }

        return lobjEffects;
    }

    /// <summary>
    /// Pure evaluation — same semantics as the Angular RuleEngineService.
    /// Keep both implementations' switch cases in lockstep when adding a new RuleType.
    /// </summary>
    public RuleEvaluationResultDto Evaluate(
        List<FormRuleDto> aArrRules,
        IReadOnlyDictionary<string, object?> aObjSubmittedValues,
        List<FormControlDto> aArrControls)
    {
        var lobjResult = new RuleEvaluationResultDto { IsValid = true };
        var lobjEffects = ComputeEffects(aArrRules, aObjSubmittedValues, aArrControls);

        // A stored Required rule carries its own message; keep it so the effect check below
        // can reuse it rather than always falling back to a generated one.
        var lobjRequiredMessages = aArrRules
            .Where(r => r.IsActive && NormalizeRuleType(r.RuleType) == RuleType.Required)
            .GroupBy(r => r.ControlKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().ErrorMessage, StringComparer.OrdinalIgnoreCase);

        // Required is checked per control, not per rule: a conditional rule can make a control
        // required even when it has no Required rule of its own, and can make one optional
        // that does.
        foreach (var lobjControl in aArrControls)
        {
            if (!lobjEffects.TryGetValue(lobjControl.ControlKey, out var lobjEffect)) continue;
            if (!lobjEffect.Required || !lobjEffect.Visible || !lobjEffect.Enabled) continue;

            aObjSubmittedValues.TryGetValue(lobjControl.ControlKey, out var lobjRaw);
            if (!string.IsNullOrWhiteSpace(lobjRaw?.ToString())) continue;

            lobjResult.Failures.Add(new RuleFailureDto
            {
                ControlKey = lobjControl.ControlKey,
                RuleType = RuleType.Required,
                ErrorMessage = lobjRequiredMessages.TryGetValue(lobjControl.ControlKey, out var lstrMessage)
                    ? lstrMessage
                    : $"{lobjControl.Label ?? lobjControl.ControlKey} is required.",
                Severity = RuleSeverity.Error
            });
            lobjResult.IsValid = false;
        }

        foreach (var rule in aArrRules.Where(r => r.IsActive).OrderBy(r => r.DisplayOrder))
        {
            // Conditional rules produced the effect map above; they never fail a submission.
            if (IsConditional(rule.RuleType)) continue;

            var lstrRuleType = NormalizeRuleType(rule.RuleType);

            // Handled by the per-control loop above, which knows about conditional overrides.
            if (lstrRuleType == RuleType.Required) continue;

            // A control the user could not see or edit is not held to its rules.
            if (lobjEffects.TryGetValue(rule.ControlKey, out var lobjEffect)
                && (!lobjEffect.Visible || !lobjEffect.Enabled))
                continue;

            aObjSubmittedValues.TryGetValue(rule.ControlKey, out var lobjRawValue);
            var lstrStringValue = lobjRawValue?.ToString() ?? string.Empty;

            // A blank optional field never trips a pattern or range check.
            if (string.IsNullOrWhiteSpace(lstrStringValue)) continue;

            bool lboolPassed = lstrRuleType switch
            {
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

    /// <summary>
    /// File rules are evaluated separately from Evaluate() because they need the uploaded
    /// file itself — by the time Evaluate() runs, Values holds only the stored filename and
    /// the size is gone. Called before anything is written to disk.
    /// </summary>
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

    #endregion

    #region Private Methods

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

    private static bool IsConditional(string aStrRuleType) =>
        aStrRuleType is RuleType.Visibility or RuleType.EnableDisable or RuleType.RequiredOptional;

    /// <summary>
    /// Walks every control's rules array looking for one id. Rules saved before rule
    /// identity existed have no "ruleId", so the synthesised form is matched too —
    /// see FlattenRules for how that fallback is built.
    /// </summary>
    private static (JsonArray? Rules, int Index, string ControlKey) FindRuleById(JsonObject aObjRoot, string aStrRuleId)
    {
        var lobjControlsArr = aObjRoot["controls"]?.AsArray();
        if (lobjControlsArr == null) return (null, -1, "");

        foreach (var lobjNode in lobjControlsArr)
        {
            if (lobjNode is not JsonObject lobjControl) continue;

            var lstrControlKey = lobjControl["controlKey"]?.GetValue<string>() ?? "";
            var lobjRulesArr = lobjControl["rules"]?.AsArray();
            if (lobjRulesArr == null) continue;

            for (int i = 0; i < lobjRulesArr.Count; i++)
            {
                if (lobjRulesArr[i] is not JsonObject lobjRule) continue;

                var lstrStoredId = lobjRule["ruleId"]?.GetValue<string>();
                var lstrEffectiveId = string.IsNullOrEmpty(lstrStoredId)
                    ? BuildLegacyRuleId(lstrControlKey, lobjRule["ruleType"]?.GetValue<string>() ?? "", i)
                    : lstrStoredId;

                if (string.Equals(lstrEffectiveId, aStrRuleId, StringComparison.Ordinal))
                    return (lobjRulesArr, i, lstrControlKey);
            }
        }

        return (null, -1, "");
    }

    /// <summary>Deterministic id for a rule written before ruleId existed, so it stays
    /// editable without a data migration.</summary>
    private static string BuildLegacyRuleId(string aStrControlKey, string aStrRuleType, int aNumIndex)
        => $"legacy:{aStrControlKey}#{aStrRuleType}#{aNumIndex}";

    /// <summary>Finds a control's rules array, creating it when the control has none yet.</summary>
    private static JsonArray? GetOrCreateRulesArray(JsonObject aObjRoot, string aStrControlKey)
    {
        var lobjControlsArr = aObjRoot["controls"]?.AsArray();
        if (lobjControlsArr == null) return null;

        foreach (var lobjNode in lobjControlsArr)
        {
            if (lobjNode is not JsonObject lobjControl) continue;
            if (!string.Equals(lobjControl["controlKey"]?.GetValue<string>(), aStrControlKey, StringComparison.OrdinalIgnoreCase))
                continue;

            var lobjRulesArr = lobjControl["rules"]?.AsArray();
            if (lobjRulesArr == null)
            {
                lobjRulesArr = new JsonArray();
                lobjControl["rules"] = lobjRulesArr;
            }
            return lobjRulesArr;
        }

        return null;
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

                // Position within this control's rules array — the legacy id fallback depends
                // on it, so it must count every rule, not just the ones that parse.
                int lnumIndex = 0;

                foreach (var lobjRuleEl in lobjRulesEl.EnumerateArray())
                {
                    var lstrRuleType = lobjRuleEl.TryGetProperty("ruleType", out var t) ? t.GetString() ?? "" : "";
                    var lstrStoredId = lobjRuleEl.TryGetProperty("ruleId", out var idEl) ? idEl.GetString() : null;

                    larrResult.Add(new FormRuleDto
                    {
                        RuleId = string.IsNullOrEmpty(lstrStoredId)
                            ? BuildLegacyRuleId(lstrControlKey, lstrRuleType, lnumIndex)
                            : lstrStoredId,
                        ControlKey = lstrControlKey,
                        RuleType = lstrRuleType,
                        RuleDetailsJson = lobjRuleEl.TryGetProperty("ruleDetailsJson", out var d) ? d.GetString() : null,
                        ErrorMessage = lobjRuleEl.TryGetProperty("errorMessage", out var e) ? e.GetString() ?? "" : "",
                        Severity = lobjRuleEl.TryGetProperty("severity", out var s) ? s.GetString() ?? "Error" : "Error",
                        DisplayOrder = lobjRuleEl.TryGetProperty("displayOrder", out var o) && o.TryGetInt32(out var ov) ? ov : 0,
                        IsActive = !lobjRuleEl.TryGetProperty("isActive", out var a) || a.GetBoolean()
                    });

                    lnumIndex++;
                }
            }
        }
        catch { /* malformed JSON — return whatever was parsed so far */ }

        return larrResult.OrderBy(r => r.DisplayOrder).ToList();
    }

    /// <summary>
    /// Numeric when both sides parse as numbers, string otherwise. All six operators are
    /// supported — the old ComputeVisibility silently treated anything other than "!=" as
    /// equality, which meant the client and server disagreed on the four ordering operators.
    /// </summary>
    private static bool CompareValues(string aStrActual, string aStrExpected, string aStrOperator)
    {
        if (double.TryParse(aStrActual, NumberStyles.Any, CultureInfo.InvariantCulture, out var lnumA)
            && double.TryParse(aStrExpected, NumberStyles.Any, CultureInfo.InvariantCulture, out var lnumB))
        {
            return aStrOperator switch
            {
                "==" => lnumA == lnumB,
                "!=" => lnumA != lnumB,
                "<" => lnumA < lnumB,
                "<=" => lnumA <= lnumB,
                ">" => lnumA > lnumB,
                ">=" => lnumA >= lnumB,
                _ => lnumA == lnumB
            };
        }

        return aStrOperator == "!="
            ? !string.Equals(aStrActual, aStrExpected, StringComparison.Ordinal)
            : string.Equals(aStrActual, aStrExpected, StringComparison.Ordinal);
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

    private static List<RuleConditionDto> ParseConditions(string? aStrDetailsJson)
    {
        var larrConditions = new List<RuleConditionDto>();
        if (string.IsNullOrWhiteSpace(aStrDetailsJson)) return larrConditions;

        try
        {
            using var lobjDoc = JsonDocument.Parse(aStrDetailsJson);
            var lobjRoot = lobjDoc.RootElement;

            if (lobjRoot.TryGetProperty("conditions", out var lobjArr) && lobjArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var lobjItem in lobjArr.EnumerateArray())
                {
                    larrConditions.Add(new RuleConditionDto
                    {
                        ControlKey = lobjItem.TryGetProperty("controlKey", out var k) ? k.GetString() ?? "" : "",
                        Operator = lobjItem.TryGetProperty("operator", out var o) ? o.GetString() ?? "==" : "==",
                        Value = lobjItem.TryGetProperty("value", out var v) ? v.GetString() ?? "" : ""
                    });
                }

                return larrConditions;
            }

            // Legacy single-trigger shape.
            if (lobjRoot.TryGetProperty("triggerControlKey", out var lobjKeyEl))
            {
                larrConditions.Add(new RuleConditionDto
                {
                    ControlKey = lobjKeyEl.GetString() ?? "",
                    Operator = lobjRoot.TryGetProperty("operator", out var o2) ? o2.GetString() ?? "==" : "==",
                    Value = lobjRoot.TryGetProperty("triggerValue", out var v2) ? v2.GetString() ?? "" : ""
                });
            }
        }
        catch { /* malformed details — treat as no conditions, which never fires */ }

        return larrConditions;
    }
    private static bool EvaluateConditions(List<RuleConditionDto> aArrConditions,string aStrLogic,IReadOnlyDictionary<string, object?> aObjValues)
    {
        var larrUsable = aArrConditions.Where(c => !string.IsNullOrEmpty(c.ControlKey)).ToList();
        if (larrUsable.Count == 0) return false;

        var lboolIsOr = string.Equals(aStrLogic, ConditionLogic.Or, StringComparison.OrdinalIgnoreCase);

        foreach (var lobjCondition in larrUsable)
        {
            aObjValues.TryGetValue(lobjCondition.ControlKey, out var lobjRaw);
            var lstrActual = lobjRaw?.ToString() ?? string.Empty;

            var lboolMet = CompareValues(lstrActual, lobjCondition.Value, lobjCondition.Operator);

            if (lboolIsOr && lboolMet) return true;
            if (!lboolIsOr && !lboolMet) return false;
        }

        return !lboolIsOr;
    }
    #endregion
}