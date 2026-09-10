import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConditionalAction, ConditionLogic, CreateFormRuleRequest, FormRule, RuleCondition, RuleType } from '../../core/models/rule.model';
import { FormControlDef } from '../../core/models/form.model';
import { RuleService } from '../../core/services/rule';
import { ExpressionEvaluator } from '../../core/services/expression-evaluator';

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
    },
    {
      type: 'SetValue', label: 'Set Value', icon: 'bi-pencil-square',
      actions: ['SetValue'],
      badges: { 'SetValue': { text: 'Set Value', css: 'bg-info-subtle text-info-emphasis' } }
    },
    {
      type: 'FilterDependency', label: 'Filter Options', icon: 'bi-funnel',
      actions: ['Filter'],
      badges: { 'Filter': { text: 'Filter Options', css: 'bg-secondary-subtle text-secondary-emphasis' } }
    },
    {
      type: 'Calculate', label: 'Calculate', icon: 'bi-calculator',
      actions: ['Calculate'],
      badges: { 'Calculate': { text: 'Calculate', css: 'bg-warning-subtle text-warning-emphasis' } }
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

  /** Set Value: what gets written into the target when the conditions hold. */
  istrSetValue = '';

  /** Filter Options: the control whose value drives the filtering. */
  istrSourceControlKey = '';

  /**
   * Filter Options mapping, held as rows for editing. Stored as
   * { "India": ["Maharashtra","Gujarat"] } — flattened on save, expanded on edit.
   */
  iarrMappingRows: { sourceValue: string; options: string }[] = [{ sourceValue: '', options: '' }];

  istrExpression = '';
  inumDecimals?: number;

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
    const d = this.parseDetails(aObjRule.ruleDetailsJson);
    if (d.action) return d.action;

    // Calculate and Filter have no action of their own — name the rule instead.
    return aObjRule.ruleType === 'Calculate' ? 'Calculate'
      : aObjRule.ruleType === 'FilterDependency' ? 'Filter'
        : 'Show';
  }

  badgeFor(aObjRule: FormRule): { text: string; css: string } {
    const lobjMeta = this.iarrRuleTypes.find(t => t.type === aObjRule.ruleType);
    return lobjMeta?.badges[this.actionOf(aObjRule)]
      ?? { text: aObjRule.ruleType, css: 'bg-light text-muted' };
  }

  /** "Reason = Other AND Department != HR" — the Condition column. */
  conditionText(aObjRule: FormRule): string {
    const d = this.parseDetails(aObjRule.ruleDetailsJson);

    if (aObjRule.ruleType === 'FilterDependency') {
      return `Based on ${this.controlLabel(d.sourceControlKey ?? '')}`;
    }
    if (aObjRule.ruleType === 'Calculate') {
      return d.expression ?? '—';
    }

    const larr = this.conditionsOf(aObjRule);
    if (larr.length === 0) return '—';

    return larr
      .map(c => `${this.controlLabel(c.controlKey)} ${c.operator === '==' ? '=' : c.operator} ${c.value}`)
      .join(` ${d.logic ?? 'AND'} `);
  }

  /** The stored description is already type-aware — see save(). */
  summaryText(aObjRule: FormRule): string {
    return aObjRule.errorMessage;
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

  /** True when the rule type is driven by conditions rather than a source lookup. */
  get usesConditions(): boolean {
    return this.istrRuleType !== 'FilterDependency' && this.istrRuleType !== 'Calculate';
  }

  /** True when the rule needs an Action picker — the other three have only one action. */
  get usesAction(): boolean {
    return this.istrRuleType !== 'SetValue'
      && this.istrRuleType !== 'FilterDependency'
      && this.istrRuleType !== 'Calculate';
  }

  addMappingRow(): void {
    this.iarrMappingRows.push({ sourceValue: '', options: '' });
  }

  removeMappingRow(aNumIndex: number): void {
    if (this.iarrMappingRows.length > 1) this.iarrMappingRows.splice(aNumIndex, 1);
  }

  /** Rows to the stored shape. "Maharashtra, Gujarat" becomes ['Maharashtra','Gujarat']. */
  private buildMapping(): Record<string, string[]> {
    const lobjMapping: Record<string, string[]> = {};

    for (const lobjRow of this.iarrMappingRows) {
      const lstrKey = lobjRow.sourceValue.trim();
      if (!lstrKey) continue;

      lobjMapping[lstrKey] = lobjRow.options
        .split(',')
        .map(o => o.trim())
        .filter(o => o.length > 0);
    }

    return lobjMapping;
  }

  /** The chosen field's own options, offered as autocomplete when writing a mapping. */
  seedOptionsFor(aStrControlKey: string): string[] {
    const lobjControl = this.iarrControls.find(c => c.controlKey === aStrControlKey);
    if (!lobjControl?.propertiesJson) return [];
    try {
      const lobjProps = JSON.parse(lobjControl.propertiesJson);
      return typeof lobjProps.SeedData === 'string' ? lobjProps.SeedData.split(',').map((o: string) => o.trim()) : [];
    } catch { return []; }
  }

  /** Live check so a typo in the expression is caught before the rule is saved. */
  get expressionError(): string {
    if (!this.istrExpression) return '';

    const larrFields = ExpressionEvaluator.referencedFields(this.istrExpression);
    const larrUnknown = larrFields.filter(k => !this.iarrControls.some(c => c.controlKey === k));
    if (larrUnknown.length > 0) return `Unknown field: ${larrUnknown.join(', ')}`;

    // Every referenced field set to 1 — proves the syntax parses, whatever the real values.
    const lobjProbe: Record<string, any> = {};
    larrFields.forEach(k => lobjProbe[k] = 1);
    if (ExpressionEvaluator.evaluate(this.istrExpression, lobjProbe) === null) {
      return 'The expression could not be parsed.';
    }

    return '';
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

    const dAll = this.parseDetails(aObjRule.ruleDetailsJson);
    this.istrLogic = dAll.logic ?? 'AND';

    if (aObjRule.ruleType === 'SetValue') {
      this.istrSetValue = dAll.value ?? '';
    }

    if (aObjRule.ruleType === 'Calculate') {
      this.istrExpression = dAll.expression ?? '';
      this.inumDecimals = dAll.decimals;
    }

    if (aObjRule.ruleType === 'FilterDependency') {
      this.istrSourceControlKey = dAll.sourceControlKey ?? '';
      const lobjMapping: Record<string, string[]> = dAll.mapping ?? {};
      const larrRows = Object.keys(lobjMapping).map(k => ({
        sourceValue: k,
        options: (lobjMapping[k] ?? []).join(', ')
      }));
      this.iarrMappingRows = larrRows.length > 0 ? larrRows : [{ sourceValue: '', options: '' }];
    }

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
    if (!this.istrControlKey) return false;

    if (this.istrRuleType === 'FilterDependency') {
      // A mapping with no rows would blank the target's options entirely.
      return !!this.istrSourceControlKey && Object.keys(this.buildMapping()).length > 0;
    }

    if (this.istrRuleType === 'Calculate') {
      return !!this.istrExpression && this.expressionError === '';
    }

    if (this.validConditions.length === 0) return false;
    if (this.istrRuleType === 'SetValue') return this.istrSetValue !== '';

    return true;
  }

  save(): void {
    if (!this.canSave) return;

    const larrValid = this.validConditions;
    let lstrDetailsJson: string;
    let lstrDescription: string;

    if (this.istrRuleType === 'FilterDependency') {
      lstrDetailsJson = JSON.stringify({
        sourceControlKey: this.istrSourceControlKey,
        mapping: this.buildMapping()
      });
      lstrDescription = `Filter "${this.controlLabel(this.istrControlKey)}" by ${this.controlLabel(this.istrSourceControlKey)}`;
    }
    else if (this.istrRuleType === 'SetValue') {
      lstrDetailsJson = JSON.stringify({
        conditions: larrValid,
        logic: this.istrLogic,
        value: this.istrSetValue,
        action: 'SetValue'
      });
      lstrDescription = `Set "${this.controlLabel(this.istrControlKey)}" to ${this.istrSetValue} when ` +
        this.describeConditions(larrValid);
    }
    else if (this.istrRuleType === 'Calculate') {
      lstrDetailsJson = JSON.stringify({
        expression: this.istrExpression,
        decimals: this.inumDecimals
      });
      lstrDescription = `Calculate "${this.controlLabel(this.istrControlKey)}" as ${this.istrExpression}`;
    }
    else {
      lstrDetailsJson = JSON.stringify({
        conditions: larrValid,
        logic: this.istrLogic,
        action: this.istrAction
      });
      lstrDescription = `${this.istrAction} "${this.controlLabel(this.istrControlKey)}" when ` +
        this.describeConditions(larrValid);
    }

    const lobjDto: CreateFormRuleRequest = {
      controlKey: this.istrControlKey,
      ruleType: this.istrRuleType,
      ruleDetailsJson: lstrDetailsJson,
      // Conditional rules never fail a submission, so this is a description rather than
      // an error — it is what the table's summary column shows.
      errorMessage: lstrDescription,
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

  private describeConditions(aArrConditions: RuleCondition[]): string {
    return aArrConditions
      .map(c => `${this.controlLabel(c.controlKey)} ${c.operator} ${c.value}`)
      .join(` ${this.istrLogic} `);
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
    this.istrSetValue = '';
    this.istrSourceControlKey = '';
    this.iarrMappingRows = [{ sourceValue: '', options: '' }];
    this.istrExpression = '';
    this.inumDecimals = undefined;
  }
}