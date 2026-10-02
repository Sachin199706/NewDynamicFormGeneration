using NewDynamicFormGenAPI.Models.DTOs.Rules;
using NewDynamicFormGenAPI.Models.Entities;
using NewDynamicFormGenAPI.Models.Enums;
using NewDynamicFormGenAPI.Models.Interfaces;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NewDynamicFormGenAPI.Models.Services;

/// <summary>
/// Service for managing form validation and conditional rules within form versions.
/// </summary>
/// <remarks>
/// <para>
/// RuleEngineService provides CRUD operations for form rules. Rules no longer exist in a separate
/// database table; instead, they are embedded within each control's JSON definition inside
/// FormVersions.FormDefinitionJson. This embedding approach enables:
/// <list type="bullet">
/// <item><description>Version snapshots – rules are captured as part of the form version</description></item>
/// <item><description>Form rollback – reverting to prior versions restores their original rules</description></item>
/// <item><description>Submission correlation – each submission references its version's rules</description></item>
/// </list>
/// </para>
/// <para>
/// Rule operations (read, create, update, delete) parse the FormDefinitionJson blob, mutate
/// the rules array for the specified control, and persist the updated definition back to the database.
/// </para>
/// </remarks>
public class RuleEngineService : IRuleEngineService
{
    private readonly IUnitOfWork _uow;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuleEngineService"/> class.
    /// </summary>
    /// <param name="uow">The unit of work for database access.</param>
    public RuleEngineService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    #region Public Methods

    /// <summary>
    /// Retrieves all rules associated with a specific form version.
    /// </summary>
    /// <param name="aNumFormVersionId">The form version ID to extract rules from.</param>
    /// <returns>
    /// A list of <see cref="FormRuleDto"/> containing all validation and conditional rules
    /// embedded in the form version. Returns an empty list if the version is not found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method extracts and flattens rules from the FormDefinitionJson by:
    /// <list type="number">
    /// <item><description>Loading the form version</description></item>
    /// <item><description>Parsing the FormDefinitionJson</description></item>
    /// <item><description>Iterating through all controls and extracting their rules arrays</description></item>
    /// <item><description>Flattening the hierarchical structure into a flat list</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Each returned DTO includes the control key, rule type, configuration, severity, and active status.
    /// </para>
    /// </remarks>
    public async Task<List<FormRuleDto>> GetRulesForVersionAsync(int aNumFormVersionId)
    {
        var lobjVersion = await _uow.Repository<FormVersion>().GetByIdAsync(aNumFormVersionId);
        if (lobjVersion == null) return new List<FormRuleDto>();

        return FlattenRules(lobjVersion.FormDefinitionJson);
    }

    /// <summary>
    /// Adds a new rule to a specific control within a form version.
    /// </summary>
    /// <param name="aNumFormVersionId">The form version ID to add the rule to.</param>
    /// <param name="aObjDto">The rule creation request containing rule type, configuration, and control key.</param>
    /// <returns>
    /// A <see cref="FormRuleDto"/> representing the newly created rule with its assigned RuleId.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method:
    /// <list type="number">
    /// <item><description>Loads the form version and parses its FormDefinitionJson</description></item>
    /// <item><description>Locates the specified control by ControlKey</description></item>
    /// <item><description>Inserts a new rule object with a generated RuleId into the control's rules array</description></item>
    /// <item><description>Persists the updated definition back to the database</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// If the control is not found, a <see cref="KeyNotFoundException"/> is thrown. If the version
    /// is not found, an exception is also thrown.
    /// </para>
    /// <para>
    /// Rules are immediately active (IsActive = true) upon creation. Severity defaults to "Error"
    /// if not specified. DisplayOrder defaults based on existing rules in the control.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Updates an existing rule within a form version.
    /// </summary>
    /// <param name="aNumFormVersionId">The form version ID containing the rule.</param>
    /// <param name="aStrRuleId">The unique rule ID to update.</param>
    /// <param name="aObjDto">The rule update request with revised configuration.</param>
    /// <returns>
    /// A <see cref="FormRuleDto"/> representing the updated rule if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method locates the rule within the FormDefinitionJson by RuleId and updates its properties:
    /// RuleType, RuleDetailsJson, ErrorMessage, Severity, DisplayOrder, and IsActive status.
    /// </para>
    /// <para>
    /// The updated definition is persisted to the database. If the rule is not found, <c>null</c>
    /// is returned to indicate no update occurred.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Deletes a rule from a form version.
    /// </summary>
    /// <param name="aNumFormVersionId">The form version ID containing the rule.</param>
    /// <param name="aStrRuleId">The unique rule ID to delete.</param>
    /// <remarks>
    /// <para>
    /// This method removes the specified rule from the form version's FormDefinitionJson by:
    /// <list type="number">
    /// <item><description>Loading and parsing the form definition</description></item>
    /// <item><description>Locating the rule by RuleId across all controls</description></item>
    /// <item><description>Removing the rule from the control's rules array</description></item>
    /// <item><description>Persisting the updated definition back to the database</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// If the rule or version is not found, the operation completes without error (idempotent behavior).
    /// </para>
    /// </remarks>
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