import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConditionalAction, ConditionLogic, CreateFormRuleRequest, FormRule, RuleCondition, RuleType } from '../../core/models/rule.model';
import { FormControlDef } from '../../core/models/form.model';
import { RuleService } from '../../core/services/rule';


interface ConditionalTypeMeta {
  type: RuleType;
  label: string;
  icon: string;
  actions: ConditionalAction[];
  /** Badge text and colour in the table, per action. */
  badges: Record<string, { text: string; css: string }>;
}

@Component({
  selector: 'app-conditional-rules-tab',
  imports: [CommonModule, FormsModule],
  templateUrl: './conditional-rules-tab.html',
})
export class ConditionalRulesTab {

  @Input() inumVersionId!: number;
  @Input() iarrControls: FormControlDef[] = [];
  @Input() iarrRules: FormRule[] = [];
  @Output() ichanged = new EventEmitter<void>();
  @Output() igoToValidation = new EventEmitter<void>();

  iboolFormOpen = false;
  istrEditingRuleId: string | null = null;

  iarrRuleTypes: ConditionalTypeMeta[] = [
    {
      type: 'Visibility', label: 'Show / Hide Field', icon: 'bi-eye',
      actions: ['Show', 'Hide'],
      badges: {
        'Show': { text: 'Show Field', css: 'bg-success-subtle text-success-emphasis' },
        'Hide': { text: 'Hide Field', css: 'bg-primary-subtle text-primary-emphasis' }
      }
    },
    {
      type: 'EnableDisable', label: 'Enable / Disable Field', icon: 'bi-toggle-on',
      actions: ['Enable', 'Disable'],
      badges: {
        'Enable': { text: 'Enable Field', css: 'bg-info-subtle text-info-emphasis' },
        'Disable': { text: 'Disable Field', css: 'bg-secondary-subtle text-secondary-emphasis' }
      }
    },
    {
      type: 'RequiredOptional', label: 'Make Required / Optional', icon: 'bi-asterisk',
      actions: ['Required', 'Optional'],
      badges: {
        'Required': { text: 'Make Required', css: 'bg-warning-subtle text-warning-emphasis' },
        'Optional': { text: 'Make Optional', css: 'bg-light text-muted' }
      }
    }
  ];

  iarrExamples = [
    { icon: 'bi-eye', title: 'Show / Hide Field', sub: 'Show or hide a field based on another field value.' },
    { icon: 'bi-asterisk', title: 'Make Required / Optional', sub: 'Make a field required or optional conditionally.' },
    { icon: 'bi-toggle-on', title: 'Enable / Disable Field', sub: 'Enable or disable a field for user input.' },
    { icon: 'bi-pencil-square', title: 'Set Value', sub: 'Automatically set a value in a field.' },
    { icon: 'bi-layout-text-window', title: 'Show / Hide Section', sub: 'Show or hide a section based on condition.' },
    { icon: 'bi-funnel', title: 'Filter Options', sub: 'Filter dropdown/list options based on another field.' }
  ];

  istrRuleType: RuleType = 'Visibility';
  istrControlKey = '';
  istrAction: ConditionalAction = 'Show';
  iarrConditions: RuleCondition[] = [{ controlKey: '', operator: '==', value: '' }];
  istrLogic: ConditionLogic = 'AND';

  constructor(private iobjRuleService: RuleService) { }

  get selectedMeta(): ConditionalTypeMeta | undefined {
    return this.iarrRuleTypes.find(t => t.type === this.istrRuleType);
  }

  controlLabel(aStrControlKey: string): string {
    return this.iarrControls.find(c => c.controlKey === aStrControlKey)?.label ?? aStrControlKey;
  }

  private parseDetails(aStrJson?: string): any {
    if (!aStrJson) return {};
    try { return JSON.parse(aStrJson); } catch { return {}; }
  }

  /** Rules saved before multi-condition support carry a single flat trigger. */
  private conditionsOf(aObjRule: FormRule): RuleCondition[] {
    const d = this.parseDetails(aObjRule.ruleDetailsJson);
    if (Array.isArray(d.conditions) && d.conditions.length > 0) return d.conditions;
    if (d.triggerControlKey) {
      return [{ controlKey: d.triggerControlKey, operator: d.operator ?? '==', value: d.triggerValue ?? '' }];
    }
    return [];
  }

  actionOf(aObjRule: FormRule): string {
    return this.parseDetails(aObjRule.ruleDetailsJson).action ?? 'Show';
  }

  badgeFor(aObjRule: FormRule): { text: string; css: string } {
    const lobjMeta = this.iarrRuleTypes.find(t => t.type === aObjRule.ruleType);
    return lobjMeta?.badges[this.actionOf(aObjRule)]
      ?? { text: aObjRule.ruleType, css: 'bg-light text-muted' };
  }

  /** "Reason = Other AND Department != HR" — the Condition column. */
  conditionText(aObjRule: FormRule): string {
    const d = this.parseDetails(aObjRule.ruleDetailsJson);
    const larr = this.conditionsOf(aObjRule);
    if (larr.length === 0) return '—';

    return larr
      .map(c => `${this.controlLabel(c.controlKey)} ${c.operator === '==' ? '=' : c.operator} ${c.value}`)
      .join(` ${d.logic ?? 'AND'} `);
  }

  /** The plain-English sentence in the Rule (Summary) column. */
  summaryText(aObjRule: FormRule): string {
    return `${this.actionOf(aObjRule)} "${this.controlLabel(aObjRule.controlKey)}" when ${this.conditionText(aObjRule)}`;
  }

  onRuleTypeChange(): void {
    const larrActions = this.selectedMeta?.actions ?? ['Show', 'Hide'];
    if (!larrActions.includes(this.istrAction)) this.istrAction = larrActions[0];
  }

  addCondition(): void {
    this.iarrConditions.push({ controlKey: '', operator: '==', value: '' });
  }

  /** The first condition is never removed — a rule with none can never fire. */
  removeCondition(aNumIndex: number): void {
    if (this.iarrConditions.length > 1) this.iarrConditions.splice(aNumIndex, 1);
  }

  openAdd(): void {
    this.resetDraft();
    this.istrEditingRuleId = null;
    this.iboolFormOpen = true;
  }

  openEdit(aObjRule: FormRule): void {
    this.resetDraft();
    this.istrEditingRuleId = aObjRule.ruleId;
    this.istrRuleType = aObjRule.ruleType;
    this.istrControlKey = aObjRule.controlKey;
    this.istrAction = this.actionOf(aObjRule) as ConditionalAction;
    this.istrLogic = this.parseDetails(aObjRule.ruleDetailsJson).logic ?? 'AND';

    const larr = this.conditionsOf(aObjRule);
    this.iarrConditions = larr.length > 0
      ? larr.map(c => ({ ...c }))
      : [{ controlKey: '', operator: '==', value: '' }];

    this.iboolFormOpen = true;
  }

  cancel(): void {
    this.iboolFormOpen = false;
    this.istrEditingRuleId = null;
  }

  private get validConditions(): RuleCondition[] {
    return this.iarrConditions.filter(c => !!c.controlKey && c.value !== '');
  }

  get canSave(): boolean {
    return !!this.istrControlKey && this.validConditions.length > 0;
  }

  save(): void {
    if (!this.canSave) return;

    const larrValid = this.validConditions;

    const lobjDto: CreateFormRuleRequest = {
      controlKey: this.istrControlKey,
      ruleType: this.istrRuleType,
      ruleDetailsJson: JSON.stringify({
        conditions: larrValid,
        logic: this.istrLogic,
        action: this.istrAction
      }),
      // Conditional rules never fail a submission, so this is a description rather than
      // an error — it is what the table's summary column falls back to.
      errorMessage: `${this.istrAction} "${this.controlLabel(this.istrControlKey)}" when ` +
        larrValid.map(c => `${this.controlLabel(c.controlKey)} ${c.operator} ${c.value}`)
                 .join(` ${this.istrLogic} `),
      severity: 'Warning',
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
    this.istrRuleType = 'Visibility';
    this.istrControlKey = '';
    this.istrAction = 'Show';
    this.istrLogic = 'AND';
    this.iarrConditions = [{ controlKey: '', operator: '==', value: '' }];
  }
}