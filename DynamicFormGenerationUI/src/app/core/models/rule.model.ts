export type RuleSeverity = 'Error' | 'Warning';

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
export interface MinMaxLengthDetails { min?: number; max?: number; }
export interface PatternDetails { pattern: string; }
export interface RangeDetails { min?: number; max?: number; }
export interface DateRuleDetails { operator: '<=Today' | '>=Today' | '<Today' | '>Today'; }
export interface CompareFieldsDetails {
    compareControlKey: string;
    operator: '==' | '!=' | '<' | '<=' | '>' | '>=';
}

/**
 * Visibility rule: the target control is the rule's ControlKey itself.
 * "Show" the target when the trigger condition is true; "Hide" flips the sense.
 */
export interface VisibilityDetails {
    triggerControlKey: string;
    operator: '==' | '!=' | '<' | '<=' | '>' | '>=';
    triggerValue: string;
    action: 'Show' | 'Hide';
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
    | 'Visibility'
    // Names used before issue #10. Still present in saved forms, so they are read
    // and normalised — never written for new rules.
    | 'MinLength'
    | 'MaxLength'
    | 'Regex'
    | 'Email'
    | 'CrossField';

export type FormatKind = 'Email' | 'Phone' | 'URL' | 'Number' | 'Alphanumeric';

/** One rule covering both bounds. A missing bound means unbounded. */
export interface LengthDetails {
    min?: number;
    max?: number;
}

export interface FormatDetails {
    format: FormatKind;
}

export interface FileDetails {
    /** Extensions without the dot, e.g. ['pdf','png']. Empty means any type. */
    allowedExtensions?: string[];
    maxSizeKb?: number;
}