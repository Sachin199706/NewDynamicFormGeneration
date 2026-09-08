/**
 * Validation rules fail a submission; conditional rules change form state instead.
 * Kept in lockstep with RuleType in the C# Enums.cs — adding one here means adding
 * it there too, or client and server will disagree.
 */
export type RuleType =
    | 'Required'
    | 'Length'
    | 'Range'
    | 'Pattern'
    | 'Format'
    | 'Date'
    | 'File'
    | 'CompareFields'
    | 'Custom'
    // Conditional rules change form state rather than failing a submission.
    | 'Visibility'
    | 'EnableDisable'
    | 'RequiredOptional'
    // Names used before issue #10. Still present in saved forms, so they are read
    // and normalised — never written for new rules.
    | 'MinLength'
    | 'MaxLength'
    | 'Regex'
    | 'Email'
    | 'CrossField';

export type RuleSeverity = 'Error' | 'Warning';

export type FormatKind = 'Email' | 'Phone' | 'URL' | 'Number' | 'Alphanumeric';

export type ConditionalAction =
    | 'Show' | 'Hide'
    | 'Enable' | 'Disable'
    | 'Required' | 'Optional';

export interface FormRule {
    controlKey: string;
    ruleType: RuleType;
    ruleDetailsJson?: string;
    errorMessage: string;
    severity: RuleSeverity;
    displayOrder: number;
    isActive: boolean;
}

export interface CreateFormRuleRequest {
    controlKey: string;
    ruleType: RuleType;
    ruleDetailsJson?: string;
    errorMessage: string;
    severity: RuleSeverity;
    displayOrder: number;
}

/** RuleDetailsJson shapes per RuleType — kept here so the rule builder UI can build valid JSON. */

/** One rule covering both bounds. A missing bound means unbounded — which is also how
 *  legacy MinLength/MaxLength rules land here, each having set only its own half. */
export interface LengthDetails { min?: number; max?: number; }
export interface RangeDetails { min?: number; max?: number; }
export interface PatternDetails { pattern: string; }
export interface FormatDetails { format: FormatKind; }
export interface DateRuleDetails { operator: '<=Today' | '>=Today' | '<Today' | '>Today'; }

export interface FileDetails {
    /** Extensions without the dot, e.g. ['pdf','png']. Empty or absent means any type. */
    allowedExtensions?: string[];
    maxSizeKb?: number;
}

export interface CompareFieldsDetails {
    compareControlKey: string;
    operator: '==' | '!=' | '<' | '<=' | '>' | '>=';
}

/**
 * All three conditional rules share this shape — the target control is the rule's own
 * controlKey, and `action` decides which effect the trigger condition drives.
 * Show/Hide, Enable/Disable and Required/Optional differ only in that field.
 */
export interface ConditionalDetails {
    triggerControlKey: string;
    operator: '==' | '!=' | '<' | '<=' | '>' | '>=';
    triggerValue: string;
    action: ConditionalAction;
}

/** Kept as an alias so existing references to VisibilityDetails still compile. */
export type VisibilityDetails = ConditionalDetails;

/**
 * The computed state of one control after all conditional rules have run.
 * Every control gets one of these; controls with no conditional rules keep their defaults.
 */
export interface ControlEffects {
    visible: boolean;
    enabled: boolean;
    required: boolean;
}

export interface SubmitFormRequest {
    formId: number;
    formVersionId: number;
    values: Record<string, any>;
}

export interface SubmissionDetail {
    submissionId: number;
    formId: number;
    formName: string;
    versionNo: number;
    submittedOn: string;
    values: Record<string, any>;
}

export interface SubmissionOverviewItem {
    submissionId: number;
    submissionCode: string;
    formId: number;
    formName: string;
    versionNo: number;
    submittedOn: string;
    isRead: boolean;
}

export interface SubmissionStats {
    totalSubmissions: number;
    unreadSubmissions: number;
    readSubmissions: number;
}

export interface SubmissionFilter {
    search?: string;
    formId?: number | null;
    isRead?: boolean | null;
    fromDate?: string;
    toDate?: string;
    page: number;
    pageSize: number;
}