namespace NewDynamicFormGenAPI.Models.Common;

/// <summary>
/// Settings for the local Ollama AI service used by the "Upload Form Image" feature.
/// </summary>
/// <remarks>
/// <para>
/// Values are read from the <c>Ollama</c> section of <c>appsettings.json</c>.
/// The defaults below are used when a value is missing from the configuration.
/// </para>
/// </remarks>
public class OllamaOptions
{
    /// <summary>
    /// Name of the configuration section in <c>appsettings.json</c>.
    /// </summary>
    public const string SectionName = "Ollama";

    /// <summary>
    /// Name of the rate limit policy applied to the AI analysis endpoint.
    /// </summary>
    public const string RateLimitPolicyName = "FormImageAnalysis";

    /// <summary>
    /// Gets or sets the address of the Ollama server, e.g. <c>http://localhost:11434</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Gets or sets the Ollama model that turns the form text into fields, e.g. <c>llama3.2</c>.
    /// </summary>
    public string ModelName { get; set; } = "llama3.2";

    /// <summary>
    /// Gets or sets how many seconds to wait for an answer from Ollama before giving up.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the maximum number of characters of form text accepted for one analysis.
    /// </summary>
    public int MaxFormTextLength { get; set; } = 20000;

    /// <summary>
    /// Gets or sets how many AI analysis requests are accepted per minute.
    /// </summary>
    public int MaxRequestsPerMinute { get; set; } = 10;
}
