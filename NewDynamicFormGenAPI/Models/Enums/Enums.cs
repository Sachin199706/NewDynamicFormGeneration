namespace NewDynamicFormGenAPI.Models.Enums;

public static class FormStatus
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Archived = "Archived";
}
public static class FormatType
{
    public const string Email = "Email";
    public const string Phone = "Phone";
    public const string Url = "URL";
    public const string Number = "Number";
    public const string Alphanumeric = "Alphanumeric";
}

public static class RuleSeverity
{
    public const string Error = "Error";
    public const string Warning = "Warning";
}
public static class RuleType
{
    // ---- Validation rules (issue #10 taxonomy) ----
    public const string Required = "Required";
    public const string Length = "Length";
    public const string Range = "Range";
    public const string Pattern = "Pattern";
    public const string Format = "Format";
    public const string Date = "Date";
    public const string File = "File";
    public const string CompareFields = "CompareFields";

    public const string Custom = "Custom";

    // ---- Conditional rules: these change form state rather than failing a submission ----

    /// <summary>Show/hide a control based on another control's value.</summary>
    public const string Visibility = "Visibility";

    /// <summary>Enable/disable a control based on another control's value.</summary>
    public const string EnableDisable = "EnableDisable";

    /// <summary>Make a control required/optional based on another control's value.</summary>
    public const string RequiredOptional = "RequiredOptional";

    /// <summary>
    /// Rule names used before issue #10. Rules are stored as strings inside
    /// FormDefinitionJson, so existing forms still carry these — they are mapped
    /// to their current equivalent on read. Never written for new rules.
    /// </summary>
    public static class Legacy
    {
        public const string MinLength = "MinLength";
        public const string MaxLength = "MaxLength";
        public const string Regex = "Regex";
        public const string Email = "Email";
        public const string CrossField = "CrossField";
    }
}

/// <summary>
/// What a conditional rule does when its trigger condition is met. Each pair is
/// one effect and its inverse, so the rule author can express the condition
/// whichever way round reads more naturally.
/// </summary>
public static class ConditionalAction
{
    public const string Show = "Show";
    public const string Hide = "Hide";
    public const string Enable = "Enable";
    public const string Disable = "Disable";
    public const string Required = "Required";
    public const string Optional = "Optional";
}