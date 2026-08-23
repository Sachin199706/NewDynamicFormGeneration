import { Injectable } from '@angular/core';
import { CrossFieldDetails, DateRuleDetails, FormRule, MinMaxLengthDetails, RangeDetails, RegexDetails, VisibilityDetails } from '../models/rule.model';
import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

@Injectable({
  providedIn: 'root',
})
export class RuleEngineService {
  /** Builds an Angular ValidatorFn for one rule. Cross-field rules need the whole form group. */
  buildValidator(rule: FormRule, getFieldValue: (controlKey: string) => any): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const value = control.value;
      const stringValue = value === null || value === undefined ? '' : String(value);

      // Optional fields: only Required rules fire on empty values (mirrors the server).
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
  computeVisibility(rules: FormRule[], values: Record<string, any>,
    controlKeyById: Record<number, string>): Record<string, boolean> {
    const visibility: Record<string, boolean> = {};

    for (const rule of rules.filter(r => r.isActive && r.ruleType === 'Visibility')) {
      const targetKey = controlKeyById[rule.controlId];
      if (!targetKey) continue;

      const d = this.parseDetails<VisibilityDetails>(rule.ruleDetailsJson);
      if (!d?.triggerControlKey) continue;

      const raw = values[d.triggerControlKey];
      const stringValue = raw === null || raw === undefined ? '' : String(raw);
      const conditionMet = this.compareStrings(stringValue, d.triggerValue ?? '', d.operator);

      const shouldShow = d.action === 'Hide' ? !conditionMet : conditionMet;
      visibility[targetKey] = shouldShow;
    }

    return visibility;
  }

  private compareStrings(a: string, b: string, op: VisibilityDetails['operator']): boolean {
    const numA = Number(a), numB = Number(b);
    if (!Number.isNaN(numA) && !Number.isNaN(numB)) return this.compare(numA, numB, op);
    switch (op) {
      case '==': return a === b;
      case '!=': return a !== b;
      default: return a === b; // <, <=, >, >= on non-numeric strings falls back to equality
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
    switch (rule.ruleType) {
      case 'Required':
        return value.trim().length > 0;

      case 'MinLength': {
        const d = this.parseDetails<MinMaxLengthDetails>(rule.ruleDetailsJson);
        return value.length >= (d?.min ?? 0);
      }

      case 'MaxLength': {
        const d = this.parseDetails<MinMaxLengthDetails>(rule.ruleDetailsJson);
        return value.length <= (d?.max ?? Number.MAX_SAFE_INTEGER);
      }

      case 'Regex': {
        const d = this.parseDetails<RegexDetails>(rule.ruleDetailsJson);
        if (!d?.pattern) return true;
        try { return new RegExp(d.pattern).test(value); } catch { return true; }
      }

      case 'Range': {
        const num = Number(value);
        if (Number.isNaN(num)) return false;
        const d = this.parseDetails<RangeDetails>(rule.ruleDetailsJson);
        const min = d?.min ?? -Infinity;
        const max = d?.max ?? Infinity;
        return num >= min && num <= max;
      }

      case 'Email':
        return /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(value);

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

      case 'CrossField': {
        const d = this.parseDetails<CrossFieldDetails>(rule.ruleDetailsJson);
        if (!d?.compareControlKey) return true;
        const compareRaw = getFieldValue(d.compareControlKey);
        const compareValue = compareRaw === null || compareRaw === undefined ? '' : String(compareRaw);

        const a = Number(value), b = Number(compareValue);
        if (!Number.isNaN(a) && !Number.isNaN(b)) return this.compare(a, b, d.operator);

        const da = new Date(value), db = new Date(compareValue);
        if (!isNaN(da.getTime()) && !isNaN(db.getTime())) return this.compare(da.getTime(), db.getTime(), d.operator);

        return d.operator === '==' ? value === compareValue : value !== compareValue;
      }

      case 'Custom':
        return true; // opt-in server hook; client treats as pass, server may enforce more

      case 'Visibility':
        return true; // UI-only — handled separately by computeVisibility(), never a validation failure

      default:
        return true;
    }
  }

  private compare(a: number, b: number, op: CrossFieldDetails['operator']): boolean {
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
}

