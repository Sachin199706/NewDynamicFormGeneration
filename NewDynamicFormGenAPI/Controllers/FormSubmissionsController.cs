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
    [HttpGet("submissions/{submissionId:int}")]
    public async Task<IActionResult> GetDetail(int submissionId)
    {
        var result = await _submissionService.GetDetailAsync(submissionId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPut("submissions/{aNumSubmissionId:int}/mark-read")]
    public async Task<IActionResult> MarkAsRead(int aNumSubmissionId)
    {
        var lobjResult = await _submissionService.MarkAsReadAsync(aNumSubmissionId);
        return lobjResult.Success ? Ok(lobjResult) : NotFound(lobjResult);
    }

    [HttpGet("submissions")]
    public async Task<IActionResult> GetAllSubmissions([FromQuery] SubmissionFilterDto aObjFilter)
    {
        var lobjResult = await _submissionService.GetAllSubmissionsAsync(aObjFilter);
        return Ok(lobjResult);
    }

    [HttpGet("submissions/stats")]
    public async Task<IActionResult> GetStats()
    {
        var lobjResult = await _submissionService.GetStatsAsync();
        return Ok(lobjResult);
    }
    [HttpGet("submissions/stats/{inumID:int}")]
    public async Task<IActionResult> GetStats( int inumID)
    {
        var lobjResult = await _submissionService.GetStatsAsync(inumID);
        return Ok(lobjResult);
    }
}
