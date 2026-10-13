using NewDynamicFormGenAPI.Models.Common;
using NewDynamicFormGenAPI.Models.DTOs.FormImage;

namespace NewDynamicFormGenAPI.Models.Interfaces;

/// <summary>
/// Uses the local AI model to support the "Upload Form Image" feature.
/// </summary>
public interface IFormImageAnalysisService
{
    /// <summary>
    /// Asks the AI model for the control type of each field label.
    /// </summary>
    /// <param name="aObjDto">The field labels read from the form image.</param>
    /// <param name="aObjCancellationToken">Cancels the request, e.g. when the browser disconnects.</param>
    /// <returns>
    /// A successful result with one item per label, in the same order; or a failed result with a
    /// message for the user (invalid input, AI busy, AI not available, answer not readable).
    /// </returns>
    Task<Result<ClassifyFieldLabelsResultDto>> ClassifyFieldLabelsAsync(ClassifyFieldLabelsRequestDto aObjDto, CancellationToken aObjCancellationToken = default);

    /// <summary>
    /// Checks whether the AI service can be used.
    /// </summary>
    /// <param name="aObjCancellationToken">Cancels the check.</param>
    /// <returns>The AI status.</returns>
    Task<FormImageAiStatusDto> GetAiStatusAsync(CancellationToken aObjCancellationToken = default);
}
