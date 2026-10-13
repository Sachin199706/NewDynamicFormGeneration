using NewDynamicFormGenAPI.Models.Common;

namespace NewDynamicFormGenAPI.Models.Interfaces;

/// <summary>
/// Sends requests to the local Ollama AI service.
/// </summary>
/// <remarks>
/// <para>
/// This client only handles the HTTP communication with Ollama. The prompt and the JSON schema
/// are built by the caller, so the client can be reused for other AI features later.
/// </para>
/// </remarks>
public interface IOllamaClient
{
    /// <summary>
    /// Sends one prompt to the configured model and returns its answer as JSON text.
    /// </summary>
    /// <param name="aStrPrompt">The full prompt for the model.</param>
    /// <param name="aObjJsonSchema">
    /// The JSON schema the answer must follow. It is sent as the Ollama <c>format</c> field.
    /// </param>
    /// <param name="aObjCancellationToken">Cancels the request, e.g. when the caller disconnects.</param>
    /// <returns>The raw JSON text returned by the model.</returns>
    /// <exception cref="OllamaServiceException">
    /// Ollama is not reachable, does not answer in time, does not have the model, or returns an empty answer.
    /// </exception>
    Task<string> GenerateJsonAsync(string aStrPrompt, object aObjJsonSchema, CancellationToken aObjCancellationToken = default);

    /// <summary>
    /// Checks whether Ollama is running and the configured model is installed.
    /// </summary>
    /// <param name="aObjCancellationToken">Cancels the check.</param>
    /// <returns><c>true</c> if the model can be used; otherwise <c>false</c>. Never throws for connection problems.</returns>
    Task<bool> IsModelAvailableAsync(CancellationToken aObjCancellationToken = default);
}
