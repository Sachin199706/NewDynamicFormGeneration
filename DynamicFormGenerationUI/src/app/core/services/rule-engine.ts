import { Injectable } from '@angular/core';
import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import {
  CompareFieldsDetails, DateRuleDetails, FormatDetails, FormatKind, FormRule, LengthDetails,
  PatternDetails, RangeDetails, RuleType, VisibilityDetails
} from '../models/rule.model';

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
   * Visibility rules are UI-only — they never produce a validation error, so they're kept
   * separate from evaluateAll()/buildValidator(). Returns controlKey -> should-be-visible.
   * Controls with no Visibility rule default to visible.
   */
  computeVisibility(rules: FormRule[], values: Record<string, any>): Record<string, boolean> {
    const visibility: Record<string, boolean> = {};

    for (const rule of rules.filter(r => r.isActive && r.ruleType === 'Visibility')) {
      const d = this.parseDetails<VisibilityDetails>(rule.ruleDetailsJson);
      if (!d?.triggerControlKey) continue;

      const raw = values[d.triggerControlKey];
      const stringValue = raw === null || raw === undefined ? '' : String(raw);
      const conditionMet = this.compareStrings(stringValue, d.triggerValue ?? '', d.operator);

      const shouldShow = d.action === 'Hide' ? !conditionMet : conditionMet;
      visibility[rule.controlKey] = shouldShow;
    }

    return visibility;
  }

  private compareStrings(a: string, b: string, op: VisibilityDetails['operator']): boolean {
    const numA = Number(a), numB = Number(b);
    if (!Number.isNaN(numA) && !Number.isNaN(numB)) return this.compare(numA, numB, op);
    switch (op) {
      case '==': return a === b;
      case '!=': return a !== b;
      default:   return a === b;
    }
  }

  /** Evaluates a full rule set against a value map — same shape as the server's Evaluate(). */
  evaluateAll(rules: FormRule[], values: Record<string, any>): { isValid: boolean; failures: FormRule[] } {
    const failures: FormRule[] = [];
    let isValid = true;

    for (const rule of rules.filter(r => r.isActive).sort((a, b) => a.displayOrder - b.displayOrder)) {
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
          case '<Today':  return cmp.getTime() <  today.getTime();
          case '>Today':  return cmp.getTime() >  today.getTime();
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

      case 'Visibility':
        return true;

      default:
        return true;
    }
  }

  private compare(a: number, b: number, op: CompareFieldsDetails['operator']): boolean {
    switch (op) {
      case '==': return a === b;
      case '!=': return a !== b;
      case '<':  return a < b;
      case '<=': return a <= b;
      case '>':  return a > b;
      case '>=': return a >= b;
      default:   return true;
    }
  }

  private parseDetails<T>(json?: string): T | null {
    if (!json) return null;
    try { return JSON.parse(json) as T; } catch { return null; }
  }
}