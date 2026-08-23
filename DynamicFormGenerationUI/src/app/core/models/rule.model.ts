export type RuleType =
    | 'Required' | 'MinLength' | 'MaxLength' | 'Regex' | 'Range'
    | 'Email' | 'Date' | 'CrossField' | 'Custom' | 'Visibility';

export type RuleSeverity = 'Error' | 'Warning';



export interface FormRule {
    ruleId: number;
    formVersionId: number;
    controlId: number;
    controlKey: string;
    ruleType: RuleType;
    ruleDetailsJson?: string;
    errorMessage: string;
    severity: RuleSeverity;
    displayOrder: number;
    isActive: boolean;
}

export interface CreateFormRuleRequest {
    controlId: number;
    ruleType: RuleType;
    ruleDetailsJson?: string;
    errorMessage: string;
    severity: RuleSeverity;
    displayOrder: number;
}

/** RuleDetailsJson shapes per RuleType — kept here so the rule builder UI can build valid JSON. */
export interface MinMaxLengthDetails { min?: number; max?: number; }
export interface RegexDetails { pattern: string; }
export interface RangeDetails { min?: number; max?: number; }
export interface DateRuleDetails { operator: '<=Today' | '>=Today' | '<Today' | '>Today'; }
export interface CrossFieldDetails {
    compareControlKey: string;
    operator: '==' | '!=' | '<' | '<=' | '>' | '>=';
}

/**
 * Visibility rule: the target control is the rule's ControlId itself.
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

export interface SubmissionListItem {
    submissionId: number;
    formId: number;
    submittedOn: string;
}

export interface SubmissionDetail {
    submissionId: number;
    formId: number;
    formName: string;
    versionNo: number;
    submittedOn: string;
    values: Record<string, any>;
}
