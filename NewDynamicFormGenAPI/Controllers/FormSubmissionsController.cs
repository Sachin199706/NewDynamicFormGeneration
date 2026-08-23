using Microsoft.AspNetCore.Mvc;
using NewDynamicFormGenAPI.Models.DTOs.Submissions;
using NewDynamicFormGenAPI.Models.Interfaces;

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

    // POST api/forms/{formId}/submissions
    [HttpPost("forms/{formId:int}/submissions")]
    public async Task<IActionResult> Submit(int formId, [FromBody] SubmitFormDto dto)
    {
        dto.FormId = formId;
        var result = await _submissionService.SubmitAsync(dto);
        return result.Success ? Ok(result) : BadRequest(result); // 400 => rule validation failures in result.Errors
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
