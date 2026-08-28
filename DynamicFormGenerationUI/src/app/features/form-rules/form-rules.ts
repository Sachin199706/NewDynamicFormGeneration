import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormControlDef } from '../../core/models/form.model';
import { CreateFormRuleRequest, FormRule, RuleType } from '../../core/models/rule.model';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormService } from '../../core/services/form';
import { RuleService } from '../../core/services/rule';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-form-rules',
  imports: [CommonModule, FormsModule,RouterLink],
  templateUrl: './form-rules.html',
  styleUrl: './form-rules.scss',
})
export class FormRules implements OnInit {

  inumFormId!: number;
  inumVersionId!: number;
  iarrControls: FormControlDef[] = [];
  iarrRules: FormRule[] = [];

  iarrRuleTypes: RuleType[] = ['Required', 'MinLength', 'MaxLength', 'Regex', 'Range', 'Email', 'Date', 'CrossField', 'Visibility'];

  iobjDraft: Partial<CreateFormRuleRequest> = { ruleType: 'Required', severity: 'Error' };
  inumLengthValue?: number;
  inumRangeMin?: number;
  inumRangeMax?: number;
  istrRegexPattern = '';
  istrDateOperator: '<=Today' | '>=Today' | '<Today' | '>Today' = '<=Today';
  istrCrossFieldKey = '';
  istrCrossFieldOperator: '==' | '!=' | '<' | '<=' | '>' | '>=' = '==';

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

  controlLabel(aNumControlId: number): string {
    return this.iarrControls.find(c => c.controlId === aNumControlId)?.label ?? `#${aNumControlId}`;
  }

  private buildDetailsJson(): string | undefined {
    switch (this.iobjDraft.ruleType) {
      case 'MinLength': return JSON.stringify({ min: this.inumLengthValue });
      case 'MaxLength': return JSON.stringify({ max: this.inumLengthValue });
      case 'Range': return JSON.stringify({ min: this.inumRangeMin, max: this.inumRangeMax });
      case 'Regex': return JSON.stringify({ pattern: this.istrRegexPattern });
      case 'Date': return JSON.stringify({ operator: this.istrDateOperator });
      case 'CrossField': return JSON.stringify({ compareControlKey: this.istrCrossFieldKey, operator: this.istrCrossFieldOperator });
      case 'Visibility': return JSON.stringify({
        triggerControlKey: this.istrVisibilityTriggerKey,
        operator: this.istrVisibilityOperator,
        triggerValue: this.istrVisibilityValue,
        action: this.istrVisibilityAction
      });
      default: return undefined;
    }
  }

  addRule(): void {
    const lboolIsVisibility = this.iobjDraft.ruleType === 'Visibility';
    if (!this.iobjDraft.controlId || !this.iobjDraft.ruleType) return;
    if (!lboolIsVisibility && !this.iobjDraft.errorMessage) return;
    if (lboolIsVisibility && (!this.istrVisibilityTriggerKey || !this.istrVisibilityValue)) return;

    const lobjDto: CreateFormRuleRequest = {
      controlId: this.iobjDraft.controlId,
      ruleType: this.iobjDraft.ruleType,
      ruleDetailsJson: this.buildDetailsJson(),
      errorMessage: lboolIsVisibility
        ? `${this.istrVisibilityAction} when ${this.istrVisibilityTriggerKey} ${this.istrVisibilityOperator} ${this.istrVisibilityValue}`
        : this.iobjDraft.errorMessage!,
      severity: lboolIsVisibility ? 'Warning' : (this.iobjDraft.severity ?? 'Error'),
      displayOrder: this.iarrRules.length
    };

    this.iobjRuleService.addRule(this.inumVersionId, lobjDto).subscribe(() => {
      this.loadRules();
      this.iobjDraft = { ruleType: 'Required', severity: 'Error' };
      this.istrVisibilityTriggerKey = '';
      this.istrVisibilityValue = '';
    });
  }

  deleteRule(aObjR: FormRule): void {
    this.iobjRuleService.deleteRule(aObjR.ruleId).subscribe(() => this.loadRules());
  }

}