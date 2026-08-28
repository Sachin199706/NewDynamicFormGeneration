using Microsoft.AspNetCore.Mvc;
using NewDynamicFormGenAPI.Models.DTOs.Rules;
using NewDynamicFormGenAPI.Models.Interfaces;

namespace NewDynamicFormGenAPI.API.Controllers;

/// <summary>
/// Backs the Rule Builder screen (Validation Rules panel). Rules are always scoped
/// to a specific FormVersion so editing a draft never mutates a published version.
/// No auth in this application — reachable by URL alone.
/// </summary>
[ApiController]
[Route("api")]
public class FormRulesController : ControllerBase
{
    private readonly IRuleEngineService _ruleEngine;

    public FormRulesController(IRuleEngineService ruleEngine)
    {
        _ruleEngine = ruleEngine;
    }

    [HttpGet("forms/versions/{aNumFormVersionId:int}/rules")]
    public async Task<IActionResult> GetRules(int aNumFormVersionId)
    {
        var lobjRules = await _ruleEngine.GetRulesForVersionAsync(aNumFormVersionId);
        return Ok(lobjRules);
    }

    [HttpPost("forms/versions/{aNumFormVersionId:int}/rules")]
    public async Task<IActionResult> AddRule(int aNumFormVersionId, [FromBody] CreateFormRuleDto aObjDto)
    {
        var lobjRule = await _ruleEngine.AddRuleAsync(aNumFormVersionId, aObjDto);
        return Ok(lobjRule);
    }

    [HttpPut("rules/{aNumRuleId:int}")]
    public async Task<IActionResult> UpdateRule(int aNumRuleId, [FromBody] CreateFormRuleDto aObjDto)
    {
        await _ruleEngine.UpdateRuleAsync(aNumRuleId, aObjDto);
        return NoContent();
    }

    [HttpDelete("rules/{aNumRuleId:int}")]
    public async Task<IActionResult> DeleteRule(int aNumRuleId)
    {
        await _ruleEngine.DeleteRuleAsync(aNumRuleId);
        return NoContent();
    }
}