using System.Text.Json.Serialization;

namespace NewDynamicFormGenAPI.Models.DTOs.Ollama;

/// <summary>
/// Request body for the Ollama <c>POST /api/chat</c> endpoint.
/// </summary>
public class OllamaChatRequestDto
{
    /// <summary>
    /// Gets or sets the name of the model to use, e.g. <c>llama3.2</c>.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the conversation sent to the model.
    /// </summary>
    [JsonPropertyName("messages")]
    public List<OllamaChatMessageDto> Messages { get; set; } = new();

    /// <summary>
    /// Gets or sets the JSON schema the answer must follow. Ollama forces the model output into this shape.
    /// </summary>
    [JsonPropertyName("format")]
    public object? Format { get; set; }

    /// <summary>
    /// Gets or sets whether the answer is streamed. Always <c>false</c> here, so the full answer comes in one response.
    /// </summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    /// <summary>
    /// Gets or sets model parameters such as temperature.
    /// </summary>
    [JsonPropertyName("options")]
    public OllamaModelOptionsDto Options { get; set; } = new();
}

/// <summary>
/// One message in an Ollama chat request or response.
/// </summary>
public class OllamaChatMessageDto
{
    /// <summary>
    /// Gets or sets who wrote the message: <c>system</c>, <c>user</c> or <c>assistant</c>.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message text.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Model parameters sent with an Ollama chat request.
/// </summary>
public class OllamaModelOptionsDto
{
    /// <summary>
    /// Gets or sets the temperature. <c>0</c> gives the most stable, repeatable answer.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }
}

/// <summary>
/// Response body of the Ollama <c>POST /api/chat</c> endpoint (non-streaming).
/// </summary>
public class OllamaChatResponseDto
{
    /// <summary>
    /// Gets or sets the name of the model that answered.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the answer from the model.
    /// </summary>
    [JsonPropertyName("message")]
    public OllamaChatMessageDto? Message { get; set; }

    /// <summary>
    /// Gets or sets whether the answer is complete.
    /// </summary>
    [JsonPropertyName("done")]
    public bool Done { get; set; }

    /// <summary>
    /// Gets or sets the total time Ollama spent on the request, in nanoseconds.
    /// </summary>
    [JsonPropertyName("total_duration")]
    public long? TotalDuration { get; set; }

    /// <summary>
    /// Gets or sets the error text Ollama returns when the request fails, e.g. when the model is not found.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Response body of the Ollama <c>GET /api/tags</c> endpoint, which lists the installed models.
/// </summary>
public class OllamaTagsResponseDto
{
    /// <summary>
    /// Gets or sets the installed models.
    /// </summary>
    [JsonPropertyName("models")]
    public List<OllamaModelInfoDto> Models { get; set; } = new();
}

/// <summary>
/// One installed Ollama model.
/// </summary>
public class OllamaModelInfoDto
{
    /// <summary>
    /// Gets or sets the model name with its tag, e.g. <c>llama3.2:latest</c>.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
