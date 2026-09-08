import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormControlDef } from '../../core/models/form.model';
import { CreateFormRuleRequest, FormatKind, FormRule, RuleType } from '../../core/models/rule.model';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormService } from '../../core/services/form';
import { RuleService } from '../../core/services/rule';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-form-rules',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './form-rules.html',
  styleUrl: './form-rules.scss',
})
export class FormRules implements OnInit {

  inumFormId!: number;
  inumVersionId!: number;
  iarrControls: FormControlDef[] = [];
  iarrRules: FormRule[] = [];

  /**
   * Grouped for the rule-type dropdown, per issue #10. Validation rules fail a
   * submission; conditional rules change form state instead. Only Show/Hide exists
   * on the conditional side so far — the other five come in the next phase.
   */
  iarrRuleGroups: { label: string; types: RuleType[] }[] = [
    {
      label: 'VALIDATION',
      types: ['Required', 'Length', 'Range', 'Pattern', 'Format', 'Date', 'File', 'CompareFields']
    },
    {
      label: 'CONDITIONAL RULE',
      types: ['Visibility']
    }
  ];

  iarrFormatKinds: FormatKind[] = ['Email', 'Phone', 'URL', 'Number', 'Alphanumeric'];

  iobjDraft: Partial<CreateFormRuleRequest> = { ruleType: 'Required', severity: 'Error' };

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

  istrVisibilityAction: 'Show' | 'Hide' = 'Show';
  istrVisibilityTriggerKey = '';
  istrVisibilityOperator: '==' | '!=' = '==';
  istrVisibilityValue = '';

  constructor(private iobjRoute: ActivatedRoute, private iobjRuleService: RuleService, private iobjFormService: FormService) { }

  ngOnInit(): void {
    this.inumFormId = Number(this.iobjRoute.snapshot.paramMap.get('formId'));
    this.inumVersionId = Number(this.iobjRoute.snapshot.paramMap.get('versionId'));

    this.iobjFormService.getVersionById(this.inumVersionId).subscribe(res => {
      if (res.success && res.data) this.iarrControls = res.data.controls;
    });

    this.loadRules();
  }

  loadRules(): void {
    this.iobjRuleService.getRules(this.inumVersionId).subscribe(rules => this.iarrRules = rules);
  }

  controlLabel(aStrControlKey: string): string {
    return this.iarrControls.find(c => c.controlKey === aStrControlKey)?.label ?? aStrControlKey;
  }

  /** Rules saved before issue #10 still carry the old names, so the table shows them readably too. */
  ruleTypeLabel(aStrRuleType: RuleType): string {
    switch (aStrRuleType) {
      case 'CompareFields': case 'CrossField': return 'Compare Fields';
      case 'Visibility': return 'Show / Hide';
      case 'MinLength': return 'Length (min)';
      case 'MaxLength': return 'Length (max)';
      case 'Regex': return 'Pattern';
      case 'Email': return 'Format (Email)';
      default: return aStrRuleType;
    }
  }

  /** Conditional rules describe their own effect, so they take no error message. */
  isConditional(aStrRuleType?: RuleType): boolean {
    return aStrRuleType === 'Visibility';
  }

  private buildDetailsJson(): string | undefined {
    switch (this.iobjDraft.ruleType) {
      case 'Length':
        return JSON.stringify({ min: this.inumLengthMin, max: this.inumLengthMax });
      case 'Range':
        return JSON.stringify({ min: this.inumRangeMin, max: this.inumRangeMax });
      case 'Pattern':
        return JSON.stringify({ pattern: this.istrPattern });
      case 'Format':
        return JSON.stringify({ format: this.istrFormatKind });
      case 'File':
        return JSON.stringify({
          allowedExtensions: this.parseExtensions(this.istrFileExtensions),
          maxSizeKb: this.inumFileMaxSizeKb
        });
      case 'Date':
        return JSON.stringify({ operator: this.istrDateOperator });
      case 'CompareFields':
        return JSON.stringify({ compareControlKey: this.istrCompareFieldKey, operator: this.istrCompareFieldOperator });
      case 'Visibility':
        return JSON.stringify({
          triggerControlKey: this.istrVisibilityTriggerKey,
          operator: this.istrVisibilityOperator,
          triggerValue: this.istrVisibilityValue,
          action: this.istrVisibilityAction
        });
      default:
        return undefined;
    }
  }

  /** "pdf, .PNG , jpg" becomes ['pdf','png','jpg'] — the server compares without the dot, case-insensitively. */
  private parseExtensions(aStrRaw: string): string[] {
    return aStrRaw
      .split(',')
      .map(e => e.trim().replace(/^\./, '').toLowerCase())
      .filter(e => e.length > 0);
  }

  addRule(): void {
    const lboolIsConditional = this.isConditional(this.iobjDraft.ruleType);
    if (!this.iobjDraft.controlKey || !this.iobjDraft.ruleType) return;
    if (!lboolIsConditional && !this.iobjDraft.errorMessage) return;
    if (lboolIsConditional && (!this.istrVisibilityTriggerKey || !this.istrVisibilityValue)) return;

    // A File rule with neither bound set would silently allow everything.
    if (this.iobjDraft.ruleType === 'File'
      && this.parseExtensions(this.istrFileExtensions).length === 0
      && this.inumFileMaxSizeKb == null) return;

    const lobjDto: CreateFormRuleRequest = {
      controlKey: this.iobjDraft.controlKey,
      ruleType: this.iobjDraft.ruleType,
      ruleDetailsJson: this.buildDetailsJson(),
      errorMessage: lboolIsConditional
        ? `${this.istrVisibilityAction} when ${this.istrVisibilityTriggerKey} ${this.istrVisibilityOperator} ${this.istrVisibilityValue}`
        : this.iobjDraft.errorMessage!,
      severity: lboolIsConditional ? 'Warning' : (this.iobjDraft.severity ?? 'Error'),
      displayOrder: this.iarrRules.length
    };

    this.iobjRuleService.addRule(this.inumVersionId, lobjDto).subscribe(() => {
      this.loadRules();
      this.resetDraft();
    });
  }

  private resetDraft(): void {
    this.iobjDraft = { ruleType: 'Required', severity: 'Error' };
    this.inumLengthMin = undefined;
    this.inumLengthMax = undefined;
    this.inumRangeMin = undefined;
    this.inumRangeMax = undefined;
    this.istrPattern = '';
    this.istrFormatKind = 'Email';
    this.istrFileExtensions = '';
    this.inumFileMaxSizeKb = undefined;
    this.istrCompareFieldKey = '';
    this.istrVisibilityTriggerKey = '';
    this.istrVisibilityValue = '';
  }

  deleteRule(aObjR: FormRule): void {
    this.iobjRuleService.deleteRule(this.inumVersionId, aObjR.controlKey, aObjR.ruleType).subscribe(() => this.loadRules());
  }

}