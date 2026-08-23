import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { FormControlDef, FormRenderPayload } from '../../core/models/form.model';
import { ActivatedRoute } from '@angular/router';
import { FormService } from '../../core/services/form';
import { SubmissionService } from '../../core/services/submission';
import { FormRule } from '../../core/models/rule.model';
import { RuleEngineService } from '../../core/services/rule-engine';

@Component({
  selector: 'app-form-render',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './form-render.html',
  styleUrl: './form-render.scss',
})
export class FormRender implements OnInit {
  payload: FormRenderPayload | null = null;
  private fb: FormBuilder = inject(FormBuilder);
  form: FormGroup = this.fb.group({});;
  serverErrors: string[] = [];
  submitted = false;
  visibility: Record<string, boolean> = {};

  private formId!: number;
  private versionId!: number;
  private controlKeyById: Record<number, string> = {};

  constructor(private route: ActivatedRoute, private formService: FormService, private submissionService: SubmissionService,
    private ruleEngine: RuleEngineService
  ) {

  }

  ngOnInit(): void {
    this.formId = Number(this.route.snapshot.paramMap.get('formId'));
    this.versionId = Number(this.route.snapshot.paramMap.get('versionId'));

    this.formService.getRenderPayload(this.formId, this.versionId).subscribe(res => {
      if (!res.success || !res.data) return;
      this.payload = res.data;
      this.buildForm(res.data);
    });
  }

  private buildForm(payload: FormRenderPayload): void {
    const group: Record<string, any> = {};
    this.controlKeyById = {};

    for (const c of payload.controls) {
      this.controlKeyById[c.controlId!] = c.controlKey;

      // Validation rules only — Visibility rules are excluded here and handled by computeVisibility().
      const rulesForControl = payload.rules.filter(r => r.controlId === c.controlId && r.ruleType !== 'Visibility');
      const validators: ValidatorFn[] = rulesForControl.map(r =>
        this.ruleEngine.buildValidator(r, key => this.form.get(key)?.value)
      );
      if (c.isRequired) validators.push(Validators.required);

      group[c.controlKey] = [c.defaultValue ?? '', validators];
    }

    this.form = this.fb.group(group);
    this.recomputeVisibility(payload);

    this.form.valueChanges.subscribe(() => {
      // Re-validate cross-field-dependent controls whenever any value changes,
      // mirroring how the server re-evaluates the whole rule set together.
      payload.rules
        .filter((r: FormRule) => r.ruleType === 'CrossField')
        .forEach(r => this.form.get(r.controlKey)?.updateValueAndValidity({ emitEvent: false }));

      this.recomputeVisibility(payload);
    });
  }

  /**
   * Recomputes which controls should be visible and keeps validation in sync: a control
   * that becomes hidden has its validators cleared (so a hidden Required field never blocks
   * submission), and gets them restored when it becomes visible again.
   */
  private recomputeVisibility(payload: FormRenderPayload): void {
    const newVisibility = this.ruleEngine.computeVisibility(payload.rules, this.form.value, this.controlKeyById);

    for (const c of payload.controls) {
      const wasVisible = this.visibility[c.controlKey] !== false;
      const isVisible = newVisibility[c.controlKey] !== false;
      if (wasVisible === isVisible) continue;

      const ctrl = this.form.get(c.controlKey);
      if (!ctrl) continue;

      if (!isVisible) {
        ctrl.clearValidators();
        ctrl.setValue('', { emitEvent: false });
      } else {
        const rulesForControl = payload.rules.filter(r => r.controlId === c.controlId && r.ruleType !== 'Visibility');
        const validators: ValidatorFn[] = rulesForControl.map(r =>
          this.ruleEngine.buildValidator(r, key => this.form.get(key)?.value)
        );
        if (c.isRequired) validators.push(Validators.required);
        ctrl.setValidators(validators);
      }
      ctrl.updateValueAndValidity({ emitEvent: false });
    }

    this.visibility = newVisibility;
  }

  seedOptions(c: FormControlDef): string[] {
    if (!c.propertiesJson) return [];
    try {
      const props = JSON.parse(c.propertiesJson);
      return typeof props.SeedData === 'string' ? props.SeedData.split(',') : [];
    } catch { return []; }
  }

  submit(): void {
    this.serverErrors = [];
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.submissionService.submit(this.formId, {
      formId: this.formId,
      formVersionId: this.versionId,
      values: this.form.value
    }).subscribe(res => {
      if (res.success) {
        this.submitted = true;
      } else {
        // Server re-validated via the same rule contract and rejected — show it,
        // since the client's checks are UX only, not the actual gate.
        this.serverErrors = res.errors ?? [res.message ?? 'Submission failed.'];
      }
    });
  }
}

