using Microsoft.AspNetCore.Mvc;
using NewDynamicFormGenAPI.Models.DTOs.Rules;
using NewDynamicFormGenAPI.Models.Interfaces;

namespace NewDynamicFormGenAPI.API.Controllers;

/// <summary>
/// Backs the Rule Builder screen (Validation Rules panel). Rules live embedded inside
/// FormVersions.FormDefinitionJson now — no FormRules table. No auth in this application.
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

    [HttpDelete("{aNumFormVersionId:int}/rules/{aStrRuleId}")]
    public async Task<IActionResult> DeleteRule(int aNumFormVersionId, string aStrRuleId)
    {
        await _ruleEngine.DeleteRuleAsync(aNumFormVersionId, aStrRuleId);
        return NoContent();
    }

    [HttpPut("{aNumFormVersionId:int}/rules/{aStrRuleId}")]
    public async Task<IActionResult> UpdateRule(int aNumFormVersionId, string aStrRuleId, [FromBody] CreateFormRuleDto aObjDto)
    {
        var lobjUpdated = await _ruleEngine.UpdateRuleAsync(aNumFormVersionId, aStrRuleId, aObjDto);
        return lobjUpdated is null ? NotFound() : Ok(lobjUpdated);
    }
}