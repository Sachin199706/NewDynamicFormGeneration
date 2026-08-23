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
    public async Task<IActionResult> GetForms([FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        var result = await _formService.GetFormsAsync(page, pageSize, search);
        return Ok(result);
    }

    // GET api/forms/{formId}/versions/latest
    [HttpGet("{formId:int}/versions/latest")]
    public async Task<IActionResult> GetLatestVersion(int formId)
    {
        var result = await _formService.GetLatestVersionAsync(formId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    // POST api/forms/versions   — save a new design version (create or update)
    [HttpPost("versions")]
    public async Task<IActionResult> SaveVersion([FromBody] SaveFormVersionDto dto)
    {
        var result = await _formService.SaveVersionAsync(dto);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // PUT api/forms/{formId}/versions/{versionId}/publish
    [HttpPut("{formId:int}/versions/{versionId:int}/publish")]
    public async Task<IActionResult> Publish(int formId, int versionId)
    {
        var result = await _formService.PublishAsync(formId, versionId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // GET api/forms/{formId}/versions/{versionId}/render — payload for the fill-in screen
    [HttpGet("{formId:int}/versions/{versionId:int}/render")]
    public async Task<IActionResult> GetRenderPayload(int formId, int versionId)
    {
        var result = await _formService.GetRenderPayloadAsync(formId, versionId);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
