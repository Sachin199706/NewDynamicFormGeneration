
namespace NewDynamicFormGenAPI.Models.Entities;

/// <summary>
/// Represents a historical record of when a form version was published.
/// </summary>
/// <remarks>
/// <para>
/// FormPublishHistory tracks the publication workflow and governance of form versions.
/// Each record captures a point in time when a form version transitioned from draft to published,
/// enabling audit trails, compliance tracking, and historical analysis of form changes.
/// </para>
/// <para>
/// This history can be used to:
/// <list type="bullet">
///   <item><description>Answer "when was this form version published?"</description></item>
///   <item><description>Determine which form version was active on a specific date</description></item>
///   <item><description>Create audit reports for compliance and regulatory requirements</description></item>
///   <item><description>Support rollback or version recovery decisions</description></item>
/// </list>
/// </para>
/// </remarks>
public class FormPublishHistory
{
    /// <summary>
    /// Gets or sets the unique identifier for this publish history record.
    /// </summary>
    public int PublishHistoryId { get; set; }

    /// <summary>
    /// Gets or sets the parent form identifier.
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// Gets or sets the form version identifier for the version that was published.
    /// </summary>
    public int FormVersionId { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this form version was published in UTC.
    /// </summary>
    public DateTime PublishedOn { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets an optional note or reason for the publication.
    /// </summary>
    /// <remarks>
    /// This field can contain information such as:
    /// <list type="bullet">
    ///   <item><description>The reason for publishing (e.g., "Bug fixes", "New fields added")</description></item>
    ///   <item><description>The user or process that initiated the publication</description></item>
    ///   <item><description>Related ticket numbers or change requests</description></item>
    /// </list>
    /// </remarks>
    public string? Notes { get; set; }
}