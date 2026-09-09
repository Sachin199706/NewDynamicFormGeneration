import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CreateFormRuleRequest, FormatKind, FormRule, RuleType } from '../../core/models/rule.model';
import { FormControlDef } from '../../core/models/form.model';
import { RuleService } from '../../core/services/rule';



interface RuleTypeMeta {
  type: RuleType;
  label: string;
  icon: string;
  description: string;
}

@Component({
  selector: 'app-validation-rules-tab',
  imports: [CommonModule, FormsModule],
  templateUrl: './validation-rules-tab.html',
})
export class ValidationRulesTab {

  @Input() inumVersionId!: number;
  @Input() iarrControls: FormControlDef[] = [];
  @Input() iarrRules: FormRule[] = [];
  @Output() ichanged = new EventEmitter<void>();
  @Output() igoToConditional = new EventEmitter<void>();

  iboolFormOpen = false;
  /** Set while editing; null while adding. Drives Add Rule vs Save Changes. */
  istrEditingRuleId: string | null = null;

  iarrRuleTypes: RuleTypeMeta[] = [
    { type: 'Required', label: 'Required', icon: 'bi-check-circle', description: 'Make sure the field has a value.' },
    { type: 'Length', label: 'Length', icon: 'bi-distribute-vertical', description: 'Limit how many characters can be entered.' },
    { type: 'Range', label: 'Range', icon: 'bi-graph-up', description: 'Limit the numeric value that can be entered.' },
    { type: 'Pattern', label: 'Pattern', icon: 'bi-braces', description: 'Match the value against a regular expression.' },
    { type: 'Format', label: 'Format', icon: 'bi-card-text', description: 'Check the value looks like an email, phone number, URL and so on.' },
    { type: 'Date', label: 'Date', icon: 'bi-calendar', description: 'Restrict the date relative to today.' },
    { type: 'File', label: 'File', icon: 'bi-file-earmark', description: 'Restrict which file types and sizes can be uploaded.' },
    { type: 'CompareFields', label: 'Compare Fields', icon: 'bi-arrow-left-right', description: 'Compare this field against another field on the form.' }
  ];

  iarrExamples = [
    { icon: 'bi-asterisk', title: 'Required', sub: 'Field is mandatory' },
    { icon: 'bi-envelope', title: 'Email', sub: 'Valid email address' },
    { icon: 'bi-type', title: 'Min Length', sub: 'At least 8 characters' },
    { icon: 'bi-arrows-expand', title: 'Range', sub: 'Between min and max' },
    { icon: 'bi-braces', title: 'Pattern', sub: 'Match a specific format' },
    { icon: 'bi-arrow-left-right', title: 'Compare', sub: 'Match another field' }
  ];

  iarrFormatKinds: FormatKind[] = ['Email', 'Phone', 'URL', 'Number', 'Alphanumeric'];

  istrControlKey = '';
  istrRuleType: RuleType = 'Required';
  istrErrorMessage = '';
  istrSeverity: 'Error' | 'Warning' = 'Error';

  inumLengthMin?: number;
  inumLengthMax?: number;
  inumRangeMin?: number;
  inumRangeMax?: number;
  istrPattern = '';
  istrFormatKind: FormatKind = 'Email';
  istrFileExtensions = '';
  inumFileMaxSizeKb?: number;
  istrDateOperator: '<=Today' | '>=Today' | '<Today' | '>Today' = '<=Today';
  istrCompareFieldKey = '';
  istrCompareFieldOperator: '==' | '!=' | '<' | '<=' | '>' | '>=' = '==';

  constructor(private iobjRuleService: RuleService) { }

  get selectedMeta(): RuleTypeMeta | undefined {
    return this.iarrRuleTypes.find(t => t.type === this.istrRuleType);
  }

  controlLabel(aStrControlKey: string): string {
    return this.iarrControls.find(c => c.controlKey === aStrControlKey)?.label ?? aStrControlKey;
  }

  /** Short badge text for the table — the rule type plus whatever detail identifies it. */
  ruleSummary(aObjRule: FormRule): string {
    const lobjDetails = this.parseDetails(aObjRule.ruleDetailsJson);

    switch (aObjRule.ruleType) {
      case 'Length':
      case 'MinLength':
      case 'MaxLength': {
        if (lobjDetails.min != null && lobjDetails.max != null) return `Length (${lobjDetails.min} - ${lobjDetails.max})`;
        if (lobjDetails.min != null) return `Min Length (${lobjDetails.min})`;
        if (lobjDetails.max != null) return `Max Length (${lobjDetails.max})`;
        return 'Length';
      }
      case 'Range': return `Range (${lobjDetails.min ?? ''} - ${lobjDetails.max ?? ''})`;
      case 'Pattern':
      case 'Regex': return 'Pattern';
      case 'Format': return lobjDetails.format ?? 'Format';
      case 'Email': return 'Email';
      case 'Date': return `Date (${lobjDetails.operator ?? ''})`;
      case 'File': return 'File';
      case 'CompareFields':
      case 'CrossField': return `Match Field (${this.controlLabel(lobjDetails.compareControlKey ?? '')})`;
      default: return aObjRule.ruleType;
    }
  }

  private parseDetails(aStrJson?: string): any {
    if (!aStrJson) return {};
    try { return JSON.parse(aStrJson); } catch { return {}; }
  }

  openAdd(): void {
    this.resetDraft();
    this.istrEditingRuleId = null;
    this.iboolFormOpen = true;
  }

  /** Loads an existing rule back into the draft so the same card serves add and edit. */
  openEdit(aObjRule: FormRule): void {
    this.resetDraft();
    this.istrEditingRuleId = aObjRule.ruleId;
    this.istrControlKey = aObjRule.controlKey;
    this.istrErrorMessage = aObjRule.errorMessage;
    this.istrSeverity = aObjRule.severity;

    const d = this.parseDetails(aObjRule.ruleDetailsJson);

    // Legacy names map onto their current equivalent so old rules open in the right editor.
    switch (aObjRule.ruleType) {
      case 'MinLength':
      case 'MaxLength':
      case 'Length':
        this.istrRuleType = 'Length';
        this.inumLengthMin = d.min;
        this.inumLengthMax = d.max;
        break;
      case 'Range':
        this.istrRuleType = 'Range';
        this.inumRangeMin = d.min;
        this.inumRangeMax = d.max;
        break;
      case 'Regex':
      case 'Pattern':
        this.istrRuleType = 'Pattern';
        this.istrPattern = d.pattern ?? '';
        break;
      case 'Email':
      case 'Format':
        this.istrRuleType = 'Format';
        this.istrFormatKind = d.format ?? 'Email';
        break;
      case 'Date':
        this.istrRuleType = 'Date';
        this.istrDateOperator = d.operator ?? '<=Today';
        break;
      case 'File':
        this.istrRuleType = 'File';
        this.istrFileExtensions = (d.allowedExtensions ?? []).join(', ');
        this.inumFileMaxSizeKb = d.maxSizeKb;
        break;
      case 'CrossField':
      case 'CompareFields':
        this.istrRuleType = 'CompareFields';
        this.istrCompareFieldKey = d.compareControlKey ?? '';
        this.istrCompareFieldOperator = d.operator ?? '==';
        break;
      default:
        this.istrRuleType = 'Required';
    }

    this.iboolFormOpen = true;
  }

  cancel(): void {
    this.iboolFormOpen = false;
    this.istrEditingRuleId = null;
  }

  private buildDetailsJson(): string | undefined {
    switch (this.istrRuleType) {
      case 'Length': return JSON.stringify({ min: this.inumLengthMin, max: this.inumLengthMax });
      case 'Range': return JSON.stringify({ min: this.inumRangeMin, max: this.inumRangeMax });
      case 'Pattern': return JSON.stringify({ pattern: this.istrPattern });
      case 'Format': return JSON.stringify({ format: this.istrFormatKind });
      case 'File': return JSON.stringify({
        allowedExtensions: this.parseExtensions(this.istrFileExtensions),
        maxSizeKb: this.inumFileMaxSizeKb
      });
      case 'Date': return JSON.stringify({ operator: this.istrDateOperator });
      case 'CompareFields': return JSON.stringify({
        compareControlKey: this.istrCompareFieldKey,
        operator: this.istrCompareFieldOperator
      });
      default: return undefined;
    }
  }

  /** "pdf, .PNG , jpg" becomes ['pdf','png','jpg'] — the server compares without the dot. */
  private parseExtensions(aStrRaw: string): string[] {
    return aStrRaw.split(',').map(e => e.trim().replace(/^\./, '').toLowerCase()).filter(e => e.length > 0);
  }

  get canSave(): boolean {
    if (!this.istrControlKey || !this.istrErrorMessage) return false;

    // A File rule with neither bound set would silently allow everything.
    if (this.istrRuleType === 'File'
      && this.parseExtensions(this.istrFileExtensions).length === 0
      && this.inumFileMaxSizeKb == null) return false;

    return true;
  }

  save(): void {
    if (!this.canSave) return;

    const lobjDto: CreateFormRuleRequest = {
      controlKey: this.istrControlKey,
      ruleType: this.istrRuleType,
      ruleDetailsJson: this.buildDetailsJson(),
      errorMessage: this.istrErrorMessage,
      severity: this.istrSeverity,
      displayOrder: this.iarrRules.length
    };

    const lobjRequest$ = this.istrEditingRuleId
      ? this.iobjRuleService.updateRule(this.inumVersionId, this.istrEditingRuleId, lobjDto)
      : this.iobjRuleService.addRule(this.inumVersionId, lobjDto);

    lobjRequest$.subscribe(() => {
      this.iboolFormOpen = false;
      this.istrEditingRuleId = null;
      this.ichanged.emit();
    });
  }

  deleteRule(aObjRule: FormRule): void {
    this.iobjRuleService.deleteRule(this.inumVersionId, aObjRule.ruleId)
      .subscribe(() => this.ichanged.emit());
  }

  private resetDraft(): void {
    this.istrControlKey = '';
    this.istrRuleType = 'Required';
    this.istrErrorMessage = '';
    this.istrSeverity = 'Error';
    this.inumLengthMin = undefined;
    this.inumLengthMax = undefined;
    this.inumRangeMin = undefined;
    this.inumRangeMax = undefined;
    this.istrPattern = '';
    this.istrFormatKind = 'Email';
    this.istrFileExtensions = '';
    this.inumFileMaxSizeKb = undefined;
    this.istrDateOperator = '<=Today';
    this.istrCompareFieldKey = '';
    this.istrCompareFieldOperator = '==';
  }
}