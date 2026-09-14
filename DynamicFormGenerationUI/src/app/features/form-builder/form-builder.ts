import { Component, OnInit } from '@angular/core';
import { ControlType, FormControlDef } from '../../core/models/form.model';
import { ControlTypeService } from '../../core/services/control-type';
import { FormService } from '../../core/services/form';
import { ControlEffects, FormRule } from '../../core/models/rule.model';
import { RuleService } from '../../core/services/rule';
import { RuleEngineService } from '../../core/services/rule-engine';
import { ActivatedRoute, Router,} from '@angular/router';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { ToastrService } from 'ngx-toastr';

interface CanvasControl extends FormControlDef {
  tempId: string;
}

@Component({
  selector: 'app-form-builder', imports: [CommonModule, FormsModule,DragDropModule], templateUrl: './form-builder.html',
  styleUrl: './form-builder.scss',
})
export class FormBuilder implements OnInit {
  inumTemplateId: number | null = null;
  inumVersionId: number | null = null;
  istrVersionDescription = '';
  istrTemplateName = '';
  iarrControlTypes: ControlType[] = [];
  iarrCanvasControls: CanvasControl[] = [];
  iobjSelected: CanvasControl | null = null;
  inumColumnLayout = 1;

  iboolPreviewOpen = false;
  iarrPreviewRules: FormRule[] = [];
  iobjPreviewValues: Record<string, any> = {};
  /** Per-control state after all conditional rules have run. */
  iobjPreviewEffects: Record<string, ControlEffects> = {};
  iobjPreviewErrors: Record<string, string> = {};
  iobjSelectedFiles: Record<string, File> = {};
  iobjPreviewImageUrls: Record<string, string> = {};
  iobjPreviewTouched: Record<string, boolean> = {};

  iboolPublished = false;
  istrPublishError = '';
  iboolDirty = false;
  /** Rules loaded from the saved version — conditional ones are only available from here. */

  constructor(private iobjControlTypeService: ControlTypeService, private iobjFormService: FormService,
    private iobjRuleEngine: RuleEngineService, private iobjRoute: ActivatedRoute, private iobjRouter: Router, private toastr: ToastrService
  ) { }

  //#region  Lifecycle Method
  ngOnInit(): void {
    this.iobjControlTypeService.getAll().subscribe(types => this.iarrControlTypes = types);

    const lStrIdParam = this.iobjRoute.snapshot.paramMap.get('tid');
    const lStrTemplateName = this.iobjRoute.snapshot.queryParamMap.get('tname');
    const lStrVersionParam = this.iobjRoute.snapshot.queryParamMap.get('version');

    if (lStrIdParam) {
      this.inumTemplateId = Number(lStrIdParam);
      if (lStrTemplateName) this.istrTemplateName = String(lStrTemplateName);

      const lobjVersionLoad$ = this.iobjFormService.getVersionById(Number(lStrVersionParam));

      lobjVersionLoad$.subscribe(res => {
        if (res.success && res.data) {
          this.istrVersionDescription = res.data.versionDescription;
          this.inumVersionId = res.data.formVersionId;
          this.iarrCanvasControls = res.data.controls.map(c => ({ ...c, tempId: crypto.randomUUID() }));

          if (res.data.layoutDefinitionJson) {
            try {
              const lobjLayout = JSON.parse(res.data.layoutDefinitionJson);
              if (lobjLayout.columnLayout) this.inumColumnLayout = lobjLayout.columnLayout;
            } catch { /* ignore malformed layout, default stays 1 */ }
          }
        }
      });
    }
  }
  //#endregion

  //#region Get Set Method
  get selectedIsRequired(): boolean {

    return (this.iobjSelected?.rules ?? []).some(r => r.isActive && r.ruleType === 'Required');
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
  get selectedRuleKind(): 'text' | 'number' | 'date' | 'none' {
    switch (this.iobjSelected?.controlTypeCode) {
      case 'TextBox': return 'text';
      case 'Number': return 'number';
      case 'Date': return 'date';
      default: return 'none';
    }
  }

  set selectedIsRequired(aBoolValue: boolean) {
    if (!this.iobjSelected) return;
    this.iboolDirty=true;
    const larrRules = this.iobjSelected.rules ?? [];

    if (!aBoolValue) {
      this.iobjSelected.rules = larrRules.filter(r => r.ruleType !== 'Required');
      return;
    }
    if (larrRules.some(r => r.ruleType === 'Required')) return;   // keep the author's message

    this.iobjSelected.rules = [...larrRules, {
      ruleId: crypto.randomUUID(),
      controlKey: this.iobjSelected.controlKey,
      ruleType: 'Required',
      errorMessage: `${this.iobjSelected.label} is required.`,
      severity: 'Error',
      displayOrder: larrRules.length,
      isActive: true
    }];
  }

  //#endregion

  //#region Public method
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
     this.iboolDirty = true;
  }
  select(aObjC: CanvasControl): void {
    this.iobjSelected = aObjC;
  }
  removeSelected(): void {
    if (!this.iobjSelected) return;
    this.iarrCanvasControls = this.iarrCanvasControls.filter(c => c !== this.iobjSelected);
    this.iobjSelected = null;
    this.iboolDirty=true;

  }
  onOptionsChange(aStrValue: string): void {
    if (!this.iobjSelected) return;
    let lobjProps: any = {};
    if (this.iobjSelected.propertiesJson) {
      try { lobjProps = JSON.parse(this.iobjSelected.propertiesJson); } catch { lobjProps = {}; }
    }
    lobjProps.SeedData = aStrValue;
    this.iobjSelected.propertiesJson = JSON.stringify(lobjProps);
    this.iboolDirty=true;
  }
  onCheckboxTextChange(aStrValue: string): void {
    if (!this.iobjSelected) return;
    let lobjProps: any = {};
    if (this.iobjSelected.propertiesJson) {
      try { lobjProps = JSON.parse(this.iobjSelected.propertiesJson); } catch { lobjProps = {}; }
    }
    lobjProps.CheckboxText = aStrValue;
    this.iobjSelected.propertiesJson = JSON.stringify(lobjProps);
    this.iboolDirty=true;
  }
  save(aFnOnSaved?: () => void): void {
    const larrControlsWithRules = this.iarrCanvasControls.map(({ tempId, ...rest }) => ({
      ...rest,
      rules: rest.rules ?? []
    }));

    const lobjDto = {
      formId: this.inumTemplateId,
      formVersionId: this.inumVersionId,
      versionDescription: this.istrVersionDescription || 'Untitled Form',
      formDefinitionJson: JSON.stringify({ controls: larrControlsWithRules }),
      layoutDefinitionJson: JSON.stringify({ columnLayout: this.inumColumnLayout }),
      controls: larrControlsWithRules
    };

    this.iobjFormService.saveVersion(lobjDto).subscribe(res => {
      if (res.success && res.data) {
        this.inumTemplateId = res.data.formId;
        this.inumVersionId = res.data.formVersionId;
        this.iboolDirty = false;
        this.toastr.success('Form version saved successfully.', 'Success');
         aFnOnSaved?.();
      }
    });
  }
  publish(): void {
    if (!this.inumTemplateId || !this.inumVersionId) return;
    this.istrPublishError = '';

    this.iobjFormService.publish(this.inumTemplateId, this.inumVersionId).subscribe({
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
  seedOptions(aObjC: CanvasControl): string[] {
    if (!aObjC.propertiesJson) return [];
    try {
      const lobjProps = JSON.parse(aObjC.propertiesJson);
      return typeof lobjProps.SeedData === 'string' ? lobjProps.SeedData.split(',') : [];
    } catch { return []; }
  }
  isEffectivelyRequired(aObjC: CanvasControl): boolean {
    return this.iobjPreviewEffects[aObjC.controlKey]?.required === true;
  }
   /// Reads the control's own rules, the same source Preview now uses.
  isRequiredOnCanvas(aObjC: CanvasControl): boolean {
    return (aObjC.rules ?? []).some(r => r.isActive && r.ruleType === 'Required');
  }
    goToRules(): void {
    const lstrTarget = 'Validation Rules';

    if (!this.iboolDirty) {
      this.navigateToRules();
      return;
    }

    if (confirm(`You have unsaved changes. Save the form before opening ${lstrTarget}?`)) {
      this.save(() => this.navigateToRules());
    }
  }
  onLabelChange(aStrValue: string): void {
    if (!this.iobjSelected) return;
    this.iobjSelected.label = aStrValue;
    this.iboolDirty = true;
  }

  onPlaceholderChange(aStrValue: string): void {
    if (!this.iobjSelected) return;
    this.iobjSelected.placeholder = aStrValue;
    this.iboolDirty = true;
  }
  //#region  Preview Method
  openPreview(): void {
    this.iobjPreviewValues = {};
    this.iobjPreviewEffects = {};
    this.iobjPreviewTouched = {};
    this.iarrCanvasControls.forEach(c => this.iobjPreviewValues[c.controlKey] = c.defaultValue ?? '');

    this.iarrPreviewRules = this.buildInMemoryRules();
    this.recomputePreviewEffects();
    this.recomputePreviewErrors();
    this.iboolPreviewOpen = true;
  }

  closePreview(): void {
    this.iboolPreviewOpen = false;
  }

  onPreviewChange(aStrControlKey: string, aObjValue: any): void {
    this.iobjPreviewValues[aStrControlKey] = aObjValue;
    this.iobjPreviewTouched[aStrControlKey] = true;
    this.recomputePreviewEffects();
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

    // File rules are checked here rather than in the rule engine: the value map holds the
    // file name, so size is invisible to evaluateOne(). A rejected file is cleared, or its
    // name would stay in the value map and be treated as a valid entry.
    const lstrError = this.iobjRuleEngine.validateFile(this.iarrPreviewRules, aStrControlKey, lobjFile);
    this.iobjPreviewTouched[aStrControlKey] = true;

    if (lstrError) {
      lobjInput.value = '';
      delete this.iobjPreviewImageUrls[aStrControlKey];
      this.iobjPreviewValues[aStrControlKey] = '';
      this.iobjPreviewErrors[aStrControlKey] = lstrError;
      return;
    }

    this.onPreviewChange(aStrControlKey, lobjFile.name);

    if (lobjFile.type.startsWith('image/')) {
      const lobjReader = new FileReader();
      lobjReader.onload = () => {
        this.iobjPreviewImageUrls[aStrControlKey] = lobjReader.result as string;
      };
      lobjReader.readAsDataURL(lobjFile);
    }
  }
  isPreviewVisible(aStrControlKey: string): boolean {
    return this.iobjPreviewEffects[aStrControlKey]?.visible !== false;
  }

  isPreviewEnabled(aStrControlKey: string): boolean {
    return this.iobjPreviewEffects[aStrControlKey]?.enabled !== false;
  }
  markPreviewTouched(aStrControlKey: string): void {
    this.iobjPreviewTouched[aStrControlKey] = true;
  }

  /** The message to show, or '' while the field is still untouched. */
  previewError(aStrControlKey: string): string {
    return this.iobjPreviewTouched[aStrControlKey]
      ? (this.iobjPreviewErrors[aStrControlKey] ?? '')
      : '';
  }
  //#endregion

  //#endregion
  //#region Private Method
  private recomputePreviewEffects(): void {
    this.iobjPreviewEffects = this.iobjRuleEngine.computeEffects(
      this.iarrPreviewRules, this.iobjPreviewValues, this.iarrCanvasControls
    );
  }


  private recomputePreviewErrors(): void {
    const lobjResult = this.iobjRuleEngine.evaluateAll(this.iarrPreviewRules, this.iobjPreviewValues);
    const lobjErrors: Record<string, string> = {};

    for (const lobjFailure of lobjResult.failures) {
      // A control the user cannot see or edit is not held to its rules, so Preview must
      // skip these or it flags errors on fields that aren't on screen.
      const lobjEffect = this.iobjPreviewEffects[lobjFailure.controlKey];
      if (lobjEffect && (!lobjEffect.visible || !lobjEffect.enabled)) continue;

      if (!lobjErrors[lobjFailure.controlKey]) {
        lobjErrors[lobjFailure.controlKey] = lobjFailure.errorMessage;
      }
    }

    this.iobjPreviewErrors = lobjErrors;
  }
   private buildInMemoryRules(): FormRule[] {
    return this.iarrCanvasControls
      .flatMap(c => (c.rules ?? []).map(r => ({ ...r, controlKey: c.controlKey })))
      .filter(r => r.isActive);
  }
  private navigateToRules(): void {
    this.iobjRouter.navigate(['/forms', this.inumTemplateId, 'versions', this.inumVersionId, 'rules']);
  }
  //#endregion
}