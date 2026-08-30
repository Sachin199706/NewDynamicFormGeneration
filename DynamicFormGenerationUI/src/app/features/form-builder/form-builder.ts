import { Component, OnInit } from '@angular/core';
import { ControlType, FormControlDef } from '../../core/models/form.model';
import { ControlTypeService } from '../../core/services/control-type';
import { FormService } from '../../core/services/form';
import { FormRule } from '../../core/models/rule.model';
import { RuleService } from '../../core/services/rule';
import { RuleEngineService } from '../../core/services/rule-engine';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { Observable, forkJoin, of } from 'rxjs';

interface CanvasControl extends FormControlDef {
  tempId: string;
}

@Component({
  selector: 'app-form-builder',
  imports: [CommonModule, FormsModule, RouterLink, DragDropModule],
  templateUrl: './form-builder.html',
  styleUrl: './form-builder.scss',
})
export class FormBuilder implements OnInit {

  inumFormId: number | null = null;
  inumVersionId: number | null = null;
  istrFormName = '';
  iarrControlTypes: ControlType[] = [];
  iarrCanvasControls: CanvasControl[] = [];
  iobjSelected: CanvasControl | null = null;
  inumColumnLayout = 1;

  iboolPreviewOpen = false;
  iarrPreviewRules: FormRule[] = [];
  iobjPreviewValues: Record<string, any> = {};
  iobjPreviewVisibility: Record<string, boolean> = {};
  iobjPreviewErrors: Record<string, string> = {};
  iobjSelectedFiles: Record<string, File> = {};
  iobjPreviewImageUrls: Record<string, string> = {};

  iboolPublished = false;
  istrPublishError = '';

  iobjControlRuleValues: Record<string, any> = {};

  constructor(private iobjControlTypeService: ControlTypeService, private iobjFormService: FormService, private iobjRuleService: RuleService, private iobjRuleEngine: RuleEngineService, private iobjRoute: ActivatedRoute, private iobjRouter: Router) { }

  ngOnInit(): void {
    this.iobjControlTypeService.getAll().subscribe(types => this.iarrControlTypes = types);

    const lStrIdParam = this.iobjRoute.snapshot.paramMap.get('formId');
    const lStrVersionParam = this.iobjRoute.snapshot.queryParamMap.get('version');

    if (lStrIdParam) {
      this.inumFormId = Number(lStrIdParam);

      const lobjVersionLoad$ = lStrVersionParam
        ? this.iobjFormService.getVersionById(Number(lStrVersionParam))
        : this.iobjFormService.getLatestVersion(this.inumFormId);

      lobjVersionLoad$.subscribe(res => {
        if (res.success && res.data) {
          this.istrFormName = res.data.formName;
          this.inumVersionId = res.data.formVersionId;
          this.iarrCanvasControls = res.data.controls.map(c => ({ ...c, tempId: crypto.randomUUID() }));

          if (res.data.layoutDefinitionJson) {
            try {
              const lobjLayout = JSON.parse(res.data.layoutDefinitionJson);
              if (lobjLayout.columnLayout) this.inumColumnLayout = lobjLayout.columnLayout;
            } catch { /* ignore malformed layout, default stays 1 */ }
          }

          this.loadExistingRulesIntoPanel(res.data.formVersionId);
        }
      });
    }
  }

  drop(aObjEvent: CdkDragDrop<any>): void {
    if (aObjEvent.previousContainer === aObjEvent.container) {
      moveItemInArray(this.iarrCanvasControls, aObjEvent.previousIndex, aObjEvent.currentIndex);
    } else {
      const lobjCt: ControlType = aObjEvent.item.data;
      const lobjNewControl: CanvasControl = {
        tempId: crypto.randomUUID(),
        controlKey: `${lobjCt.controlCode.toLowerCase()}_${Date.now()}`,
        controlTypeCode: lobjCt.controlCode,
        label: lobjCt.controlName,
        placeholder: `Enter ${lobjCt.controlName.toLowerCase()}`,
        isRequired: false,
        isReadOnly: false,
        isVisible: true,
        displayOrder: 0
      };
      this.iarrCanvasControls.splice(aObjEvent.currentIndex, 0, lobjNewControl);
      this.select(lobjNewControl);
    }

    this.iarrCanvasControls.forEach((c, i) => c.displayOrder = i);
  }

  select(aObjC: CanvasControl): void {
    this.iobjSelected = aObjC;
  }

  removeSelected(): void {
    if (!this.iobjSelected) return;
    this.iarrCanvasControls = this.iarrCanvasControls.filter(c => c !== this.iobjSelected);
    this.iobjSelected = null;
  }

  get selectedNeedsOptions(): boolean {
    return this.iobjSelected?.controlTypeCode === 'Dropdown'
      || this.iobjSelected?.controlTypeCode === 'Radio'
      || this.iobjSelected?.controlTypeCode === 'CheckboxList';
  }

  get selectedOptionsText(): string {
    if (!this.iobjSelected?.propertiesJson) return '';
    try {
      const lobjProps = JSON.parse(this.iobjSelected.propertiesJson);
      return typeof lobjProps.SeedData === 'string' ? lobjProps.SeedData : '';
    } catch { return ''; }
  }

  onOptionsChange(aStrValue: string): void {
    if (!this.iobjSelected) return;
    let lobjProps: any = {};
    if (this.iobjSelected.propertiesJson) {
      try { lobjProps = JSON.parse(this.iobjSelected.propertiesJson); } catch { lobjProps = {}; }
    }
    lobjProps.SeedData = aStrValue;
    this.iobjSelected.propertiesJson = JSON.stringify(lobjProps);
  }

  get selectedIsCheckbox(): boolean {
    return this.iobjSelected?.controlTypeCode === 'Checkbox';
  }

  get selectedCheckboxText(): string {
    if (!this.iobjSelected?.propertiesJson) return '';
    try {
      const lobjProps = JSON.parse(this.iobjSelected.propertiesJson);
      return typeof lobjProps.CheckboxText === 'string' ? lobjProps.CheckboxText : '';
    } catch { return ''; }
  }

  onCheckboxTextChange(aStrValue: string): void {
    if (!this.iobjSelected) return;
    let lobjProps: any = {};
    if (this.iobjSelected.propertiesJson) {
      try { lobjProps = JSON.parse(this.iobjSelected.propertiesJson); } catch { lobjProps = {}; }
    }
    lobjProps.CheckboxText = aStrValue;
    this.iobjSelected.propertiesJson = JSON.stringify(lobjProps);
  }

  get selectedRuleKind(): 'text' | 'number' | 'date' | 'none' {
    switch (this.iobjSelected?.controlTypeCode) {
      case 'TextBox': return 'text';
      case 'Number': return 'number';
      case 'Date': return 'date';
      default: return 'none';
    }
  }

  get ruleMinLength(): number | null {
    return this.iobjSelected ? (this.iobjControlRuleValues[this.iobjSelected.controlKey]?.minLength ?? null) : null;
  }
  set ruleMinLength(aNumValue: number | null) {
    if (!this.iobjSelected) return;
    this.setRuleValue(this.iobjSelected.controlKey, 'minLength', aNumValue);
  }

  get ruleMaxLength(): number | null {
    return this.iobjSelected ? (this.iobjControlRuleValues[this.iobjSelected.controlKey]?.maxLength ?? null) : null;
  }
  set ruleMaxLength(aNumValue: number | null) {
    if (!this.iobjSelected) return;
    this.setRuleValue(this.iobjSelected.controlKey, 'maxLength', aNumValue);
  }

  get ruleRangeMin(): number | null {
    return this.iobjSelected ? (this.iobjControlRuleValues[this.iobjSelected.controlKey]?.rangeMin ?? null) : null;
  }
  set ruleRangeMin(aNumValue: number | null) {
    if (!this.iobjSelected) return;
    this.setRuleValue(this.iobjSelected.controlKey, 'rangeMin', aNumValue);
  }

  get ruleRangeMax(): number | null {
    return this.iobjSelected ? (this.iobjControlRuleValues[this.iobjSelected.controlKey]?.rangeMax ?? null) : null;
  }
  set ruleRangeMax(aNumValue: number | null) {
    if (!this.iobjSelected) return;
    this.setRuleValue(this.iobjSelected.controlKey, 'rangeMax', aNumValue);
  }

  get ruleDateOperator(): string {
    return this.iobjSelected ? (this.iobjControlRuleValues[this.iobjSelected.controlKey]?.dateOperator ?? '') : '';
  }
  set ruleDateOperator(aStrValue: string) {
    if (!this.iobjSelected) return;
    this.setRuleValue(this.iobjSelected.controlKey, 'dateOperator', aStrValue);
  }

  private setRuleValue(aStrControlKey: string, aStrField: string, aObjValue: any): void {
    if (!this.iobjControlRuleValues[aStrControlKey]) this.iobjControlRuleValues[aStrControlKey] = {};
    this.iobjControlRuleValues[aStrControlKey][aStrField] = aObjValue;
  }

  /// Rules are keyed directly by controlKey now — no more controlId lookup needed.
  private loadExistingRulesIntoPanel(aNumVersionId: number): void {
    this.iobjRuleService.getRules(aNumVersionId).subscribe(rules => {
      for (const rule of rules) {
        if (!rule.isActive) continue;

        let lobjDetails: any = {};
        try { lobjDetails = rule.ruleDetailsJson ? JSON.parse(rule.ruleDetailsJson) : {}; } catch { lobjDetails = {}; }

        switch (rule.ruleType) {
          case 'MinLength': this.setRuleValue(rule.controlKey, 'minLength', lobjDetails.min); break;
          case 'MaxLength': this.setRuleValue(rule.controlKey, 'maxLength', lobjDetails.max); break;
          case 'Range':
            this.setRuleValue(rule.controlKey, 'rangeMin', lobjDetails.min);
            this.setRuleValue(rule.controlKey, 'rangeMax', lobjDetails.max);
            break;
          case 'Date': this.setRuleValue(rule.controlKey, 'dateOperator', lobjDetails.operator); break;
        }
      }
    });
  }

  save(): void {
    const lobjDto = {
      formId: this.inumFormId,
      formName: this.istrFormName || 'Untitled Form',
      formDefinitionJson: JSON.stringify({ controls: this.iarrCanvasControls }),
      layoutDefinitionJson: JSON.stringify({ columnLayout: this.inumColumnLayout }),
      controls: this.iarrCanvasControls.map(({ tempId, ...rest }) => rest)
    };

    this.iobjFormService.saveVersion(lobjDto).subscribe(res => {
      if (res.success && res.data) {
        this.saveInlineRules(res.data.formVersionId).subscribe(() => {
          this.iobjRouter.navigate(['/dashboard']);
        });
      }
    });
  }

  /// controlKey is already known client-side and is stable — no need to wait for a
  /// database-assigned ControlId anymore, so rules can be created directly.
  private saveInlineRules(aNumVersionId: number): Observable<any> {
    const larrRequests: Observable<any>[] = [];

    for (const lstrKey of Object.keys(this.iobjControlRuleValues)) {
      const lobjValues = this.iobjControlRuleValues[lstrKey];

      if (lobjValues.minLength != null) {
        larrRequests.push(this.iobjRuleService.addRule(aNumVersionId, {
          controlKey: lstrKey, ruleType: 'MinLength',
          ruleDetailsJson: JSON.stringify({ min: lobjValues.minLength }),
          errorMessage: `Minimum length is ${lobjValues.minLength}.`, severity: 'Error', displayOrder: 0
        }));
      }
      if (lobjValues.maxLength != null) {
        larrRequests.push(this.iobjRuleService.addRule(aNumVersionId, {
          controlKey: lstrKey, ruleType: 'MaxLength',
          ruleDetailsJson: JSON.stringify({ max: lobjValues.maxLength }),
          errorMessage: `Maximum length is ${lobjValues.maxLength}.`, severity: 'Error', displayOrder: 0
        }));
      }
      if (lobjValues.rangeMin != null || lobjValues.rangeMax != null) {
        larrRequests.push(this.iobjRuleService.addRule(aNumVersionId, {
          controlKey: lstrKey, ruleType: 'Range',
          ruleDetailsJson: JSON.stringify({ min: lobjValues.rangeMin, max: lobjValues.rangeMax }),
          errorMessage: `Value must be between ${lobjValues.rangeMin} and ${lobjValues.rangeMax}.`, severity: 'Error', displayOrder: 0
        }));
      }
      if (lobjValues.dateOperator) {
        larrRequests.push(this.iobjRuleService.addRule(aNumVersionId, {
          controlKey: lstrKey, ruleType: 'Date',
          ruleDetailsJson: JSON.stringify({ operator: lobjValues.dateOperator }),
          errorMessage: `Date is invalid.`, severity: 'Error', displayOrder: 0
        }));
      }
    }

    return larrRequests.length ? forkJoin(larrRequests) : of(null);
  }

  publish(): void {
    if (!this.inumFormId || !this.inumVersionId) return;
    this.istrPublishError = '';

    this.iobjFormService.publish(this.inumFormId, this.inumVersionId).subscribe({
      next: (res) => {
        if (res.success) {
          this.iobjRouter.navigate(['/forms']);
        } else {
          this.istrPublishError = res.message ?? 'Publish failed.';
        }
      },
      error: (err) => {
        this.istrPublishError = 'Publish failed. Check the console for details.';
        console.error('Publish failed:', err);
      }
    });
  }

  openPreview(): void {
    this.iobjPreviewValues = {};
    this.iobjPreviewVisibility = {};
    this.iarrCanvasControls.forEach(c => this.iobjPreviewValues[c.controlKey] = c.defaultValue ?? '');

    this.iarrPreviewRules = this.buildInMemoryRules();
    this.recomputePreviewVisibility();
    this.recomputePreviewErrors();
    this.iboolPreviewOpen = true;
  }

  closePreview(): void {
    this.iboolPreviewOpen = false;
  }

  onPreviewChange(aStrControlKey: string, aObjValue: any): void {
    this.iobjPreviewValues[aStrControlKey] = aObjValue;
    this.recomputePreviewVisibility();
    this.recomputePreviewErrors();
  }

  isPreviewCheckboxListSelected(aStrKey: string, aStrOpt: string): boolean {
    const larrCurrent: string[] = this.iobjPreviewValues[aStrKey] ?? [];
    return larrCurrent.includes(aStrOpt);
  }

  togglePreviewCheckboxListOption(aStrKey: string, aStrOpt: string): void {
    const larrCurrent: string[] = this.iobjPreviewValues[aStrKey] ?? [];
    const larrNext = larrCurrent.includes(aStrOpt) ? larrCurrent.filter(v => v !== aStrOpt) : [...larrCurrent, aStrOpt];
    this.onPreviewChange(aStrKey, larrNext);
  }

  onPreviewFileChange(aStrControlKey: string, aObjEvent: Event): void {
    const lobjInput = aObjEvent.target as HTMLInputElement;
    const lobjFile = lobjInput.files?.[0];
    if (!lobjFile) return;

    this.onPreviewChange(aStrControlKey, lobjFile.name);

    if (lobjFile.type.startsWith('image/')) {
      const lobjReader = new FileReader();
      lobjReader.onload = () => {
        this.iobjPreviewImageUrls[aStrControlKey] = lobjReader.result as string;
      };
      lobjReader.readAsDataURL(lobjFile);
    }
  }

  private recomputePreviewVisibility(): void {
    this.iobjPreviewVisibility = this.iobjRuleEngine.computeVisibility(this.iarrPreviewRules, this.iobjPreviewValues);
  }

  private recomputePreviewErrors(): void {
    const lobjResult = this.iobjRuleEngine.evaluateAll(this.iarrPreviewRules, this.iobjPreviewValues);
    const lobjErrors: Record<string, string> = {};

    for (const lobjFailure of lobjResult.failures) {
      if (!lobjErrors[lobjFailure.controlKey]) {
        lobjErrors[lobjFailure.controlKey] = lobjFailure.errorMessage;
      }
    }

    this.iobjPreviewErrors = lobjErrors;
  }

  isEffectivelyRequired(aObjC: CanvasControl): boolean {
    if (aObjC.isRequired) return true;
    return this.iarrPreviewRules.some(r => r.isActive && r.controlKey === aObjC.controlKey && r.ruleType === 'Required');
  }

  /// Required (from the checkbox) + MinLength/MaxLength/Range/Date (from the inline
  /// Properties panel) — client-side only, no round trip, so Preview works even on
  /// a form that's never been saved. Visibility/CrossField still only appear once
  /// the form has been saved at least once (configured via the Rule Builder screen).
  private buildInMemoryRules(): FormRule[] {
    const larrRules: FormRule[] = [];

    for (const c of this.iarrCanvasControls) {
      if (c.isRequired) {
        larrRules.push({
          controlKey: c.controlKey, ruleType: 'Required', ruleDetailsJson: undefined,
          errorMessage: 'This field is required.', severity: 'Error', displayOrder: 0, isActive: true
        });
      }

      const lobjValues = this.iobjControlRuleValues[c.controlKey];
      if (!lobjValues) continue;

      if (lobjValues.minLength != null) {
        larrRules.push({
          controlKey: c.controlKey, ruleType: 'MinLength', ruleDetailsJson: JSON.stringify({ min: lobjValues.minLength }),
          errorMessage: `Minimum length is ${lobjValues.minLength}.`, severity: 'Error', displayOrder: 0, isActive: true
        });
      }
      if (lobjValues.maxLength != null) {
        larrRules.push({
          controlKey: c.controlKey, ruleType: 'MaxLength', ruleDetailsJson: JSON.stringify({ max: lobjValues.maxLength }),
          errorMessage: `Maximum length is ${lobjValues.maxLength}.`, severity: 'Error', displayOrder: 0, isActive: true
        });
      }
      if (lobjValues.rangeMin != null || lobjValues.rangeMax != null) {
        larrRules.push({
          controlKey: c.controlKey, ruleType: 'Range', ruleDetailsJson: JSON.stringify({ min: lobjValues.rangeMin, max: lobjValues.rangeMax }),
          errorMessage: `Value must be between ${lobjValues.rangeMin} and ${lobjValues.rangeMax}.`, severity: 'Error', displayOrder: 0, isActive: true
        });
      }
      if (lobjValues.dateOperator) {
        larrRules.push({
          controlKey: c.controlKey, ruleType: 'Date', ruleDetailsJson: JSON.stringify({ operator: lobjValues.dateOperator }),
          errorMessage: `Date is invalid.`, severity: 'Error', displayOrder: 0, isActive: true
        });
      }
    }

    return larrRules;
  }

  seedOptions(aObjC: CanvasControl): string[] {
    if (!aObjC.propertiesJson) return [];
    try {
      const lobjProps = JSON.parse(aObjC.propertiesJson);
      return typeof lobjProps.SeedData === 'string' ? lobjProps.SeedData.split(',') : [];
    } catch { return []; }
  }
}