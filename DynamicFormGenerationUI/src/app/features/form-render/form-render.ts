import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { FormControlDef, FormRenderPayload } from '../../core/models/form.model';
import { ActivatedRoute } from '@angular/router';
import { FormService } from '../../core/services/form';
import { SubmissionService } from '../../core/services/submission';
import { ControlEffects, FormRule } from '../../core/models/rule.model';
import { RuleEngineService } from '../../core/services/rule-engine';
import { environment } from '../../../environments/environment';

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
  iboolReadOnly = false;

  /** Per-control state after all conditional rules have run. Replaces the old visibility map. */
  iobjEffects: Record<string, ControlEffects> = {};

  inumColumnLayout = 1;
  selectedFiles: Record<string, File> = {};
  imagePreviewUrls: Record<string, string> = {};

  /// Built once in buildForm() and held, rather than returned from a getter. A getter
  /// calling filter() hands *ngFor a new array object on every change-detection pass, so
  /// the list was torn down and rebuilt continuously and the form appeared one control at
  /// a time instead of all at once.
  iarrUnsectionedControls: FormControlDef[] = [];
  iobjSectionControls: Record<string, FormControlDef[]> = {};

  private formId!: number;
  private versionId!: number;

  constructor(private route: ActivatedRoute, private formService: FormService, private submissionService: SubmissionService, private ruleEngine: RuleEngineService) { }

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

  private loadSubmissionForViewing(aNumSubmissionId: number): void {
    this.submissionService.getDetail(aNumSubmissionId).subscribe(res => {
      if (!res.success || !res.data) return;

      // Set before patching: patchValue fires valueChanges, which runs recomputeEffects,
      // and that pass must know the form is read-only or it will re-enable controls.
      this.iboolReadOnly = true;

      this.form.patchValue(res.data.values);
      this.form.disable();
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
      if (c.controlTypeCode === 'Label') continue;
      const defaultValue = c.controlTypeCode === 'CheckboxList' ? [] : (c.defaultValue ?? '');
      group[c.controlKey] = [defaultValue, []];
    }

    this.form = this.fb.group(group);

    // Grouped once here so the template binds to stable array references.
    this.iarrUnsectionedControls = payload.controls.filter(c => !c.sectionKey);
    this.iobjSectionControls = {};
    for (const s of payload.sections ?? []) {
      this.iobjSectionControls[s.sectionKey] = payload.controls.filter(c => c.sectionKey === s.sectionKey);
    }

    this.recomputeEffects(payload);

    this.form.valueChanges.subscribe(() => {
      payload.rules
        .filter((r: FormRule) => r.ruleType === 'CompareFields' || r.ruleType === 'CrossField')
        .forEach(r => this.form.get(r.controlKey)?.updateValueAndValidity({ emitEvent: false }));

      this.recomputeEffects(payload);
    });
  }

  private buildValidatorsFor(aObjControl: FormControlDef, aArrRules: FormRule[], aBoolRequired: boolean): ValidatorFn[] {
    const larrConditional: string[] = ['Visibility', 'EnableDisable', 'RequiredOptional'];

    const larrValidators: ValidatorFn[] = aArrRules.filter(r => r.controlKey === aObjControl.controlKey &&
      !larrConditional.includes(r.ruleType) && r.ruleType !== 'Required').map(r => this.ruleEngine.buildValidator(
        r, key => this.form.get(key)?.value));

    if (aBoolRequired) {
      // Must match on ruleType: taking the first active rule on the control meant a field
      // with both Required and, say, CompareFields showed the wrong message when empty.
      const lobjRequiredRule = aArrRules.find(r => r.controlKey === aObjControl.controlKey
        && r.isActive && r.ruleType === 'Required');

      larrValidators.push(control => {
        const empty = control.value === null || control.value === undefined
          || (typeof control.value === 'string' && control.value.trim() === '');
        if (empty) {
          return { ruleFailure: { message: lobjRequiredRule?.errorMessage || 'This field is required.' } };
        }
        return null;
      });
    }

    return larrValidators;
  }

  /**
   * Applies every conditional effect in one pass: visibility, enabled state and required.
   *
   * Every setter passes { emitEvent: false } — enable(), disable() and setValidators all
   * fire valueChanges, and this method is called *from* a valueChanges subscription, so
   * without it the form loops until the tab locks up.
   */
  private recomputeEffects(payload: FormRenderPayload): void {
    this.iobjEffects = this.ruleEngine.computeEffects(payload.rules, this.form.getRawValue(), payload.controls);

    // Viewing a submitted response: the whole form is disabled on purpose, so effects are
    // computed for display only and must not re-enable anything.
    if (this.iboolReadOnly) return;

    for (const c of payload.controls) {
      if (c.controlTypeCode === 'Label') continue;

      const lobjEffect = this.iobjEffects[c.controlKey];
      const ctrl = this.form.get(c.controlKey);
      if (!lobjEffect || !ctrl) continue;

      // A calculated field is derived, so the user must not type over it.
      if (lobjEffect.calculated && ctrl.enabled) {
        ctrl.disable({ emitEvent: false });
      }

      if (lobjEffect.enabled && ctrl.disabled) {
        ctrl.enable({ emitEvent: false });
      } else if (!lobjEffect.enabled && ctrl.enabled) {
        ctrl.disable({ emitEvent: false });
      }

      if (lobjEffect.value !== undefined && ctrl.value !== lobjEffect.value) {
        ctrl.setValue(lobjEffect.value, { emitEvent: false });
      }

      // A filtered-out value would otherwise sit in the control invisibly and be
      // submitted as something no longer offered.
      if (lobjEffect.options && ctrl.value && !lobjEffect.options.includes(String(ctrl.value))) {
        ctrl.setValue('', { emitEvent: false });
      }

      // A control the user cannot see or edit is not held to its rules, so a hidden
      // Required field never blocks submission.
      const lboolActive = lobjEffect.visible && lobjEffect.enabled;

      if (!lboolActive) {
        ctrl.clearValidators();
        if (!lobjEffect.visible) ctrl.setValue('', { emitEvent: false });
      } else {
        ctrl.setValidators(this.buildValidatorsFor(c, payload.rules, lobjEffect.required));
      }

      ctrl.updateValueAndValidity({ emitEvent: false });
    }
  }

  isVisible(aStrControlKey: string): boolean {
    return this.iobjEffects[aStrControlKey]?.visible !== false;
  }

  isEnabled(aStrControlKey: string): boolean {
    return this.iobjEffects[aStrControlKey]?.enabled !== false;
  }

  isRequired(aStrControlKey: string): boolean {
    return this.iobjEffects[aStrControlKey]?.required === true;
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

  /// File rules cannot go through buildValidator(): a validator sees only the control's
  /// value, which is the file name, so size is invisible to it. The check runs here, where
  /// the File object exists, and the error is pushed onto the control directly.
  /// Client-side only — the server still accepts anything (RP-04).
  onFileSelected(controlKey: string, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const lstrError = this.ruleEngine.validateFile(this.payload?.rules ?? [], controlKey, file);
    if (lstrError) {
      // Cleared, or the rejected name stays in the value map and is submitted as valid.
      input.value = '';
      delete this.selectedFiles[controlKey];
      delete this.imagePreviewUrls[controlKey];

      const lobjCtrl = this.form.get(controlKey);
      lobjCtrl?.setValue('', { emitEvent: false });
      lobjCtrl?.markAsTouched();
      lobjCtrl?.setErrors({ ruleFailure: { message: lstrError } });
      return;
    }

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

  /** Stored filename held by a File/Image control, or '' when nothing was uploaded. */
  storedFileName(aStrControlKey: string): string {
    const lobjValue = this.form.get(aStrControlKey)?.value;
    return typeof lobjValue === 'string' ? lobjValue : '';
  }

  /** Inline URL — what <img src> points at. */
  fileUrl(aStrStoredFileName: string): string {
    return `${environment.apiUrl}/files/${encodeURIComponent(aStrStoredFileName)}`;
  }

  /** Attachment URL — what the Download link points at. */
  downloadUrl(aStrStoredFileName: string): string {
    return `${this.fileUrl(aStrStoredFileName)}?download=true`;
  }

  /** Stored names are "{Guid}_{originalName}" — show the user only the original part. */
  displayFileName(aStrStoredFileName: string): string {
    if (!aStrStoredFileName) return '';

    const lnumIndex = aStrStoredFileName.indexOf('_');
    if (lnumIndex <= 0) return aStrStoredFileName;

    const lstrPrefix = aStrStoredFileName.substring(0, lnumIndex);
    const lobjGuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

    return lobjGuidPattern.test(lstrPrefix)
      ? aStrStoredFileName.substring(lnumIndex + 1)
      : aStrStoredFileName;
  }

  optionsFor(aObjControl: FormControlDef): string[] {
    return this.iobjEffects[aObjControl.controlKey]?.options ?? this.seedOptions(aObjControl);
  }

  /** Controls belonging to one section. Reads the map built in buildForm(). */
  controlsIn(aStrSectionKey: string): FormControlDef[] {
    return this.iobjSectionControls[aStrSectionKey] ?? [];
  }

  /** Stable identity for *ngFor so Angular reuses rows instead of rebuilding them. */
  trackByControlKey(aNumIndex: number, aObjControl: FormControlDef): string {
    return aObjControl.controlKey;
  }

  /**
   * The section header and border are drawn by the section itself, so its visibility is
   * tracked separately from its controls' — even though a hidden section also marks
   * every control inside it hidden.
   */
  isSectionVisible(aStrSectionKey: string): boolean {
    return this.ruleEngine.isSectionVisible(aStrSectionKey);
  }
}