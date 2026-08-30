using Microsoft.AspNetCore.Mvc;
using NewDynamicFormGenAPI.Models.DTOs.Submissions;
using NewDynamicFormGenAPI.Models.Interfaces;
using System.Text.Json;

namespace NewDynamicFormGenAPI.API.Controllers;

// No auth in this application — reachable by URL alone.
[ApiController]
[Route("api")]
public class FormSubmissionsController : ControllerBase
{
    private readonly ISubmissionService _submissionService;

    public FormSubmissionsController(ISubmissionService submissionService)
    {
        _submissionService = submissionService;
    }

    [HttpPost("forms/{aNumFormId:int}/submissions")]
    public async Task<IActionResult> Submit(int aNumFormId, [FromForm] string values, [FromForm] int formVersionId)
    {
        var lobjDto = new SubmitFormDto
        {
            FormId = aNumFormId,
            FormVersionId = formVersionId,
            Values = JsonSerializer.Deserialize<Dictionary<string, object?>>(values) ?? new Dictionary<string, object?>()
        };

        var lobjResult = await _submissionService.SubmitAsync(lobjDto, Request.Form.Files);
        return lobjResult.Success ? Ok(lobjResult) : BadRequest(lobjResult);
    }

    [HttpGet("forms/{formId:int}/submissions")]
    public async Task<IActionResult> GetSubmissions(int formId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _submissionService.GetSubmissionsAsync(formId, page, pageSize);
        return Ok(result);
    }

    [HttpGet("submissions/{submissionId:int}")]
    public async Task<IActionResult> GetDetail(int submissionId)
    {
        var result = await _submissionService.GetDetailAsync(submissionId);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
