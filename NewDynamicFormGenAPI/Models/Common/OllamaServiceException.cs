namespace NewDynamicFormGenAPI.Models.Common;

/// <summary>
/// Thrown when the local Ollama AI service cannot give a usable answer.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Exception.Message"/> is written for the end user (for example
/// "The AI service is not available. Check that Ollama is running.") so the caller
/// can return it directly in a <see cref="Result{T}"/>.
/// </para>
/// </remarks>
public class OllamaServiceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaServiceException"/> class.
    /// </summary>
    /// <param name="aStrMessage">A message that can be shown to the end user.</param>
    public OllamaServiceException(string aStrMessage)
        : base(aStrMessage)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaServiceException"/> class.
    /// </summary>
    /// <param name="aStrMessage">A message that can be shown to the end user.</param>
    /// <param name="aObjInnerException">The technical error that caused this exception.</param>
    public OllamaServiceException(string aStrMessage, Exception aObjInnerException)
        : base(aStrMessage, aObjInnerException)
    {
    }
}
