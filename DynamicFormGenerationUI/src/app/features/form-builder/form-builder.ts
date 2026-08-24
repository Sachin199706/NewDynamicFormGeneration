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

  previewOpen = false;
  previewRules: FormRule[] = [];
  previewValues: Record<string, any> = {};
  previewVisibility: Record<string, boolean> = {};

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

  save(): void {
    const dto = {
      formId: this.formId,
      formName: this.formName || 'Untitled Form',
      formDefinitionJson: JSON.stringify({ controls: this.canvasControls }),
      layoutDefinitionJson: undefined,
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
}
