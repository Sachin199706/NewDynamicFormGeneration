import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormControlDef } from '../../core/models/form.model';
import { CreateFormRuleRequest, FormRule, RuleType } from '../../core/models/rule.model';
import { ActivatedRoute } from '@angular/router';
import { FormService } from '../../core/services/form';
import { RuleService } from '../../core/services/rule';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-form-rules',
  imports: [CommonModule, FormsModule],
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
  iobjDateOperator: '<=Today' | '>=Today' | '<Today' | '>Today' = '<=Today';
  istrCrossFieldKey = '';
  crossFieldOperator: '==' | '!=' | '<' | '<=' | '>' | '>=' = '==';

  iobjVisibilityAction: 'Show' | 'Hide' = 'Show';
  iobjVisibilityTriggerKey = '';
  iobjVisibilityOperator: '==' | '!=' = '==';
  istrVisibilityValue = '';

  constructor(private iobjRoute: ActivatedRoute, private iobjRuleService: RuleService, private iobjFormService: FormService) { }

  ngOnInit(): void {
    this.inumFormId = Number(this.iobjRoute.snapshot.paramMap.get('formId'));
    this.inumVersionId = Number(this.iobjRoute.snapshot.paramMap.get('versionId'));

    this.iobjFormService.getLatestVersion(this.inumFormId).subscribe(res => {
      if (res.success && res.data) this.iarrControls = res.data.controls;
    });

    this.loadRules();
  }

  loadRules(): void {
    const _self = this;
    this.iobjRuleService.getRules(this.inumVersionId).subscribe(rules => _self.iarrRules = rules);
  }

  controlLabel(controlId: number): string {
    return this.iarrControls.find(c => c.controlId === controlId)?.label ?? `#${controlId}`;
  }

  private buildDetailsJson(): string | undefined {
    switch (this.iobjDraft.ruleType) {
      case 'MinLength': return JSON.stringify({ min: this.inumLengthValue });
      case 'MaxLength': return JSON.stringify({ max: this.inumLengthValue });
      case 'Range': return JSON.stringify({ min: this.inumRangeMin, max: this.inumRangeMax });
      case 'Regex': return JSON.stringify({ pattern: this.istrRegexPattern });
      case 'Date': return JSON.stringify({ operator: this.iobjDateOperator });
      case 'CrossField': return JSON.stringify({ compareControlKey: this.istrCrossFieldKey, operator: this.crossFieldOperator });
      case 'Visibility': return JSON.stringify({
        triggerControlKey: this.iobjVisibilityTriggerKey,
        operator: this.iobjVisibilityOperator,
        triggerValue: this.istrVisibilityValue,
        action: this.iobjVisibilityAction
      });
      default: return undefined;
    }
  }

  addRule(): void {
    const isVisibility = this.iobjDraft.ruleType === 'Visibility';
    if (!this.iobjDraft.controlId || !this.iobjDraft.ruleType) return;
    if (!isVisibility && !this.iobjDraft.errorMessage) return;
    if (isVisibility && (!this.iobjVisibilityTriggerKey || !this.istrVisibilityValue)) return;

    const dto: CreateFormRuleRequest = {
      controlId: this.iobjDraft.controlId,
      ruleType: this.iobjDraft.ruleType,
      ruleDetailsJson: this.buildDetailsJson(),
      errorMessage: isVisibility
        ? `${this.iobjVisibilityAction} when ${this.iobjVisibilityTriggerKey} ${this.iobjVisibilityOperator} ${this.istrVisibilityValue}`
        : this.iobjDraft.errorMessage!,
      // Visibility rules never block submission regardless of Severity (RuleEngineService skips
      // them entirely) — force Warning so the intent reads correctly if this list is ever surfaced.
      severity: isVisibility ? 'Warning' : (this.iobjDraft.severity ?? 'Error'),
      displayOrder: this.iarrRules.length
    };

    this.iobjRuleService.addRule(this.inumVersionId, dto).subscribe(() => {
      this.loadRules();
      this.iobjDraft = { ruleType: 'Required', severity: 'Error' };
      this.iobjVisibilityTriggerKey = '';
      this.istrVisibilityValue = '';
    });
  }

  deleteRule(r: FormRule): void {
    this.iobjRuleService.deleteRule(r.ruleId).subscribe(() => this.loadRules());
  }

}
