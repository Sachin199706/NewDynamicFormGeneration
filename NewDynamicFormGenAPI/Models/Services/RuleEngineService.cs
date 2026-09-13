using NewDynamicFormGenAPI.Models.DTOs.Rules;
using NewDynamicFormGenAPI.Models.Entities;
using NewDynamicFormGenAPI.Models.Enums;
using NewDynamicFormGenAPI.Models.Interfaces;
using System.Text.Json;
using System.Text.Json.Nodes;

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
    #endregion

    #region Private Methods

   
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
    #endregion
}