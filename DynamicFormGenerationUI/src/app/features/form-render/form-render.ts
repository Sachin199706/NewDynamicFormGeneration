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
  form: FormGroup = this.fb.group({});
  serverErrors: string[] = [];
  submitted = false;
  visibility: Record<string, boolean> = {};
  inumColumnLayout = 1;
  selectedFiles: Record<string, File> = {};
  imagePreviewUrls: Record<string, string> = {};

  private formId!: number;
  private versionId!: number;

  constructor(private route: ActivatedRoute, private formService: FormService, private submissionService: SubmissionService,
    private ruleEngine: RuleEngineService
  ) {

  }

  ngOnInit(): void {
  this.formId = Number(this.route.snapshot.paramMap.get('formId'));
  this.versionId = Number(this.route.snapshot.paramMap.get('versionId'));
  const lStrSubmissionIdParam = this.route.snapshot.queryParamMap.get('submissionId');

  this.formService.getRenderPayload(this.formId, this.versionId).subscribe(res => {
    if (!res.success || !res.data) return;
    this.payload = res.data;
    this.buildForm(res.data);

    if (lStrSubmissionIdParam) {
      this.loadSubmissionForViewing(Number(lStrSubmissionIdParam));
    }
  });
}

iboolReadOnly = false;

private loadSubmissionForViewing(aNumSubmissionId: number): void {
  this.submissionService.getDetail(aNumSubmissionId).subscribe(res => {
    if (!res.success || !res.data) return;

    this.form.patchValue(res.data.values);

    console.log('FORM VALUES:', this.form.getRawValue());

    const dropdown = this.payload?.controls.find(
      c => c.controlTypeCode === 'Dropdown'
    );

    const radio = this.payload?.controls.find(
      c => c.controlTypeCode === 'Radio'
    );

    const checkbox = this.payload?.controls.find(
      c => c.controlTypeCode === 'CheckboxList'
    );

    console.log('Dropdown value:',
      dropdown ? this.form.get(dropdown.controlKey)?.value : null
    );

    console.log('Dropdown options:',
      dropdown ? this.seedOptions(dropdown) : []
    );

    console.log('Radio value:',
      radio ? this.form.get(radio.controlKey)?.value : null
    );

    console.log('Radio options:',
      radio ? this.seedOptions(radio) : []
    );

    console.log('Checkbox value:',
      checkbox ? this.form.get(checkbox.controlKey)?.value : null
    );

    console.log('Checkbox options:',
      checkbox ? this.seedOptions(checkbox) : []
    );

    this.form.disable();
    this.iboolReadOnly = true;
  });
}
  private buildForm(payload: FormRenderPayload): void {
    const group: Record<string, any> = {};

    this.inumColumnLayout = 1;
    if (payload.layoutDefinitionJson) {
      try {
        const lobjLayout = JSON.parse(payload.layoutDefinitionJson);
        if (lobjLayout.columnLayout) this.inumColumnLayout = lobjLayout.columnLayout;
      } catch { /* default stays 1 */ }
    }

    for (const c of payload.controls) {
      if (c.controlTypeCode === 'Label') continue; // static text, not a real form field

      const rulesForControl = payload.rules.filter(r => r.controlKey === c.controlKey && r.ruleType !== 'Visibility');
      const validators: ValidatorFn[] = rulesForControl.map(r =>
        this.ruleEngine.buildValidator(r, key => this.form.get(key)?.value)
      );
      if (c.isRequired) validators.push(Validators.required);

      const defaultValue = c.controlTypeCode === 'CheckboxList' ? [] : (c.defaultValue ?? '');
      group[c.controlKey] = [defaultValue, validators];
    }

    this.form = this.fb.group(group);
    this.recomputeVisibility(payload);

    this.form.valueChanges.subscribe(() => {
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
    const newVisibility = this.ruleEngine.computeVisibility(payload.rules, this.form.value);

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
        const rulesForControl = payload.rules.filter(r => r.controlKey === c.controlKey && r.ruleType !== 'Visibility');
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

  isCheckboxListOptionSelected(controlKey: string, opt: string): boolean {
    const current: string[] = this.form.get(controlKey)?.value ?? [];
    return current.includes(opt);
  }

  toggleCheckboxListOption(controlKey: string, opt: string): void {
    const ctrl = this.form.get(controlKey);
    if (!ctrl) return;
    const current: string[] = ctrl.value ?? [];
    const next = current.includes(opt) ? current.filter(v => v !== opt) : [...current, opt];
    ctrl.setValue(next);
  }

  onFileSelected(controlKey: string, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.selectedFiles[controlKey] = file;
    this.form.get(controlKey)?.setValue(file.name);

    if (file.type.startsWith('image/')) {
      const reader = new FileReader();
      reader.onload = () => {
        this.imagePreviewUrls[controlKey] = reader.result as string;
      };
      reader.readAsDataURL(file);
    }
  }

  submit(): void {
    this.serverErrors = [];
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    const lobjFormData = new FormData();
    lobjFormData.append('formVersionId', this.versionId.toString());
    lobjFormData.append('values', JSON.stringify(this.form.value));

    for (const controlKey of Object.keys(this.selectedFiles)) {
      lobjFormData.append(controlKey, this.selectedFiles[controlKey]);
    }

    this.submissionService.submit(this.formId, lobjFormData).subscribe(res => {
      if (res.success) {
        this.submitted = true;
      } else {
        this.serverErrors = res.errors ?? [res.message ?? 'Submission failed.'];
      }
    });
  }
}