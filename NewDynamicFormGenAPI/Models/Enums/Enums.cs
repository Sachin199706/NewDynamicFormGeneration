namespace NewDynamicFormGenAPI.Models.Enums;

public static class FormStatus
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Archived = "Archived";
}

public static class RuleType
{
    public const string Required = "Required";
    public const string MinLength = "MinLength";
    public const string MaxLength = "MaxLength";
    public const string Regex = "Regex";
    public const string Range = "Range";
    public const string Email = "Email";
    public const string Date = "Date";
    public const string CrossField = "CrossField";
    public const string Custom = "Custom";

    /// <summary>Show/hide a control based on another control's value. UI-only — never fails validation.</summary>
    public const string Visibility = "Visibility";
}

public static class RuleSeverity
{
    public const string Error = "Error";
    public const string Warning = "Warning";
}

public static class DataSourceType
{
    public const string Static = "Static";
    public const string Api = "Api";
    public const string Sql = "Sql";
}
