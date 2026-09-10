/**
 * Evaluates the arithmetic expressions used by Calculate rules — shunting-yard to RPN,
 * then a stack evaluation.
 *
 * Deliberately NOT eval() or Function(): the expression is typed into the Rule Builder
 * by a user, so evaluating it as JavaScript would run whatever they wrote. This parser
 * understands only numbers, {fieldKey} references, + - * / and parentheses, and returns
 * null for anything else rather than throwing.
 */
export class ExpressionEvaluator {

  private static readonly Precedence: Record<string, number> = {
    '+': 1, '-': 1, '*': 2, '/': 2
  };

  /**
   * Returns the computed value, or null when the expression is malformed, references a
   * field that has no numeric value, or divides by zero. Null means "show nothing"
   * rather than "show zero" — a half-filled form should leave the total blank.
   */
  static evaluate(aStrExpression: string, aObjValues: Record<string, any>): number | null {
    if (!aStrExpression || aStrExpression.trim() === '') return null;

    const larrTokens = this.tokenise(aStrExpression, aObjValues);
    if (larrTokens === null) return null;

    const larrRpn = this.toRpn(larrTokens);
    if (larrRpn === null) return null;

    return this.evaluateRpn(larrRpn);
  }

  /** Field keys are extracted so the caller knows which controls to watch for changes. */
  static referencedFields(aStrExpression: string): string[] {
    const larrMatches = aStrExpression.match(/\{([^}]+)\}/g) ?? [];
    return larrMatches.map(m => m.slice(1, -1).trim()).filter(k => k.length > 0);
  }

  /** Substitutes {fieldKey} for its numeric value, then splits into numbers and operators. */
  private static tokenise(aStrExpression: string, aObjValues: Record<string, any>): string[] | null {
    let lstrResolved = aStrExpression;
    let lboolFieldMissing = false;

    lstrResolved = lstrResolved.replace(/\{([^}]+)\}/g, (_match, aStrKey: string) => {
      const lobjRaw = aObjValues[aStrKey.trim()];
      const lnumValue = Number(lobjRaw);

      // An empty or non-numeric field makes the whole expression unresolvable.
      if (lobjRaw === null || lobjRaw === undefined || String(lobjRaw).trim() === '' || Number.isNaN(lnumValue)) {
        lboolFieldMissing = true;
        return '0';
      }

      return String(lnumValue);
    });

    if (lboolFieldMissing) return null;

    const larrTokens = lstrResolved.match(/\d+\.?\d*|[+\-*/()]/g);
    if (!larrTokens) return null;

    // Anything left over after removing the recognised tokens is unsupported syntax.
    const lstrRemainder = lstrResolved.replace(/\d+\.?\d*|[+\-*/()]/g, '').trim();
    if (lstrRemainder !== '') return null;

    return larrTokens;
  }

  private static toRpn(aArrTokens: string[]): string[] | null {
    const larrOutput: string[] = [];
    const larrOperators: string[] = [];

    for (const lstrToken of aArrTokens) {
      if (/^\d/.test(lstrToken)) {
        larrOutput.push(lstrToken);
        continue;
      }

      if (lstrToken === '(') {
        larrOperators.push(lstrToken);
        continue;
      }

      if (lstrToken === ')') {
        while (larrOperators.length > 0 && larrOperators[larrOperators.length - 1] !== '(') {
          larrOutput.push(larrOperators.pop()!);
        }
        // No matching open bracket — malformed.
        if (larrOperators.pop() !== '(') return null;
        continue;
      }

      while (larrOperators.length > 0) {
        const lstrTop = larrOperators[larrOperators.length - 1];
        if (lstrTop === '(') break;
        if (this.Precedence[lstrTop] < this.Precedence[lstrToken]) break;
        larrOutput.push(larrOperators.pop()!);
      }

      larrOperators.push(lstrToken);
    }

    while (larrOperators.length > 0) {
      const lstrOperator = larrOperators.pop()!;
      if (lstrOperator === '(') return null;   // unclosed bracket
      larrOutput.push(lstrOperator);
    }

    return larrOutput;
  }

  private static evaluateRpn(aArrRpn: string[]): number | null {
    const larrStack: number[] = [];

    for (const lstrToken of aArrRpn) {
      if (/^\d/.test(lstrToken)) {
        larrStack.push(Number(lstrToken));
        continue;
      }

      const lnumB = larrStack.pop();
      const lnumA = larrStack.pop();
      if (lnumA === undefined || lnumB === undefined) return null;

      switch (lstrToken) {
        case '+': larrStack.push(lnumA + lnumB); break;
        case '-': larrStack.push(lnumA - lnumB); break;
        case '*': larrStack.push(lnumA * lnumB); break;
        case '/':
          if (lnumB === 0) return null;
          larrStack.push(lnumA / lnumB);
          break;
        default: return null;
      }
    }

    return larrStack.length === 1 ? larrStack[0] : null;
  }
}