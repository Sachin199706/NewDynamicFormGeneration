using Microsoft.AspNetCore.Mvc;
using NewDynamicFormGenAPI.Models.DTOs.Rules;
using NewDynamicFormGenAPI.Models.Interfaces;

namespace NewDynamicFormGenAPI.API.Controllers;

/// <summary>
/// API controller for form validation and conditional rule management.
/// </summary>
/// <remarks>
/// <para>
/// FormRulesController provides REST endpoints for managing form rules (validation rules and conditional actions).
/// Rules are embedded within <see cref="NewDynamicFormGenAPI.Models.Entities.FormVersion.FormDefinitionJson"/>
/// rather than stored in a separate FormRules table, enabling version snapshots and rollback capability.
/// </para>
/// <para>
/// This controller backs the Rule Builder screen (Validation Rules panel) in the form designer,
/// enabling users to define:
/// <list type="bullet">
/// <item><description>Validation rules (Required, MinLength, MaxLength, Email, Pattern, etc.)</description></item>
/// <item><description>Conditional rules (Show/Hide, Enable/Disable controls based on other control values)</description></item>
/// <item><description>Rule severity (Error, Warning) and active/inactive toggle</description></item>
/// </list>
/// </para>
/// </remarks>
[ApiController]
[Route("api")]
public class FormRulesController : ControllerBase
{
    private readonly IRuleEngineService _ruleEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormRulesController"/> class.
    /// </summary>
    /// <param name="ruleEngine">The rule engine service for rule processing.</param>
    public FormRulesController(IRuleEngineService ruleEngine)
    {
        _ruleEngine = ruleEngine;
    }

    /// <summary>
    /// Retrieves all rules associated with a specific form version.
    /// </summary>
    /// <param name="aNumFormVersionId">The form version ID to retrieve rules for.</param>
    /// <returns>
    /// An HTTP 200 OK response containing a list of <see cref="FormRuleDto"/> objects
    /// representing all rules (validation and conditional) for the specified form version.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This endpoint extracts rules from the FormDefinitionJson of the specified form version.
    /// Rules are returned in their logical order within the form definition, maintaining the
    /// designer's intended rule execution sequence.
    /// </para>
    /// <para>
    /// Returned rules include:
    /// <list type="bullet">
    /// <item><description>RuleId – Unique identifier within the form version</description></item>
    /// <item><description>RuleType – Validation rule or conditional action type</description></item>
    /// <item><description>RuleDetailsJson – Rule-specific configuration payload</description></item>
    /// <item><description>IsActive – Whether the rule is currently enforced</description></item>
    /// <item><description>Severity – Error level for validation failure messages</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    [HttpGet("forms/versions/{aNumFormVersionId:int}/rules")]
    public async Task<IActionResult> GetRules(int aNumFormVersionId)
    {
        var lobjRules = await _ruleEngine.GetRulesForVersionAsync(aNumFormVersionId);
        return Ok(lobjRules);
    }

    /// <summary>
    /// Creates and adds a new rule to a form version.
    /// </summary>
    /// <param name="aNumFormVersionId">The form version ID to add the rule to.</param>
    /// <param name="aObjDto">The rule creation request containing rule type, configuration, and settings.</param>
    /// <returns>
    /// An HTTP 200 OK response containing the newly created <see cref="FormRuleDto"/> with assigned RuleId.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This endpoint creates a new rule and embeds it within the form version's FormDefinitionJson.
    /// The form version is automatically updated without creating a new version; existing submissions
    /// referenced by the current version remain unchanged.
    /// </para>
    /// <para>
    /// The request payload (<see cref="CreateFormRuleDto"/>) includes:
    /// <list type="bullet">
    /// <item><description>RuleType – The type of rule (Required, Length, Visibility, etc.)</description></item>
    /// <item><description>RuleDetailsJson – Rule-specific configuration (e.g., min/max length, target control)</description></item>
    /// <item><description>IsActive – Whether the rule is immediately enabled</description></item>
    /// <item><description>Severity – Error level for validation failures</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The returned DTO includes the auto-generated RuleId for subsequent updates or deletions.
    /// </para>
    /// </remarks>
    [HttpPost("forms/versions/{aNumFormVersionId:int}/rules")]
    public async Task<IActionResult> AddRule(int aNumFormVersionId, [FromBody] CreateFormRuleDto aObjDto)
    {
        var lobjRule = await _ruleEngine.AddRuleAsync(aNumFormVersionId, aObjDto);
        return Ok(lobjRule);
    }

    /// <summary>
    /// Deletes a rule from a form version.
    /// </summary>
    /// <param name="aNumFormVersionId">The form version ID containing the rule.</param>
    /// <param name="aStrRuleId">The unique rule ID to delete.</param>
    /// <returns>
    /// An HTTP 204 No Content response indicating successful deletion.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This endpoint removes the specified rule from the form version's FormDefinitionJson.
    /// The deletion is permanent for the current version; existing submissions continue to reference
    /// the rule configuration they were submitted against.
    /// </para>
    /// <para>
    /// If the rule ID does not exist within the form version, the operation completes without error
    /// (idempotent behavior).
    /// </para>
    /// </remarks>
    [HttpDelete("forms/versions/{aNumFormVersionId:int}/rules/{aStrRuleId}")]
    public async Task<IActionResult> DeleteRule(int aNumFormVersionId, string aStrRuleId)
    {
        await _ruleEngine.DeleteRuleAsync(aNumFormVersionId, aStrRuleId);
        return NoContent();
    }

    /// <summary>
    /// Updates an existing rule within a form version.
    /// </summary>
    /// <param name="aNumFormVersionId">The form version ID containing the rule.</param>
    /// <param name="aStrRuleId">The unique rule ID to update.</param>
    /// <param name="aObjDto">The rule update request with revised configuration.</param>
    /// <returns>
    /// An HTTP 200 OK response containing the updated <see cref="FormRuleDto"/> if found;
    /// otherwise, HTTP 404 Not Found if the rule does not exist in the form version.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This endpoint updates the rule configuration within the form version's FormDefinitionJson.
    /// Changes take effect immediately for new submissions; prior submissions retain their original
    /// rule configuration snapshot.
    /// </para>
    /// <para>
    /// The update request (<see cref="CreateFormRuleDto"/>) can modify:
    /// <list type="bullet">
    /// <item><description>RuleType – The rule category (though this may require re-validation in the UI)</description></item>
    /// <item><description>RuleDetailsJson – Rule-specific configuration parameters</description></item>
    /// <item><description>IsActive – Enable or disable the rule</description></item>
    /// <item><description>Severity – Change error level for validation failures</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    [HttpPut("forms/versions/{aNumFormVersionId:int}/rules/{aStrRuleId}")]
    public async Task<IActionResult> UpdateRule(int aNumFormVersionId, string aStrRuleId, [FromBody] CreateFormRuleDto aObjDto)
    {
        var lobjUpdated = await _ruleEngine.UpdateRuleAsync(aNumFormVersionId, aStrRuleId, aObjDto);
        return lobjUpdated is null ? NotFound() : Ok(lobjUpdated);
    }
}