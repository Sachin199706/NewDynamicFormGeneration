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

  formId: number | null = null;
  versionId: number | null = null;
  formName = '';
  controlTypes: ControlType[] = [];
  canvasControls: CanvasControl[] = [];
  selected: CanvasControl | null = null;
  inumColumnLayout = 1;

  previewOpen = false;
  previewRules: FormRule[] = [];
  previewValues: Record<string, any> = {};
  previewVisibility: Record<string, boolean> = {};
  selectedFiles: Record<string, File> = {};

  constructor(private controlTypeService: ControlTypeService, private formService: FormService, private ruleService: RuleService, private ruleEngine: RuleEngineService, private route: ActivatedRoute, private router: Router) { }

  ngOnInit(): void {
    this.controlTypeService.getAll().subscribe(types => this.controlTypes = types);

    const idParam = this.route.snapshot.paramMap.get('formId');
    if (idParam) {
      this.formId = Number(idParam);
      this.formService.getLatestVersion(this.formId).subscribe(res => {
        if (res.success && res.data) {
          this.versionId = res.data.formVersionId;
          this.canvasControls = res.data.controls.map(c => ({ ...c, tempId: crypto.randomUUID() }));

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

  drop(event: CdkDragDrop<any>): void {
    if (event.previousContainer === event.container) {
      moveItemInArray(this.canvasControls, event.previousIndex, event.currentIndex);
      this.canvasControls.forEach((c, i) => c.displayOrder = i);
      return;
    }

    const ct: ControlType = event.item.data;
    const newControl: CanvasControl = {
      tempId: crypto.randomUUID(),
      controlKey: `${ct.controlCode.toLowerCase()}_${Date.now()}`,
      controlTypeCode: ct.controlCode,
      label: ct.controlName,
      placeholder: `Enter ${ct.controlName.toLowerCase()}`,
      isRequired: false,
      isReadOnly: false,
      isVisible: true,
      displayOrder: event.currentIndex
    };
    this.canvasControls.splice(event.currentIndex, 0, newControl);
    this.select(newControl);
  }

  select(c: CanvasControl): void {
    this.selected = c;
  }

  removeSelected(): void {
    if (!this.selected) return;
    this.canvasControls = this.canvasControls.filter(c => c !== this.selected);
    this.selected = null;
  }

  get selectedNeedsOptions(): boolean {
    return this.selected?.controlTypeCode === 'Dropdown'
      || this.selected?.controlTypeCode === 'Radio'
      || this.selected?.controlTypeCode === 'CheckboxList';
  }

  get selectedOptionsText(): string {
    if (!this.selected?.propertiesJson) return '';
    try {
      const props = JSON.parse(this.selected.propertiesJson);
      return typeof props.SeedData === 'string' ? props.SeedData : '';
    } catch { return ''; }
  }

  onOptionsChange(value: string): void {
    if (!this.selected) return;
    let props: any = {};
    if (this.selected.propertiesJson) {
      try { props = JSON.parse(this.selected.propertiesJson); } catch { props = {}; }
    }
    props.SeedData = value;
    this.selected.propertiesJson = JSON.stringify(props);
  }

  save(): void {
    const dto = {
      formId: this.formId,
      formName: this.formName || 'Untitled Form',
      formDefinitionJson: JSON.stringify({ controls: this.canvasControls }),
      layoutDefinitionJson: JSON.stringify({ columnLayout: this.inumColumnLayout }),
      controls: this.canvasControls.map(({ tempId, ...rest }) => rest),
      layouts: []
    };

    this.formService.saveVersion(dto).subscribe(res => {
      if (res.success && res.data) {
        this.formId = res.data.formId;
        this.versionId = res.data.formVersionId;
        this.router.navigate(['/forms/builder', this.formId], { replaceUrl: true });
      }
    });
  }

  publish(): void {
    if (!this.formId || !this.versionId) return;
    this.formService.publish(this.formId, this.versionId).subscribe();
  }

  /**
   * Preview reads the SAME JSON the end user's fill-in screen would get:
   * canvasControls (already in memory, no round trip needed) + FormRules fetched
   * for the current saved version. Rules only exist once a version has been saved,
   * so an unsaved brand-new form previews with no visibility/validation rules yet —
   * save it first to preview conditional show/hide.
   */
  openPreview(): void {
    this.previewValues = {};
    this.previewVisibility = {};
    this.canvasControls.forEach(c => this.previewValues[c.controlKey] = c.defaultValue ?? '');

    if (this.versionId) {
      this.ruleService.getRules(this.versionId).subscribe(rules => {
        this.previewRules = rules;
        this.recomputePreviewVisibility();
        this.previewOpen = true;
      });
    } else {
      this.previewRules = [];
      this.recomputePreviewVisibility();
      this.previewOpen = true;
    }
  }

  closePreview(): void {
    this.previewOpen = false;
  }

  onPreviewChange(controlKey: string, value: any): void {
    this.previewValues[controlKey] = value;
    this.recomputePreviewVisibility();
  }

  isPreviewCheckboxListSelected(key: string, opt: string): boolean {
    const current: string[] = this.previewValues[key] ?? [];
    return current.includes(opt);
  }

  togglePreviewCheckboxListOption(key: string, opt: string): void {
    const current: string[] = this.previewValues[key] ?? [];
    const next = current.includes(opt) ? current.filter(v => v !== opt) : [...current, opt];
    this.onPreviewChange(key, next);
  }

  private recomputePreviewVisibility(): void {
    const controlKeyById: Record<number, string> = {};
    this.canvasControls.forEach(c => { if (c.controlId) controlKeyById[c.controlId] = c.controlKey; });

    this.previewVisibility = this.ruleEngine.computeVisibility(
      this.previewRules, this.previewValues, controlKeyById);
  }

  seedOptions(c: CanvasControl): string[] {
    if (!c.propertiesJson) return [];
    try {
      const props = JSON.parse(c.propertiesJson);
      return typeof props.SeedData === 'string' ? props.SeedData.split(',') : [];
    } catch { return []; }
  }
  get selectedIsCheckbox(): boolean {
    return this.selected?.controlTypeCode === 'Checkbox';
  }

  get selectedCheckboxText(): string {
    if (!this.selected?.propertiesJson) return '';
    try {
      const props = JSON.parse(this.selected.propertiesJson);
      return typeof props.CheckboxText === 'string' ? props.CheckboxText : '';
    } catch { return ''; }
  }

  onCheckboxTextChange(value: string): void {
    if (!this.selected) return;
    let props: any = {};
    if (this.selected.propertiesJson) {
      try { props = JSON.parse(this.selected.propertiesJson); } catch { props = {}; }
    }
    props.CheckboxText = value;
    this.selected.propertiesJson = JSON.stringify(props);
  }
  onFileSelected(controlKey: string, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    // No upload endpoint exists yet (known gap, flagged earlier) — this only captures
    // the file client-side for now and stores its name as the form value.
    this.selectedFiles[controlKey] = file;
    //this.form.get(controlKey)?.setValue(file.name);
  }
}