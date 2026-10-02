namespace NewDynamicFormGenAPI.Models.Enums;

/// <summary>
/// Defines the valid status values for a form version's lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// Form versions transition through these states:
/// <list type="bullet">
///   <item><description><see cref="Draft"/> - Initially created, not yet published to end-users</description></item>
///   <item><description><see cref="Published"/> - Active and available for form submissions</description></item>
///   <item><description><see cref="Archived"/> - Superseded by newer versions, no longer used for new submissions</description></item>
/// </list>
/// </para>
/// </remarks>
public static class FormStatus
{
    /// <summary>
    /// Indicates a form version is unpublished and under development.
    /// </summary>
    public const string Draft = "Draft";

    /// <summary>
    /// Indicates a form version is active and available for end-users to submit.
    /// </summary>
    public const string Published = "Published";

    /// <summary>
    /// Indicates a form version is retired and no longer used for new submissions.
    /// </summary>
    public const string Archived = "Archived";
}

/// <summary>
/// Defines supported data format and validation types for form control inputs.
/// </summary>
/// <remarks>
/// <para>
/// These formats are used to configure validation rules that check if submitted control values
/// conform to the expected format. The application applies these formatting rules server-side
/// to ensure data quality and consistency.
/// </para>
/// </remarks>
public static class FormatType
{
    /// <summary>
    /// Specifies that a control value must be a valid email address format.
    /// </summary>
    /// <remarks>Email format validation typically follows RFC 5322 conventions.</remarks>
    public const string Email = "Email";

    /// <summary>
    /// Specifies that a control value must be a valid phone number format.
    /// </summary>
    public const string Phone = "Phone";

    /// <summary>
    /// Specifies that a control value must be a valid URL format.
    /// </summary>
    public const string Url = "URL";

    /// <summary>
    /// Specifies that a control value must be numeric (integer or decimal).
    /// </summary>
    public const string Number = "Number";

    /// <summary>
    /// Specifies that a control value must contain only alphanumeric characters (letters, numbers, spaces).
    /// </summary>
    public const string Alphanumeric = "Alphanumeric";
}

/// <summary>
/// Defines the severity levels for validation and business rule violations.
/// </summary>
/// <remarks>
/// <para>
/// Severity indicates the impact of a rule violation on form submission:
/// <list type="bullet">
///   <item><description><see cref="Error"/> - The submission is rejected and cannot proceed</description></item>
///   <item><description><see cref="Warning"/> - The violation is reported but does not block submission</description></item>
/// </list>
/// </para>
/// </remarks>
public static class RuleSeverity
{
    /// <summary>
    /// Indicates a rule violation that prevents form submission.
    /// </summary>
    public const string Error = "Error";

    /// <summary>
    /// Indicates a rule violation that is reported to the user but does not prevent submission.
    /// </summary>
    public const string Warning = "Warning";
}

/// <summary>
/// Defines the types of rules that can be applied to form controls and submissions.
/// </summary>
/// <remarks>
/// <para>
/// Rules are classified into two categories:
/// <list type="bullet">
///   <item><description>Validation rules - enforce data quality and format requirements on submitted values</description></item>
///   <item><description>Conditional rules - alter form state (visibility, enabled, required status) based on user input</description></item>
/// </list>
/// </para>
/// <para>
/// Rules are stored as configuration within <see cref="FormVersion.FormDefinitionJson"/>,
/// allowing rapid iteration without code changes.
/// </para>
/// </remarks>
public static class RuleType
{
    // ---- Validation rules (issue #10 taxonomy) ----

    /// <summary>
    /// Validates that a control has been filled with a value; the field is mandatory.
    /// </summary>
    /// <remarks>Empty strings, null values, and unselected options fail this validation.</remarks>
    public const string Required = "Required";

    /// <summary>
    /// Validates that a string value conforms to a minimum and/or maximum length constraint.
    /// </summary>
    /// <remarks>Typically configured with MinLength and MaxLength parameters.</remarks>
    public const string Length = "Length";

    /// <summary>
    /// Validates that a numeric or date value falls within a specified range (minimum and/or maximum).
    /// </summary>
    /// <remarks>Typically configured with Min and Max parameters.</remarks>
    public const string Range = "Range";

    /// <summary>
    /// Validates that a string value matches a regular expression pattern.
    /// </summary>
    /// <remarks>Enables flexible custom pattern matching for complex validation scenarios.</remarks>
    public const string Pattern = "Pattern";

    /// <summary>
    /// Validates that a value conforms to a predefined format (email, phone, URL, etc.).
    /// </summary>
    /// <remarks>References FormatType constants to specify which format to validate against.</remarks>
    public const string Format = "Format";

    /// <summary>
    /// Validates that a date value meets date-specific constraints (before, after, today, etc.).
    /// </summary>
    public const string Date = "Date";

    /// <summary>
    /// Validates file uploads, such as file type, size, and count constraints.
    /// </summary>
    public const string File = "File";

    /// <summary>
    /// Validates relationships between multiple controls, such as comparing two field values.
    /// </summary>
    /// <remarks>Example: Password confirmation matching password field, or date range validation.</remarks>
    public const string CompareFields = "CompareFields";

    /// <summary>
    /// Indicates a custom rule executed by application-specific logic beyond built-in validators.
    /// </summary>
    public const string Custom = "Custom";

    // ---- Conditional rules: these change form state rather than failing a submission ----

    /// <summary>
    /// Conditionally shows or hides a control based on the value of another control.
    /// </summary>
    /// <remarks>
    /// Execution: When the trigger condition is met, the referenced control becomes visible or hidden.
    /// See <see cref="ConditionalAction"/> for specific show/hide effects.
    /// </remarks>
    public const string Visibility = "Visibility";

    /// <summary>
    /// Conditionally enables or disables a control based on the value of another control.
    /// </summary>
    /// <remarks>
    /// Execution: When the trigger condition is met, the referenced control is enabled or disabled,
    /// preventing user interaction but keeping it visible.
    /// </remarks>
    public const string EnableDisable = "EnableDisable";

    /// <summary>
    /// Conditionally makes a control required or optional based on the value of another control.
    /// </summary>
    /// <remarks>
    /// Execution: When the trigger condition is met, the referenced control's required status
    /// is changed, dynamically altering validation requirements.
    /// </remarks>
    public const string RequiredOptional = "RequiredOptional";

    /// <summary>
    /// Legacy rule names used in earlier versions of the application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These constants represent rule names used before issue #10 refactoring.
    /// Rules are stored as strings inside <see cref="FormVersion.FormDefinitionJson"/>,
    /// so existing forms may still contain these legacy rule type values.
    /// </para>
    /// <para>
    /// Legacy rule types are mapped to their current equivalent when read from the database.
    /// Never written for newly created rules; instead, use the current <see cref="RuleType"/> constants.
    /// </para>
    /// </remarks>
    public static class Legacy
    {
        /// <summary>
        /// Legacy name for minimum length validation. Mapped to <see cref="RuleType.Length"/>.
        /// </summary>
        public const string MinLength = "MinLength";

        /// <summary>
        /// Legacy name for maximum length validation. Mapped to <see cref="RuleType.Length"/>.
        /// </summary>
        public const string MaxLength = "MaxLength";

        /// <summary>
        /// Legacy name for regex pattern validation. Mapped to <see cref="RuleType.Pattern"/>.
        /// </summary>
        public const string Regex = "Regex";

        /// <summary>
        /// Legacy name for email format validation. Mapped to <see cref="RuleType.Format"/>.
        /// </summary>
        public const string Email = "Email";

        /// <summary>
        /// Legacy name for cross-field validation. Mapped to <see cref="RuleType.CompareFields"/>.
        /// </summary>
        public const string CrossField = "CrossField";
    }
}

/// <summary>
/// Defines the actions that conditional rules apply when their trigger condition is satisfied.
/// </summary>
/// <remarks>
/// <para>
/// Conditional rules alter form state dynamically based on control values. Each action has an inverse
/// to enable rule authors to express conditions in the most natural way for the business logic.
/// </para>
/// <para>
/// Examples:
/// <list type="bullet">
///   <item><description>"If profession = 'Doctor', then Show medical_license_field" (Hide is inverse)</description></item>
///   <item><description>"If age < 18, then Disable alcohol_purchase_option" (Enable is inverse)</description></item>
///   <item><description>"If shipment_method = 'International', then Required customs_paperwork" (Optional is inverse)</description></item>
/// </list>
/// </para>
/// </remarks>
public static class ConditionalAction
{
    /// <summary>
    /// Makes a control visible to the user, revealing it in the form UI.
    /// </summary>
    /// <remarks>Inverse of <see cref="Hide"/>.</remarks>
    public const string Show = "Show";

    /// <summary>
    /// Makes a control invisible to the user, removing it from the form UI.
    /// </summary>
    /// <remarks>Inverse of <see cref="Show"/>. Hidden controls are not submitted and do not validate.</remarks>
    public const string Hide = "Hide";

    /// <summary>
    /// Enables a control, allowing the user to interact with and modify its value.
    /// </summary>
    /// <remarks>Inverse of <see cref="Disable"/>. Enabling a hidden control has no visual effect until it is shown.</remarks>
    public const string Enable = "Enable";

    /// <summary>
    /// Disables a control, preventing the user from interacting with it while keeping it visible.
    /// </summary>
    /// <remarks>Inverse of <see cref="Enable"/>. Disabled controls may display their current value but cannot be modified.</remarks>
    public const string Disable = "Disable";

    /// <summary>
    /// Makes a control required, requiring the user to provide a value before form submission.
    /// </summary>
    /// <remarks>Inverse of <see cref="Optional"/>.</remarks>
    public const string Required = "Required";

    /// <summary>
    /// Makes a control optional, allowing the user to skip providing a value.
    /// </summary>
    /// <remarks>Inverse of <see cref="Required"/>.</remarks>
    public const string Optional = "Optional";
}
