import { Injectable } from '@angular/core';
import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import {
  CompareFieldsDetails, ConditionalDetails, ConditionLogic, ConditionOperator, ControlEffects,
  DateRuleDetails, FilterDependencyDetails, FormatDetails, FormatKind, FormRule, LengthDetails, PatternDetails,
  RangeDetails, RuleCondition, RuleType,
  SetValueDetails
} from '../models/rule.model';
import { FormControlDef } from '../models/form.model';

/**
 * Client-side counterpart of the server's RuleEngineService (C#). Same RuleType +
 * RuleDetailsJson contract, same semantics per rule — this gives instant inline
 * feedback in the browser, while the server re-runs the identical logic as the
 * actual gate before persisting a submission (client checks can always be bypassed).
 *
 * Rules are identified by ControlKey now, not a database-assigned ControlId — there's
 * no FormControls/FormRules table anymore, both live embedded in FormDefinitionJson.
 *
 * IMPORTANT: when adding a new RuleType, update BOTH this file and the server's RuleEngineService.cs.
 */
@Injectable({ providedIn: 'root' })
export class RuleEngineService {

  /** Must stay identical to the pattern constants in the C# RuleEngineService, or a value
   *  passes in the browser and then fails on submit. */
  private static readonly FormatPatterns: Record<FormatKind, RegExp> = {
    'Email': /^[^@\s]+@[^@\s]+\.[^@\s]+$/,
    'Phone': /^\+?[0-9\s\-()]{7,15}$/,
    'URL': /^https?:\/\/[^\s/$.?#].[^\s]*$/,
    'Number': /^-?\d+(\.\d+)?$/,
    'Alphanumeric': /^[A-Za-z0-9]+$/
  };

  /**
   * Maps a stored rule name onto its current equivalent — mirrors NormalizeRuleType in
   * the C# engine. Rules saved before issue #10 still carry the old names, so they are
   * normalised on read rather than migrated.
   */
  private normalizeRuleType(aStrRuleType: RuleType): RuleType {
    switch (aStrRuleType) {
      case 'MinLength':
      case 'MaxLength': return 'Length';
      case 'Regex': return 'Pattern';
      case 'Email': return 'Format';
      case 'CrossField': return 'CompareFields';
      default: return aStrRuleType;
    }
  }

  /** Conditional rules produce effects rather than failures. */
  private isConditional(aStrRuleType: RuleType): boolean {
    return aStrRuleType === 'Visibility'
      || aStrRuleType === 'EnableDisable'
      || aStrRuleType === 'RequiredOptional'
      || aStrRuleType === 'SetValue'
      || aStrRuleType === 'FilterDependency';
  }

  /** Builds an Angular ValidatorFn for one rule. Cross-field rules need the whole form group. */
  buildValidator(rule: FormRule, getFieldValue: (controlKey: string) => any): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const value = control.value;
      const stringValue = value === null || value === undefined ? '' : String(value);

      if (rule.ruleType !== 'Required' && stringValue.trim() === '') {
        return null;
      }

      const passed = this.evaluateOne(rule, stringValue, getFieldValue);
      if (passed) return null;

      return {
        ruleFailure: {
          ruleType: rule.ruleType,
          message: rule.errorMessage,
          severity: rule.severity
        }
      };
    };
  }

  /**
   * Conditional rules change form state rather than failing a submission, so their output
   * is a per-control effect map. Mirrors ComputeEffects in the C# engine — the two must
   * agree, or the browser shows one thing and the server enforces another.
   *
   * Replaces the old computeVisibility(): Show/Hide is now one action among six rather
   * than a mechanism of its own.
   */
  computeEffects(rules: FormRule[], values: Record<string, any>, controls: FormControlDef[]):
    Record<string, ControlEffects> {
    const effects: Record<string, ControlEffects> = {};

    // Every control starts visible and enabled; required comes from its own definition.
    for (const c of controls) {
      effects[c.controlKey] = { visible: true, enabled: true, required: !!c.isRequired };
    }

    // A stored Required rule is another way of saying the control is required by default,
    // so it seeds the same flag — a Required/Optional rule below can then override it.
    // Matched on rule TYPE, not severity: every validation rule defaults to Error severity,
    // so filtering on that would mark a field with only a Length rule as required.
    for (const rule of rules.filter(r => r.isActive && this.normalizeRuleType(r.ruleType) === 'Required')) {
      if (effects[rule.controlKey]) effects[rule.controlKey].required = true;
    }

      for (const rule of rules
      .filter(r => r.isActive && this.isConditional(r.ruleType))
      .sort((a, b) => a.displayOrder - b.displayOrder)) {

      if (!effects[rule.controlKey]) {
        effects[rule.controlKey] = { visible: true, enabled: true, required: false };
      }
      const e = effects[rule.controlKey];
              e.calculated = true;

      // Filter/Dependency has no conditions — the source control's current value is the
      // lookup key, so it is handled before the condition machinery below.
      if (rule.ruleType === 'FilterDependency') {
        const fd = this.parseDetails<FilterDependencyDetails>(rule.ruleDetailsJson);
        if (!fd?.sourceControlKey) continue;

        const raw = values[fd.sourceControlKey];
        const sourceValue = raw === null || raw === undefined ? '' : String(raw);

        // A source value with no entry yields an empty list rather than undefined —
        // "no match" means no options, not "leave the control's own options in place".
        e.options = fd.mapping?.[sourceValue] ?? [];
        continue;
      }

      const d = this.parseDetails<ConditionalDetails & SetValueDetails>(rule.ruleDetailsJson);
      const larrConditions = this.normaliseConditions(d);
      if (larrConditions.length === 0) continue;

      const conditionMet = this.evaluateConditions(larrConditions, d?.logic ?? 'AND', values);

      if (rule.ruleType === 'SetValue') {
        // Only writes when the conditions hold — a rule that stops matching leaves
        // whatever the user has since typed alone rather than clearing it.
        if (conditionMet) e.value = d?.value;
        continue;
      }

      switch (d?.action) {
        case 'Show': e.visible = conditionMet; break;
        case 'Hide': e.visible = !conditionMet; break;
        case 'Enable': e.enabled = conditionMet; break;
        case 'Disable': e.enabled = !conditionMet; break;
        case 'Required': e.required = conditionMet; break;
        case 'Optional': e.required = !conditionMet; break;
      }
    }
    return effects;
  }

  /** Numeric when both sides parse as numbers, string otherwise. All six operators supported. */
  private compareValues(a: string, b: string, op: ConditionOperator): boolean {
    const numA = Number(a), numB = Number(b);
    if (a.trim() !== '' && b.trim() !== '' && !Number.isNaN(numA) && !Number.isNaN(numB)) {
      return this.compare(numA, numB, op);
    }
    switch (op) {
      case '!=': return a !== b;
      case '==': return a === b;
      default: return a === b;
    }
  }

  /** Evaluates a full rule set against a value map — same shape as the server's Evaluate(). */
  evaluateAll(rules: FormRule[], values: Record<string, any>): { isValid: boolean; failures: FormRule[] } {
    const failures: FormRule[] = [];
    let isValid = true;

    for (const rule of rules.filter(r => r.isActive).sort((a, b) => a.displayOrder - b.displayOrder)) {
      if (this.isConditional(rule.ruleType)) continue;

      const raw = values[rule.controlKey];
      const stringValue = raw === null || raw === undefined ? '' : String(raw);

      if (rule.ruleType !== 'Required' && stringValue.trim() === '') continue;

      const passed = this.evaluateOne(rule, stringValue, key => values[key]);
      if (!passed) {
        failures.push(rule);
        if (rule.severity === 'Error') isValid = false;
      }
    }

    return { isValid, failures };
  }

  private evaluateOne(rule: FormRule, value: string, getFieldValue: (controlKey: string) => any): boolean {
    switch (this.normalizeRuleType(rule.ruleType)) {
      case 'Required':
        return value.trim().length > 0;

      case 'Length': {
        const d = this.parseDetails<LengthDetails>(rule.ruleDetailsJson);
        const min = d?.min ?? 0;
        const max = d?.max ?? Number.MAX_SAFE_INTEGER;
        return value.length >= min && value.length <= max;
      }

      case 'Range': {
        const num = Number(value);
        if (Number.isNaN(num)) return false;
        const d = this.parseDetails<RangeDetails>(rule.ruleDetailsJson);
        const min = d?.min ?? -Infinity;
        const max = d?.max ?? Infinity;
        return num >= min && num <= max;
      }

      case 'Pattern': {
        const d = this.parseDetails<PatternDetails>(rule.ruleDetailsJson);
        if (!d?.pattern) return true;
        // A malformed pattern is ignored rather than failing the field — the server does the same.
        try { return new RegExp(d.pattern).test(value); } catch { return true; }
      }

      case 'Format': {
        const d = this.parseDetails<FormatDetails>(rule.ruleDetailsJson);
        // Legacy Email rules carry no `format` key, so Email is the default.
        const pattern = RuleEngineService.FormatPatterns[d?.format ?? 'Email'];
        return pattern ? pattern.test(value) : true;
      }

      case 'Date': {
        const date = new Date(value);
        if (isNaN(date.getTime())) return false;
        const d = this.parseDetails<DateRuleDetails>(rule.ruleDetailsJson);
        if (!d?.operator) return true;
        const today = new Date(); today.setHours(0, 0, 0, 0);
        const cmp = new Date(date); cmp.setHours(0, 0, 0, 0);
        switch (d.operator) {
          case '<=Today': return cmp.getTime() <= today.getTime();
          case '>=Today': return cmp.getTime() >= today.getTime();
          case '<Today': return cmp.getTime() < today.getTime();
          case '>Today': return cmp.getTime() > today.getTime();
          default: return true;
        }
      }

      case 'CompareFields': {
        const d = this.parseDetails<CompareFieldsDetails>(rule.ruleDetailsJson);
        if (!d?.compareControlKey) return true;
        const compareRaw = getFieldValue(d.compareControlKey);
        const compareValue = compareRaw === null || compareRaw === undefined ? '' : String(compareRaw);

        const a = Number(value), b = Number(compareValue);
        if (!Number.isNaN(a) && !Number.isNaN(b)) return this.compare(a, b, d.operator);

        const da = new Date(value), db = new Date(compareValue);
        if (!isNaN(da.getTime()) && !isNaN(db.getTime())) return this.compare(da.getTime(), db.getTime(), d.operator);

        return d.operator === '==' ? value === compareValue : value !== compareValue;
      }

      // Needs the uploaded file's size, which the browser has but the value map does not.
      // Enforced server-side in SubmissionService, before the file is written to disk.
      case 'File':
        return true;

      case 'Custom':
        return true;

      // Conditional rules produce effects, not failures — see computeEffects().
       case 'Visibility':
      case 'EnableDisable':
      case 'RequiredOptional':
      case 'SetValue':
      case 'FilterDependency':
        return true;

      default:
        return true;
    }
  }

  private compare(a: number, b: number, op: ConditionOperator): boolean {
    switch (op) {
      case '==': return a === b;
      case '!=': return a !== b;
      case '<': return a < b;
      case '<=': return a <= b;
      case '>': return a > b;
      case '>=': return a >= b;
      default: return true;
    }
  }

  private parseDetails<T>(json?: string): T | null {
    if (!json) return null;
    try { return JSON.parse(json) as T; } catch { return null; }
  }

  /**
   * Reads a conditional rule's trigger, in either shape. Rules written before
   * multi-condition support carry a single flat trigger; those are wrapped into a
   * one-element list so the evaluation path is the same for both.
   */
  private normaliseConditions(d: ConditionalDetails | null): RuleCondition[] {
    if (!d) return [];

    if (Array.isArray(d.conditions) && d.conditions.length > 0) {
      return d.conditions.filter(c => !!c.controlKey);
    }

    if (d.triggerControlKey) {
      return [{
        controlKey: d.triggerControlKey,
        operator: d.operator ?? '==',
        value: d.triggerValue ?? ''
      }];
    }

    return [];
  }

  /** A rule with no usable conditions never fires — a half-configured Hide rule
   *  should not blank out a field. */
  private evaluateConditions(
    conditions: RuleCondition[],
    logic: ConditionLogic,
    values: Record<string, any>
  ): boolean {
    if (conditions.length === 0) return false;

    const isOr = logic === 'OR';

    for (const c of conditions) {
      const raw = values[c.controlKey];
      const actual = raw === null || raw === undefined ? '' : String(raw);
      const met = this.compareValues(actual, c.value ?? '', c.operator ?? '==');

      if (isOr && met) return true;
      if (!isOr && !met) return false;
    }

    return !isOr;
  }
}