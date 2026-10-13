using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NewDynamicFormGenAPI.Models.Common;
using NewDynamicFormGenAPI.Models.DTOs.FormImage;
using NewDynamicFormGenAPI.Models.Interfaces;

namespace NewDynamicFormGenAPI.API.Controllers;

// No auth in this application — every screen, including this one, is reachable by URL alone.
// The AI endpoint is protected by a rate limit and a one-request-at-a-time lock instead.

/// <summary>
/// API controller for the "Upload Form Image" feature.
/// </summary>
/// <remarks>
/// <para>
/// The Angular page reads the form image with OCR and applies the fixed rules itself.
/// This controller only gives access to the local AI model:
/// <list type="bullet">
///   <item><description><c>GET api/form-image/status</c> – whether the AI service can be used</description></item>
///   <item><description><c>POST api/form-image/classify-labels</c> – the control type of each field label</description></item>
/// </list>
/// </para>
/// <para>
/// Both endpoints return the existing <see cref="Result{T}"/> shape. A failed analysis returns
/// HTTP 400 with <c>success: false</c> and a message for the user, like the other controllers.
/// </para>
/// </remarks>
[ApiController]
[Route("api/form-image")]
public class FormImageController : ControllerBase
{
    private readonly IFormImageAnalysisService _formImageAnalysisService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormImageController"/> class.
    /// </summary>
    /// <param name="formImageAnalysisService">The service that uses the local AI model.</param>
    public FormImageController(IFormImageAnalysisService formImageAnalysisService)
    {
        _formImageAnalysisService = formImageAnalysisService;
    }

    /// <summary>
    /// Tells whether the AI service is running and the model is installed.
    /// </summary>
    /// <param name="aObjCancellationToken">Cancelled when the browser disconnects.</param>
    /// <returns>HTTP 200 with the AI status.</returns>
    /// <remarks>
    /// The Angular page calls this when it opens, so it can show "AI service is not available"
    /// before the user uploads an image.
    /// </remarks>
    [HttpGet("status")]
    public async Task<IActionResult> GetAiStatus(CancellationToken aObjCancellationToken)
    {
        var lobjStatus = await _formImageAnalysisService.GetAiStatusAsync(aObjCancellationToken);
        return Ok(Result<FormImageAiStatusDto>.Ok(lobjStatus));
    }

    /// <summary>
    /// Asks the AI model for the control type of each field label read from a form image.
    /// </summary>
    /// <param name="aObjDto">The field labels whose type the fixed rules could not decide.</param>
    /// <param name="aObjCancellationToken">Cancelled when the browser disconnects.</param>
    /// <returns>
    /// HTTP 200 with one type per label, in the same order; HTTP 400 with a message when the input
    /// is invalid or the AI cannot answer; HTTP 429 when too many requests were sent in one minute.
    /// </returns>
    /// <remarks>
    /// On a computer without a graphics card one call can take one to two minutes.
    /// </remarks>
    [HttpPost("classify-labels")]
    [EnableRateLimiting(OllamaOptions.RateLimitPolicyName)]
    public async Task<IActionResult> ClassifyFieldLabels([FromBody] ClassifyFieldLabelsRequestDto aObjDto, CancellationToken aObjCancellationToken)
    {
        var lobjResult = await _formImageAnalysisService.ClassifyFieldLabelsAsync(aObjDto, aObjCancellationToken);
        return lobjResult.Success ? Ok(lobjResult) : BadRequest(lobjResult);
    }
}
