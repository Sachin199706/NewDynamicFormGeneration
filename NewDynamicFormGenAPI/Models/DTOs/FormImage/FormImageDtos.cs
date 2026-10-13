namespace NewDynamicFormGenAPI.Models.DTOs.FormImage;

/// <summary>
/// Request to find the control type of field labels read from a form image.
/// </summary>
/// <remarks>
/// <para>
/// The Angular page reads the image with OCR and applies the fixed rules itself
/// (required mark, radio and tick-box options, file upload, layout). Only the labels
/// whose type the rules could not decide are sent here.
/// </para>
/// </remarks>
public class ClassifyFieldLabelsRequestDto
{
    /// <summary>
    /// Gets or sets the field labels, in the order they appear on the form.
    /// </summary>
    public List<string> Labels { get; set; } = new();
}

/// <summary>
/// The control type the AI chose for one field label.
/// </summary>
public class FieldLabelTypeDto
{
    /// <summary>
    /// Gets or sets the 1-based position of the label in the request.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the label, as it was sent to the AI.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the control type returned by the AI, or <c>null</c> when the AI gave none.
    /// </summary>
    /// <remarks>
    /// Expected values: <c>TextBox</c>, <c>Number</c>, <c>Date</c>, <c>Dropdown</c>, <c>File</c>,
    /// <c>Label</c> or <c>Ignore</c>. The Angular side checks the value and uses <c>TextBox</c>
    /// when it is missing or unknown.
    /// </remarks>
    public string? ControlType { get; set; }
}

/// <summary>
/// Result of classifying field labels: one item for every label in the request, in the same order.
/// </summary>
public class ClassifyFieldLabelsResultDto
{
    /// <summary>
    /// Gets or sets the label types.
    /// </summary>
    public List<FieldLabelTypeDto> Fields { get; set; } = new();
}

/// <summary>
/// Tells the Angular page whether the AI service can be used.
/// </summary>
public class FormImageAiStatusDto
{
    /// <summary>
    /// Gets or sets whether Ollama is running and the configured model is installed.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Gets or sets the configured model name, e.g. <c>llama3.2</c>.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;
}

/// <summary>
/// The JSON answer the model returns. Used only to read the model output.
/// </summary>
public class AiFieldTypesAnswerDto
{
    /// <summary>
    /// Gets or sets the type for each numbered label.
    /// </summary>
    public List<AiFieldTypeItemDto> Fields { get; set; } = new();
}

/// <summary>
/// One item of the model answer.
/// </summary>
public class AiFieldTypeItemDto
{
    /// <summary>
    /// Gets or sets the 1-based label number the model is answering for.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the control type the model chose.
    /// </summary>
    public string? ControlType { get; set; }
}
