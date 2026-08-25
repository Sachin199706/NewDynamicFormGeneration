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
  iobjSelectedFiles: Record<string, File> = {};

  constructor(private iobjControlTypeService: ControlTypeService, private iobjFormService: FormService, private iobjRuleService: RuleService, private iobjRuleEngine: RuleEngineService, private iobjRoute: ActivatedRoute, private iobjRouter: Router) { }

  ngOnInit(): void {
    this.iobjControlTypeService.getAll().subscribe(types => this.iarrControlTypes = types);

    const lStrIdParam = this.iobjRoute.snapshot.paramMap.get('formId');
    if (lStrIdParam) {
      this.inumFormId = Number(lStrIdParam);
      this.iobjFormService.getLatestVersion(this.inumFormId).subscribe(res => {
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
        }
      });
    }
  }

  drop(aObjEvent: CdkDragDrop<any>): void {
    if (aObjEvent.previousContainer === aObjEvent.container) {
      moveItemInArray(this.iarrCanvasControls, aObjEvent.previousIndex, aObjEvent.currentIndex);
      this.iarrCanvasControls.forEach((c, i) => c.displayOrder = i);
      return;
    }

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
      displayOrder: aObjEvent.currentIndex
    };
    this.iarrCanvasControls.splice(aObjEvent.currentIndex, 0, lobjNewControl);
    this.select(lobjNewControl);
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

  save(): void {
    const lobjDto = {
      formId: this.inumFormId,
      formName: this.istrFormName || 'Untitled Form',
      formDefinitionJson: JSON.stringify({ controls: this.iarrCanvasControls }),
      layoutDefinitionJson: JSON.stringify({ columnLayout: this.inumColumnLayout }),
      controls: this.iarrCanvasControls.map(({ tempId, ...rest }) => rest),
      layouts: []
    };

    this.iobjFormService.saveVersion(lobjDto).subscribe(res => {
      if (res.success && res.data) {
        this.inumFormId = res.data.formId;
        this.inumVersionId = res.data.formVersionId;
        this.iobjRouter.navigate(['/forms/builder', this.inumFormId], { replaceUrl: true });
      }
    });
  }

  publish(): void {
    if (!this.inumFormId || !this.inumVersionId) return;
    this.iobjFormService.publish(this.inumFormId, this.inumVersionId).subscribe();
  }

  /**
   * Preview reads the SAME JSON the end user's fill-in screen would get:
   * canvasControls (already in memory, no round trip needed) + FormRules fetched
   * for the current saved version. Rules only exist once a version has been saved,
   * so an unsaved brand-new form previews with no visibility/validation rules yet —
   * save it first to preview conditional show/hide.
   */
  openPreview(): void {
    this.iobjPreviewValues = {};
    this.iobjPreviewVisibility = {};
    this.iarrCanvasControls.forEach(c => this.iobjPreviewValues[c.controlKey] = c.defaultValue ?? '');

    if (this.inumVersionId) {
      this.iobjRuleService.getRules(this.inumVersionId).subscribe(rules => {
        this.iarrPreviewRules = rules;
        this.recomputePreviewVisibility();
        this.iboolPreviewOpen = true;
      });
    } else {
      this.iarrPreviewRules = [];
      this.recomputePreviewVisibility();
      this.iboolPreviewOpen = true;
    }
  }

  closePreview(): void {
    this.iboolPreviewOpen = false;
  }

  onPreviewChange(aStrControlKey: string, aObjValue: any): void {
    this.iobjPreviewValues[aStrControlKey] = aObjValue;
    this.recomputePreviewVisibility();
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

  private recomputePreviewVisibility(): void {
    const lobjControlKeyById: Record<number, string> = {};
    this.iarrCanvasControls.forEach(c => { if (c.controlId) lobjControlKeyById[c.controlId] = c.controlKey; });

    this.iobjPreviewVisibility = this.iobjRuleEngine.computeVisibility(
      this.iarrPreviewRules, this.iobjPreviewValues, lobjControlKeyById);
  }

  seedOptions(aObjC: CanvasControl): string[] {
    if (!aObjC.propertiesJson) return [];
    try {
      const lobjProps = JSON.parse(aObjC.propertiesJson);
      return typeof lobjProps.SeedData === 'string' ? lobjProps.SeedData.split(',') : [];
    } catch { return []; }
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

  onFileSelected(aStrControlKey: string, aObjEvent: Event): void {
    const lobjInput = aObjEvent.target as HTMLInputElement;
    const lobjFile = lobjInput.files?.[0];
    if (!lobjFile) return;

    // No upload endpoint exists yet (known gap, flagged earlier) — this only captures
    // the file client-side for now and stores its name as the form value.
    this.iobjSelectedFiles[aStrControlKey] = lobjFile;
  }
}