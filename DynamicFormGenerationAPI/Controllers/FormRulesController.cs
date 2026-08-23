using Microsoft.AspNetCore.Mvc;
using FormGen.Application.DTOs.Rules;
using FormGen.Application.Interfaces;

namespace FormGen.API.Controllers
{
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

        [HttpGet("forms/versions/{formVersionId:int}/rules")]
        public async Task<IActionResult> GetRules(int formVersionId)
        {
            var rules = await _ruleEngine.GetRulesForVersionAsync(formVersionId);
            return Ok(rules);
        }

        [HttpPost("forms/versions/{formVersionId:int}/rules")]
        public async Task<IActionResult> AddRule(int formVersionId, [FromBody] CreateFormRuleDto dto)
        {
            var rule = await _ruleEngine.AddRuleAsync(formVersionId, dto);
            return Ok(rule);
        }

        [HttpPut("rules/{ruleId:int}")]
        public async Task<IActionResult> UpdateRule(int ruleId, [FromBody] CreateFormRuleDto dto)
        {
            await _ruleEngine.UpdateRuleAsync(ruleId, dto);
            return NoContent();
        }

        [HttpDelete("rules/{ruleId:int}")]
        public async Task<IActionResult> DeleteRule(int ruleId)
        {
            await _ruleEngine.DeleteRuleAsync(ruleId);
            return NoContent();
        }
    }
}
