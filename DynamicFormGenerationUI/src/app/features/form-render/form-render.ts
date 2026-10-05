import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, ValidatorFn } from '@angular/forms';
import { FormControlDef, FormLayoutRow, FormRenderPayload, normalizeLayoutDefinition } from '../../core/models/form.model';
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

  iobjEffects: Record<string, ControlEffects> = {};

  inumColumnLayout = 1;
  layoutRows: FormLayoutRow[] = [];
  selectedFiles: Record<string, File> = {};
  imagePreviewUrls: Record<string, string> = {};

  iarrUnsectionedControls: FormControlDef[] = [];
  iobjSectionControls: Record<string, FormControlDef[]> = {};

  // Public identifier of the form version, taken from the fill link (/fill/:publicId).
  private istrPublicId = '';

  constructor(private route: ActivatedRoute, private formService: FormService, private submissionService: SubmissionService, private ruleEngine: RuleEngineService) { }

   ngOnInit(): void {
    this.istrPublicId = this.route.snapshot.paramMap.get('publicId') ?? '';
    const lStrSubmissionIdParam = this.route.snapshot.queryParamMap.get('submissionId');
    this.formService.getRenderPayload(this.istrPublicId).subscribe(res => {
      if (!res.success || !res.data) return;

      // Submitted-result link: the form is shown only after the submission is found.
      if (lStrSubmissionIdParam) {
        this.loadSubmissionForViewing(Number(lStrSubmissionIdParam), res.data);
        return;
      }

      this.payload = res.data;
      this.buildForm(res.data);
    });
  }

  private loadSubmissionForViewing(aNumSubmissionId: number, aObjPayload: FormRenderPayload): void {
    this.submissionService.getDetail(aNumSubmissionId).subscribe({
      next: res => {
        // Submission not found: nothing is shown.
        if (!res.success || !res.data) return;

        this.payload = aObjPayload;
        this.buildForm(aObjPayload);
        this.iboolReadOnly = true;
        this.form.patchValue(res.data.values);
        this.form.disable();
      },
      // The API answers 404 when the submission does not exist: nothing is shown.
      error: () => { }
    });
  }

  private buildForm(payload: FormRenderPayload): void {
    const group: Record<string, any> = {};

    const layoutDefinition = normalizeLayoutDefinition(payload.layoutDefinitionJson);
    this.layoutRows = layoutDefinition.rows;
    this.inumColumnLayout = layoutDefinition.columnLayout;

    payload.controls.forEach(control => {
      if (typeof control.layoutRowIndex !== 'number' && typeof control.layoutColumnIndex !== 'number') {
        const rowIndex = (payload.controls.indexOf(control) % Math.max(1, this.layoutRows.length));
        const row = this.layoutRows[rowIndex] ?? this.layoutRows[0];
        const columnIndex = row && row.columns.length > 0 ? Math.min(row.columns.length - 1, Math.floor(payload.controls.indexOf(control) / Math.max(1, this.layoutRows.length))) : 0;
        control.layoutRowIndex = rowIndex;
        control.layoutColumnIndex = columnIndex;
      }
    });

    for (const c of payload.controls) {
      if (c.controlTypeCode === 'Label') continue;
      const defaultValue = c.controlTypeCode === 'CheckboxList' ? [] : (c.defaultValue ?? '');
      group[c.controlKey] = [defaultValue, []];
    }

    this.form = this.fb.group(group);

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

  controlsInRowColumn(rowIndex: number, columnIndex: number): FormControlDef[] {
    return this.payload?.controls.filter(control =>
      control.layoutRowIndex === rowIndex &&
      control.layoutColumnIndex === columnIndex
    ) ?? [];
  }

  labelHeadingLevel(control: FormControlDef): string {
    if (!control.propertiesJson) return 'h3';
    try {
      const properties = JSON.parse(control.propertiesJson);
      return properties.LabelHeadingLevel === 'normal' || /^h[1-6]$/.test(properties.LabelHeadingLevel)
        ? properties.LabelHeadingLevel
        : 'h3';
    } catch {
      return 'h3';
    }
  }

  hasSharedTopBorder(rowIndex: number): boolean {
    return rowIndex > 0
      && this.layoutRows[rowIndex]?.borderEnabled === true
      && this.layoutRows[rowIndex - 1]?.borderEnabled === true;
  }

  columnFlexBasis(span: number, columnCount: number): string {
    const gapSharePx = Math.max(0, columnCount - 1) * span;
    return `0 0 calc(${(span / 12) * 100}% - ${gapSharePx}px)`;
  }

  private buildValidatorsFor(aObjControl: FormControlDef, aArrRules: FormRule[], aBoolRequired: boolean): ValidatorFn[] {
    const larrConditional: string[] = ['Visibility', 'EnableDisable', 'RequiredOptional'];

    const larrValidators: ValidatorFn[] = aArrRules.filter(r => r.controlKey === aObjControl.controlKey &&
      !larrConditional.includes(r.ruleType) && r.ruleType !== 'Required').map(r => this.ruleEngine.buildValidator(
        r, key => this.form.get(key)?.value));

    if (aBoolRequired) {
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

  private recomputeEffects(payload: FormRenderPayload): void {
    this.iobjEffects = this.ruleEngine.computeEffects(payload.rules, this.form.getRawValue(), payload.controls);

    if (this.iboolReadOnly) return;

    for (const c of payload.controls) {
      if (c.controlTypeCode === 'Label') continue;

      const lobjEffect = this.iobjEffects[c.controlKey];
      const ctrl = this.form.get(c.controlKey);
      if (!lobjEffect || !ctrl) continue;

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

      if (lobjEffect.options && ctrl.value && !lobjEffect.options.includes(String(ctrl.value))) {
        ctrl.setValue('', { emitEvent: false });
      }

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

  onFileSelected(controlKey: string, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const lstrError = this.ruleEngine.validateFile(this.payload?.rules ?? [], controlKey, file);
    if (lstrError) {
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
    lobjFormData.append('values', JSON.stringify(this.form.value));

    for (const controlKey of Object.keys(this.selectedFiles)) {
      lobjFormData.append(controlKey, this.selectedFiles[controlKey]);
    }

    this.submissionService.submit(this.istrPublicId, lobjFormData).subscribe(res => {
      if (res.success) {
        this.submitted = true;
      } else {
        this.serverErrors = res.errors ?? [res.message ?? 'Submission failed.'];
      }
    });
  }

  storedFileName(aStrControlKey: string): string {
    const lobjValue = this.form.get(aStrControlKey)?.value;
    return typeof lobjValue === 'string' ? lobjValue : '';
  }

  fileUrl(aStrStoredFileName: string): string {
    return `${environment.apiUrl}/files/${encodeURIComponent(aStrStoredFileName)}`;
  }

  downloadUrl(aStrStoredFileName: string): string {
    return `${this.fileUrl(aStrStoredFileName)}?download=true`;
  }

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

  controlsIn(aStrSectionKey: string): FormControlDef[] {
    return this.iobjSectionControls[aStrSectionKey] ?? [];
  }

  trackByControlKey(aNumIndex: number, aObjControl: FormControlDef): string {
    return aObjControl.controlKey;
  }

  isSectionVisible(aStrSectionKey: string): boolean {
    return this.ruleEngine.isSectionVisible(aStrSectionKey);
  }
}