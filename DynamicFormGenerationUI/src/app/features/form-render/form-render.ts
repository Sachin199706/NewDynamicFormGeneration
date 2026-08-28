import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { FormControlDef, FormRenderPayload } from '../../core/models/form.model';
import { ActivatedRoute } from '@angular/router';
import { FormService } from '../../core/services/form';
import { SubmissionService } from '../../core/services/submission';
import { FormRule } from '../../core/models/rule.model';
import { RuleEngineService } from '../../core/services/rule-engine';
import { FileService } from '../../core/services/file';

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
  private controlKeyById: Record<number, string> = {};

  constructor(private route: ActivatedRoute, private formService: FormService, private submissionService: SubmissionService, private ruleEngine: RuleEngineService, private fileService: FileService) {

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

    this.inumColumnLayout = 1;
    if (payload.layoutDefinitionJson) {
      try {
        const lobjLayout = JSON.parse(payload.layoutDefinitionJson);
        if (lobjLayout.columnLayout) this.inumColumnLayout = lobjLayout.columnLayout;
      } catch { /* default stays 1 */ }
    }

    for (const c of payload.controls) {
      this.controlKeyById[c.controlId!] = c.controlKey;

      if (c.controlTypeCode === 'Label') continue; // static text, not a real form field

      // Validation rules only — Visibility rules are excluded here and handled by computeVisibility().
      const rulesForControl = payload.rules.filter(r => r.controlId === c.controlId && r.ruleType !== 'Visibility');
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

    // Image preview — only relevant for the Image control, harmless no-op for File
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

    this.submissionService.submit(this.formId, {
      formId: this.formId,
      formVersionId: this.versionId,
      values: this.form.value
    }).subscribe(res => {
      if (res.success && res.data) {
        this.uploadSelectedFiles(res.data);
        this.submitted = true;
      } else {
        this.serverErrors = res.errors ?? [res.message ?? 'Submission failed.'];
      }
    });
  }

  private uploadSelectedFiles(submissionId: number): void {
    for (const controlKey of Object.keys(this.selectedFiles)) {
      const controlIdEntry = Object.entries(this.controlKeyById).find(([, key]) => key === controlKey);
      if (!controlIdEntry) continue;

      this.fileService.upload(submissionId, Number(controlIdEntry[0]), this.selectedFiles[controlKey]).subscribe({
        error: (err) => console.error(`File upload failed for ${controlKey}:`, err)
      });
    }
  }
}