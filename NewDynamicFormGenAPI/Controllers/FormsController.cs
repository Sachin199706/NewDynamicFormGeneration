using Microsoft.AspNetCore.Mvc;
using NewDynamicFormGenAPI.Models.DTOs.Forms;
using NewDynamicFormGenAPI.Models.Interfaces;

namespace NewDynamicFormGenAPI.API.Controllers;

// No auth in this application — every screen, including this one, is reachable by URL alone.
[ApiController]
[Route("api/forms")]
public class FormsController : ControllerBase
{
    private readonly IFormService _formService;

    public FormsController(IFormService formService)
    {
        _formService = formService;
    }

    // GET api/forms?page=1&pageSize=10&search=employee
    [HttpGet]
    public async Task<IActionResult> GetForms([FromQuery] int aNumPage = 1, [FromQuery] int aNumPageSize = 10,
        [FromQuery] string? aStrSearch = null)
    {
        var lobjResult = await _formService.GetFormsAsync(aNumPage, aNumPageSize, aStrSearch);
        return Ok(lobjResult);
    }

    // GET api/forms/{aNumFormId}/versions/latest
    [HttpGet("{aNumFormId:int}/versions/latest")]
    public async Task<IActionResult> GetLatestVersion(int aNumFormId)
    {
        var lobjResult = await _formService.GetLatestVersionAsync(aNumFormId);
        return lobjResult.Success ? Ok(lobjResult) : NotFound(lobjResult);
    }

    // POST api/forms/versions   — save a new design version (create or update)
    [HttpPost("versions")]
    public async Task<IActionResult> SaveVersion([FromBody] SaveFormVersionDto aObjDto)
    {
        var lobjResult = await _formService.SaveVersionAsync(aObjDto);
        return lobjResult.Success ? Ok(lobjResult) : BadRequest(lobjResult);
    }

    // PUT api/forms/{aNumFormId}/versions/{aNumVersionId}/publish
    [HttpPut("{aNumFormId:int}/versions/{aNumVersionId:int}/publish")]
    public async Task<IActionResult> Publish(int aNumFormId, int aNumVersionId)
    {
        var lobjResult = await _formService.PublishAsync(aNumFormId, aNumVersionId);
        return lobjResult.Success ? Ok(lobjResult) : BadRequest(lobjResult);
    }

    // GET api/forms/{aNumFormId}/versions/{aNumVersionId}/render — payload for the fill-in screen
    [HttpGet("{aNumFormId:int}/versions/{aNumVersionId:int}/render")]
    public async Task<IActionResult> GetRenderPayload(int aNumFormId, int aNumVersionId)
    {
        var lobjResult = await _formService.GetRenderPayloadAsync(aNumFormId, aNumVersionId);
        return lobjResult.Success ? Ok(lobjResult) : NotFound(lobjResult);
    }
}