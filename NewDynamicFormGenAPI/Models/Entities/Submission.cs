namespace NewDynamicFormGenAPI.Models.Entities;

/// <summary>
/// Represents a single submission of a form filled out by a user.
/// </summary>
/// <remarks>
/// <para>
/// FormSubmission captures a completed form submission, including the submitted data, timing, and submission metadata.
/// Multiple submissions can be received for the same form, potentially from different versions of the form.
/// </para>
/// <para>
/// Submissions are linked to both the parent Form and FormVersion, allowing historical tracking of which version
/// was submitted and tracking of form versioning impact on submission history.
/// </para>
/// </remarks>
public class FormSubmission
{
    /// <summary>
    /// Gets or sets the unique identifier for this submission.
    /// </summary>
    public int SubmissionId { get; set; }

    /// <summary>
    /// Gets or sets the parent form identifier.
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the parent <see cref="Form"/>.
    /// </summary>
    public Form Form { get; set; } = null!;

    /// <summary>
    /// Gets or sets the form version identifier used when this submission was submitted.
    /// </summary>
    /// <remarks>
    /// This value indicates which form version was current when the user submitted data,
    /// enabling tracking of submissions against specific form revisions.
    /// </remarks>
    public int FormVersionId { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the submission was received in UTC.
    /// </summary>
    public DateTime SubmittedOn { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the complete submitted form data as JSON.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This JSON document is a raw key/value snapshot of the submitted data, structured to match
    /// the form controls and their unique identifiers. It serves as:
    /// <list type="bullet">
    ///   <item><description>A snapshot for quick re-display or export without transformation</description></item>
    ///   <item><description>A historical record preserving submitted values as they were captured</description></item>
    ///   <item><description>A source for validation and business rule application</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The structure and format of this JSON should match the form's control structure
    /// as defined in <see cref="FormVersion.FormDefinitionJson"/>.
    /// </para>
    /// </remarks>
    public string JsonData { get; set; } = "{}";

    /// <summary>
    /// Gets or sets a value indicating whether this submission has been read or reviewed.
    /// </summary>
    /// <remarks>
    /// This flag can be used to track which submissions have been reviewed by internal users
    /// and provide notifications for new unreviewed submissions.
    /// </remarks>
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// Gets or sets a unique code identifier for this submission.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The submission code is generated in the format: <c>{FormCode}-v{VersionNo}-{SubmissionId}</c>
    /// This code is built after the submission is inserted into the database (after SubmissionId is generated),
    /// providing a human-readable, globally-unique reference for the submission that includes context about
    /// which form and version it belongs to.
    /// </para>
    /// <para>
    /// Example: "EmployeeOnboarding-v2-1045"
    /// </para>
    /// </remarks>
    public string SubmissionCode { get; set; } = null!;
}

