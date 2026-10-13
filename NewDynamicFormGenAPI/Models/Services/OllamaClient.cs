using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NewDynamicFormGenAPI.Models.Common;
using NewDynamicFormGenAPI.Models.DTOs.Ollama;
using NewDynamicFormGenAPI.Models.Interfaces;

namespace NewDynamicFormGenAPI.Models.Services;

/// <summary>
/// Calls the local Ollama AI service over HTTP.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a typed <see cref="HttpClient"/> in <c>Program.cs</c>. The base address and
/// the timeout come from <see cref="OllamaOptions"/>.
/// </para>
/// <para>
/// Every failure is turned into an <see cref="OllamaServiceException"/> with a message
/// that can be shown to the end user.
/// </para>
/// </remarks>
public class OllamaClient : IOllamaClient
{
    private const string ChatEndpoint = "api/chat";
    private const string TagsEndpoint = "api/tags";
    private const string UserRole = "user";
    private const int StatusCheckTimeoutSeconds = 5;

    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _ollamaOptions;
    private readonly ILogger<OllamaClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured for the Ollama server.</param>
    /// <param name="ollamaOptions">The Ollama settings from <c>appsettings.json</c>.</param>
    /// <param name="logger">The logger.</param>
    public OllamaClient(HttpClient httpClient, IOptions<OllamaOptions> ollamaOptions, ILogger<OllamaClient> logger)
    {
        _httpClient = httpClient;
        _ollamaOptions = ollamaOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GenerateJsonAsync(string aStrPrompt, object aObjJsonSchema, CancellationToken aObjCancellationToken = default)
    {
        var lobjRequest = new OllamaChatRequestDto
        {
            Model = _ollamaOptions.ModelName,
            Messages = new List<OllamaChatMessageDto>
            {
                new() { Role = UserRole, Content = aStrPrompt }
            },
            Format = aObjJsonSchema,
            Stream = false,
            Options = new OllamaModelOptionsDto { Temperature = 0 }
        };

        var lobjStopwatch = Stopwatch.StartNew();
        HttpResponseMessage lobjResponse;

        try
        {
            lobjResponse = await _httpClient.PostAsJsonAsync(ChatEndpoint, lobjRequest, aObjCancellationToken);
        }
        catch (TaskCanceledException ex) when (!aObjCancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Ollama did not answer within {TimeoutSeconds} seconds", _ollamaOptions.TimeoutSeconds);
            throw new OllamaServiceException(
                $"The AI service did not answer within {_ollamaOptions.TimeoutSeconds} seconds. Please try again.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Ollama is not reachable at {BaseUrl}", _ollamaOptions.BaseUrl);
            throw new OllamaServiceException("The AI service is not available. Check that Ollama is running.", ex);
        }

        using (lobjResponse)
        {
            var lobjAnswer = await ReadChatResponseAsync(lobjResponse, aObjCancellationToken);

            if (!lobjResponse.IsSuccessStatusCode)
            {
                var lstrError = lobjAnswer?.Error ?? $"HTTP {(int)lobjResponse.StatusCode}";
                _logger.LogWarning("Ollama returned {StatusCode}: {Error}", (int)lobjResponse.StatusCode, lstrError);

                throw new OllamaServiceException(lobjResponse.StatusCode == HttpStatusCode.NotFound
                    ? $"The AI model '{_ollamaOptions.ModelName}' is not installed in Ollama."
                    : "The AI service returned an error. Please try again.");
            }

            var lstrContent = lobjAnswer?.Message?.Content;
            if (string.IsNullOrWhiteSpace(lstrContent))
            {
                throw new OllamaServiceException("The AI service returned an empty answer. Please try again.");
            }

            _logger.LogInformation("Ollama model {ModelName} answered in {ElapsedMs} ms",
                _ollamaOptions.ModelName, lobjStopwatch.ElapsedMilliseconds);

            return lstrContent;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsModelAvailableAsync(CancellationToken aObjCancellationToken = default)
    {
        using var lobjTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(aObjCancellationToken);
        lobjTimeoutSource.CancelAfter(TimeSpan.FromSeconds(StatusCheckTimeoutSeconds));

        try
        {
            var lobjTags = await _httpClient.GetFromJsonAsync<OllamaTagsResponseDto>(TagsEndpoint, lobjTimeoutSource.Token);
            return lobjTags?.Models.Any(m => IsSameModel(m.Name, _ollamaOptions.ModelName)) == true;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                   || (ex is TaskCanceledException && !aObjCancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Ollama status check failed at {BaseUrl}", _ollamaOptions.BaseUrl);
            return false;
        }
    }

    /// <summary>
    /// Reads the Ollama chat response body. Returns <c>null</c> when the body is not valid JSON.
    /// </summary>
    /// <param name="aObjResponse">The HTTP response from Ollama.</param>
    /// <param name="aObjCancellationToken">Cancels the read.</param>
    /// <returns>The parsed response, or <c>null</c> if it cannot be read.</returns>
    private async Task<OllamaChatResponseDto?> ReadChatResponseAsync(HttpResponseMessage aObjResponse, CancellationToken aObjCancellationToken)
    {
        try
        {
            return await aObjResponse.Content.ReadFromJsonAsync<OllamaChatResponseDto>(aObjCancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Ollama returned a response that is not valid JSON");
            return null;
        }
    }

    /// <summary>
    /// Checks whether an installed model name matches the configured model name.
    /// </summary>
    /// <param name="aStrInstalledName">The installed name, e.g. <c>llama3.2:latest</c>.</param>
    /// <param name="aStrConfiguredName">The configured name, e.g. <c>llama3.2</c> or <c>llama3.2:3b</c>.</param>
    /// <returns><c>true</c> if they refer to the same model.</returns>
    /// <remarks>A configured name without a tag means the <c>latest</c> tag, the same rule Ollama uses.</remarks>
    private static bool IsSameModel(string aStrInstalledName, string aStrConfiguredName)
    {
        if (string.Equals(aStrInstalledName, aStrConfiguredName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !aStrConfiguredName.Contains(':')
            && string.Equals(aStrInstalledName, $"{aStrConfiguredName}:latest", StringComparison.OrdinalIgnoreCase);
    }
}
