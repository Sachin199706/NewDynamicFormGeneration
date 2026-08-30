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

    [HttpDelete("forms/versions/{aNumFormVersionId:int}/rules/{aStrControlKey}/{aStrRuleType}")]
    public async Task<IActionResult> DeleteRule(int aNumFormVersionId, string aStrControlKey, string aStrRuleType)
    {
        await _ruleEngine.DeleteRuleAsync(aNumFormVersionId, aStrControlKey, aStrRuleType);
        return NoContent();
    }
}