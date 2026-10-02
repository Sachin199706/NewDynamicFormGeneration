import { Component, HostListener, OnInit } from '@angular/core';
import { ControlType, FormControlDef, FormLayoutRow, createDefaultLayoutDefinition, normalizeLayoutDefinition } from '../../core/models/form.model';
import { ControlTypeService } from '../../core/services/control-type';
import { FormService } from '../../core/services/form';
import { ControlEffects, FormRule } from '../../core/models/rule.model';
import { RuleEngineService } from '../../core/services/rule-engine';
import { ActivatedRoute, Router } from '@angular/router';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { ToastrService } from 'ngx-toastr';
import { GenericDialog, GenericDialogConfig } from '../../shared/generic-dialog/generic-dialog';

interface CanvasControl extends FormControlDef {
  tempId: string;
}

@Component({
  selector: 'app-form-builder',
  imports: [CommonModule, FormsModule, DragDropModule, GenericDialog],
  templateUrl: './form-builder.html',
  styleUrl: './form-builder.scss',
})
export class FormBuilder implements OnInit {
  inumTemplateId: number | null = null;
  inumVersionId: number | null = null;
  inumVersionNo: number | null = null;
  istrStatus = 'Draft';
  istrVersionDescription = '';
  istrTemplateName = '';
  iarrControlTypes: ControlType[] = [];
  iarrCanvasControls: CanvasControl[] = [];
  iobjSelected: CanvasControl | null = null;
  inumColumnLayout = 1;
  layoutRows: FormLayoutRow[] = [];
  selectedRowIndex: number | null = 0;
  selectedColumnRowIndex: number | null = null;
  selectedColumnIndex: number | null = null;
  readonly equalLayoutOptions = [1, 2, 3, 4];
  readonly presetPreviewCells: Record<number, number[]> = { 1: [0], 2: [0, 1], 3: [0, 1, 2], 4: [0, 1, 2, 3] };
  readonly customWidthOptions = Array.from({ length: 12 }, (_, index) => index + 1);
  rowLayoutMode: 'preset' | 'custom' = 'preset';
  selectedPresetColumnCount = 1;
  customColumnWidths: number[] = [12];
  customWidthsRowId: string | null = null;
  selectedLayoutColumns: number[] = [];
  contextMenu: { rowIndex: number; columnIndex: number | null; x: number; y: number; mode: 'row' | 'column' } | null = null;

  iboolPreviewOpen = false;
  iarrPreviewRules: FormRule[] = [];
  iobjPreviewValues: Record<string, any> = {};
  iobjPreviewEffects: Record<string, ControlEffects> = {};
  iobjPreviewErrors: Record<string, string> = {};
  iobjSelectedFiles: Record<string, File> = {};
  iobjPreviewImageUrls: Record<string, string> = {};
  iobjPreviewTouched: Record<string, boolean> = {};

  istrPublishError = '';
  iboolDirty = false;
  iboolSaving = false;
  iboolLoadingVersion = false;
  iboolLoadFailed = false;
  iboolDialogVisible = false;
  dialogConfig: GenericDialogConfig | null = null;

  constructor(
    private iobjControlTypeService: ControlTypeService,
    private iobjFormService: FormService,
    private iobjRuleEngine: RuleEngineService,
    private iobjRoute: ActivatedRoute,
    private iobjRouter: Router,
    private toastr: ToastrService
  ) {}

  ngOnInit(): void {
    this.iobjControlTypeService.getAll().subscribe(types => this.iarrControlTypes = types);

    const lStrIdParam = this.iobjRoute.snapshot.paramMap.get('tid');
    const lStrTemplateName = this.iobjRoute.snapshot.queryParamMap.get('tname');
    const lStrVersionParam = this.iobjRoute.snapshot.queryParamMap.get('version');

    if (lStrIdParam) {
      const lnumFormId = Number(lStrIdParam);
      if (!Number.isInteger(lnumFormId) || lnumFormId <= 0) {
        this.iboolLoadFailed = true;
        this.toastr.error('Invalid form ID.', 'Error');
        return;
      }
      this.inumTemplateId = lnumFormId;
      if (lStrTemplateName) this.istrTemplateName = String(lStrTemplateName);

      if (lStrVersionParam) {
        const lnumVersionId = Number(lStrVersionParam);
        if (!Number.isInteger(lnumVersionId) || lnumVersionId <= 0) {
          this.iboolLoadFailed = true;
          this.toastr.error('Invalid form version ID.', 'Error');
          return;
        }

        this.iboolLoadingVersion = true;
        this.iobjFormService.getVersionById(lnumVersionId).subscribe({
          next: res => {
            if (!res.success || !res.data) {
              this.iboolLoadingVersion = false;
              this.iboolLoadFailed = true;
              this.toastr.error(res.message ?? 'Unable to load the form version.', 'Error');
              return;
            }

            this.iboolLoadingVersion = false;
            this.inumTemplateId = res.data.formId;
            this.inumVersionId = res.data.formVersionId;
            this.inumVersionNo = res.data.versionNo;
            this.istrStatus = res.data.status;
            this.istrTemplateName = res.data.formName || this.istrTemplateName;
            this.istrVersionDescription = res.data.versionDescription;
            this.iarrCanvasControls = res.data.controls.map(c => ({ ...c, tempId: crypto.randomUUID() }));
            this.layoutRows = this.buildLayoutFromControls(res.data.layoutDefinitionJson ?? '');
            this.inumColumnLayout = this.layoutRows.reduce((max, row) => Math.max(max, row.columns.length), 1);
            this.syncControlLayoutAssignments();
            this.selectRow(0);
          },
          error: err => {
            this.iboolLoadingVersion = false;
            this.iboolLoadFailed = true;
            console.error('Unable to load form version:', err);
            this.toastr.error('Unable to load the form version.', 'Error');
          }
        });
      }
    }

    this.ensureLayoutRows();
  }

  getControlIcon(controlCode: string): string {
    const iconMap: Record<string, string> = {
      TextBox: 'bi-input-cursor-text',
      TextArea: 'bi-textarea-resize',
      Number: 'bi-123',
      Date: 'bi-calendar-date',
      Dropdown: 'bi-menu-button-wide',
      Radio: 'bi-ui-radios',
      Checkbox: 'bi-check-square',
      CheckboxList: 'bi-list-check',
      File: 'bi-file-earmark-arrow-up',
      Image: 'bi-card-image',
      Label: 'bi-tag',
      Default: 'bi-slash-square'
    };

    return iconMap[controlCode] ?? iconMap['Default'];
  }

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

  get selectedLabelHeadingLevel(): string {
    if (!this.iobjSelected?.propertiesJson) return 'h3';
    try {
      const properties = JSON.parse(this.iobjSelected.propertiesJson);
      return properties.LabelHeadingLevel === 'normal' || /^h[1-6]$/.test(properties.LabelHeadingLevel)
        ? properties.LabelHeadingLevel
        : 'h3';
    } catch {
      return 'h3';
    }
  }

  updateLabelHeadingLevel(level: string): void {
    if (!this.iobjSelected || (level !== 'normal' && !/^h[1-6]$/.test(level))) return;
    let properties: Record<string, unknown> = {};
    if (this.iobjSelected.propertiesJson) {
      try {
        properties = JSON.parse(this.iobjSelected.propertiesJson);
      } catch {
        properties = {};
      }
    }
    properties['LabelHeadingLevel'] = level;
    this.iobjSelected.propertiesJson = JSON.stringify(properties);
    this.iboolDirty = true;
  }

  labelHeadingLevel(control: CanvasControl): string {
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

  get selectedCheckboxText(): string {
    if (!this.iobjSelected?.propertiesJson) return '';
    try {
      const lobjProps = JSON.parse(this.iobjSelected.propertiesJson);
      return typeof lobjProps.CheckboxText === 'string' ? lobjProps.CheckboxText : '';
    } catch { return ''; }
  }

  set selectedIsRequired(aBoolValue: boolean) {
    if (!this.iobjSelected) return;
    this.iboolDirty = true;
    const larrRules = this.iobjSelected.rules ?? [];

    if (!aBoolValue) {
      this.iobjSelected.rules = larrRules.filter(r => r.ruleType !== 'Required');
      return;
    }
    if (larrRules.some(r => r.ruleType === 'Required')) return;

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

  get selectedRow(): FormLayoutRow | null {
    if (this.selectedRowIndex == null) return null;
    return this.layoutRows[this.selectedRowIndex] ?? null;
  }

  get previewColumnSpans(): number[] {
    const row = this.selectedRow;
    if (!row) return [];
    return this.rowLayoutMode === 'custom' && this.customWidthsRowId === row.id
      ? this.customColumnWidths
      : row.columns.map(column => column.span);
  }

  get customWidthsTotal(): number {
    return this.customColumnWidths.reduce((total, width) => total + width, 0);
  }

  get customWidthsAreValid(): boolean {
    return this.customWidthsTotal === 12;
  }

  get columnDropListTargets(): string[] {
    const ids = this.layoutRows.flatMap((row, rowIndex) => row.columns.map((_, colIndex) => `column-${rowIndex}-${colIndex}`));
    return ['toolboxList', ...ids];
  }

  selectRow(rowIndex: number): void {
    this.selectedRowIndex = rowIndex;
    this.selectedColumnRowIndex = null;
    this.selectedColumnIndex = null;
    this.selectedLayoutColumns = [];
    this.iobjSelected = null;
    const row = this.selectedRow;
    if (row) this.configureRowLayoutEditor(row);
  }

  selectColumn(rowIndex: number, columnIndex: number): void {
    this.selectedRowIndex = rowIndex;
    this.selectedColumnRowIndex = rowIndex;
    this.selectedColumnIndex = columnIndex;
    this.iobjSelected = null;
  }

  markLayoutDirty(): void {
    if (this.selectedRow) {
      this.selectedRow.borderWidth = Math.min(8, Math.max(1, Math.round(Number(this.selectedRow.borderWidth) || 1)));
    }
    this.iboolDirty = true;
  }

  canRemoveColumn(rowIndex: number): boolean {
    return (this.layoutRows[rowIndex]?.columns.length ?? 1) > 1;
  }

  chooseEqualLayout(columnCount: number): void {
    const row = this.selectedRow;
    if (!row || !this.equalLayoutOptions.includes(columnCount)) return;
    this.replaceRowColumns(row, Array.from({ length: columnCount }, () => 12 / columnCount));
    this.rowLayoutMode = 'preset';
    this.selectedPresetColumnCount = columnCount;
    this.customColumnWidths = row.columns.map(column => Math.round(column.span));
    this.customWidthsRowId = row.id;
    this.selectedLayoutColumns = [];
    this.iboolDirty = true;
  }

  chooseCustomLayout(): void {
    const row = this.selectedRow;
    if (!row) return;
    this.rowLayoutMode = 'custom';
    this.customWidthsRowId = row.id;
    this.customColumnWidths = this.toIntegerWidths(row.columns.map(column => column.span));
    this.selectedLayoutColumns = [];
  }

  updateCustomColumnWidth(columnIndex: number, width: number): void {
    if (columnIndex < 0 || columnIndex >= this.customColumnWidths.length) return;
    const safeWidth = Math.min(12, Math.max(1, Math.round(Number(width) || 1)));
    this.customColumnWidths = this.customColumnWidths.map((current, index) => index === columnIndex ? safeWidth : current);
    if (!this.customWidthsAreValid) return;

    const row = this.selectedRow;
    if (!row || row.id !== this.customWidthsRowId) return;
    row.columns = row.columns.map((column, index) => ({ ...column, span: this.customColumnWidths[index] }));
    this.iboolDirty = true;
  }

  customWidthLabel(width: number): string {
    return `${((width / 12) * 100).toFixed(1)}%`;
  }

  previewWidth(span: number): string {
    return `${(Math.min(span, 12) / 12) * 100}%`;
  }

  columnFlexBasis(span: number, columnCount: number): string {
    const gapSharePx = Math.max(0, columnCount - 1) * span;
    return `0 0 calc(${(span / 12) * 100}% - ${gapSharePx}px)`;
  }

  toggleLayoutColumnSelection(columnIndex: number): void {
    this.selectedLayoutColumns = this.selectedLayoutColumns.includes(columnIndex)
      ? this.selectedLayoutColumns.filter(index => index !== columnIndex)
      : [...this.selectedLayoutColumns, columnIndex].sort((left, right) => left - right);
  }

  canMergeSelectedColumns(): boolean {
    if (this.selectedLayoutColumns.length < 2) return false;
    return this.selectedLayoutColumns.every((columnIndex, index) =>
      index === 0 || columnIndex === this.selectedLayoutColumns[index - 1] + 1
    );
  }

  mergeSelectedColumns(): void {
    const row = this.selectedRow;
    if (!row || !this.canMergeSelectedColumns()) return;
    const selected = [...this.selectedLayoutColumns];
    const first = selected[0];
    const last = selected[selected.length - 1];
    const mergedSpan = row.columns.slice(first, last + 1).reduce((total, column) => total + column.span, 0);
    const nextColumns = row.columns
      .filter((_, index) => index < first || index > last)
      .map(column => ({ ...column }));
    nextColumns.splice(first, 0, { ...row.columns[first], span: mergedSpan });

    this.iarrCanvasControls.forEach(control => {
      if (control.layoutRowIndex !== this.selectedRowIndex || control.layoutColumnIndex === undefined) return;
      if (control.layoutColumnIndex >= first && control.layoutColumnIndex <= last) {
        control.layoutColumnIndex = first;
      } else if (control.layoutColumnIndex > last) {
        control.layoutColumnIndex -= last - first;
      }
    });
    this.setRowColumns(row, nextColumns);
    this.selectedLayoutColumns = [first];
    this.syncRowLayoutEditor(row);
    this.iboolDirty = true;
  }

  canSplitSelectedColumn(): boolean {
    if (this.selectedLayoutColumns.length !== 1) return false;
    const row = this.selectedRow;
    return !!row && row.columns[this.selectedLayoutColumns[0]].span >= 2;
  }

  splitSelectedColumn(): void {
    const row = this.selectedRow;
    if (!row || !this.canSplitSelectedColumn()) return;
    const columnIndex = this.selectedLayoutColumns[0];
    const selectedColumn = row.columns[columnIndex];
    const firstSpan = Math.floor(selectedColumn.span / 2);
    const secondSpan = selectedColumn.span - firstSpan;
    const nextColumns = row.columns.map(column => ({ ...column }));
    nextColumns.splice(columnIndex, 1,
      { ...selectedColumn, span: firstSpan },
      { id: `${row.id}-col-${columnIndex + 2}`, span: secondSpan }
    );

    this.iarrCanvasControls.forEach(control => {
      if (control.layoutRowIndex === this.selectedRowIndex && control.layoutColumnIndex !== undefined && control.layoutColumnIndex > columnIndex) {
        control.layoutColumnIndex += 1;
      }
    });
    this.setRowColumns(row, nextColumns);
    this.selectedLayoutColumns = [columnIndex];
    this.syncRowLayoutEditor(row);
    this.iboolDirty = true;
  }
  addRowAbove(): void {
    this.insertRowAt(this.selectedRowIndex ?? 0);
  }
  addRowBelow(): void {
    this.insertRowAt((this.selectedRowIndex ?? (this.layoutRows.length - 1)) + 1);
  }
  addRow(): void {
    this.insertRowAt(this.layoutRows.length);
  }

  private insertRowAt(index: number): void {
    const insertionIndex = Math.min(this.layoutRows.length, Math.max(0, index));
    const row: FormLayoutRow = {
      id: `row-${crypto.randomUUID()}`,
      borderEnabled: false,
      columnBordersEnabled: false,
      borderStyle: 'solid' as const,
      borderWidth: 1,
      borderColor: '#d1d5db',
      columns: [{
        id: `${crypto.randomUUID()}-col-1`,
        span: 12
      }]
    };
    this.layoutRows.splice(insertionIndex, 0, row);
    this.iarrCanvasControls.forEach(control => {
      if (typeof control.layoutRowIndex === 'number' && control.layoutRowIndex >= insertionIndex) {
        control.layoutRowIndex += 1;
      }
    });
    this.selectedRowIndex = insertionIndex;
    this.selectedColumnRowIndex = null;
    this.selectedColumnIndex = null;
    this.configureRowLayoutEditor(row);
    this.iboolDirty = true;
  }

  removeRow(rowIndex: number): void {
    if (this.layoutRows.length <= 1) return;
    this.layoutRows.splice(rowIndex, 1);
    this.selectedRowIndex = Math.min(rowIndex, this.layoutRows.length - 1);
    this.selectedColumnRowIndex = null;
    this.selectedColumnIndex = null;
    this.iarrCanvasControls.forEach(control => {
      if (control.layoutRowIndex === rowIndex) {
        control.layoutRowIndex = 0;
      }
      if (control.layoutRowIndex !== undefined && control.layoutRowIndex > rowIndex) {
        control.layoutRowIndex -= 1;
      }
    });
    if (this.selectedRow) this.configureRowLayoutEditor(this.selectedRow);
    this.iboolDirty = true;
  }

  reorderRows(event: Pick<CdkDragDrop<FormLayoutRow[]>, 'previousIndex' | 'currentIndex'>): void {
    const { previousIndex, currentIndex } = event;
    if (previousIndex === currentIndex || !this.layoutRows[previousIndex] || !this.layoutRows[currentIndex]) return;

    const selectedRowId = this.selectedRow?.id;
    moveItemInArray(this.layoutRows, previousIndex, currentIndex);
    this.iarrCanvasControls.forEach(control => {
      if (typeof control.layoutRowIndex !== 'number') return;
      if (control.layoutRowIndex === previousIndex) {
        control.layoutRowIndex = currentIndex;
      } else if (previousIndex < currentIndex && control.layoutRowIndex > previousIndex && control.layoutRowIndex <= currentIndex) {
        control.layoutRowIndex -= 1;
      } else if (previousIndex > currentIndex && control.layoutRowIndex >= currentIndex && control.layoutRowIndex < previousIndex) {
        control.layoutRowIndex += 1;
      }
    });

    this.selectedRowIndex = selectedRowId == null ? null : this.layoutRows.findIndex(row => row.id === selectedRowId);
    if (this.selectedColumnRowIndex != null) {
      this.selectedColumnRowIndex = this.selectedRowIndex;
    }
    this.closeContextMenu();
    this.iboolDirty = true;
  }

  openRowContextMenu(event: MouseEvent, rowIndex: number): void {
    event.preventDefault();
    this.selectRow(rowIndex);
    this.contextMenu = { rowIndex, columnIndex: null, x: event.clientX, y: event.clientY, mode: 'row' };
  }

  openColumnContextMenu(event: MouseEvent, rowIndex: number, columnIndex: number): void {
    event.preventDefault();
    this.selectColumn(rowIndex, columnIndex);
    this.contextMenu = { rowIndex, columnIndex, x: event.clientX, y: event.clientY, mode: 'column' };
  }

  closeContextMenu(): void {
    this.contextMenu = null;
  }

  @HostListener('document:click', ['$event'])
  closeContextMenuOnOutsideClick(event: MouseEvent): void {
    if (!this.contextMenu) return;
    const target = event.target;
    if (target instanceof Element && target.closest('.builder-context-menu')) return;
    this.closeContextMenu();
  }

  addColumnToRow(rowIndex: number): void {
    const row = this.layoutRows[rowIndex];
    if (!row || row.columns.length >= 12) return;

    const nextCount = row.columns.length + 1;
    const equalSpan = 12 / nextCount;
    row.columns.push({
      id: `${row.id}-col-${nextCount}`,
      span: equalSpan
    });

    row.columns = row.columns.map((column, index) => ({
      ...column,
      id: `${row.id}-col-${index + 1}`,
      span: equalSpan
    }));

    this.inumColumnLayout = Math.max(this.inumColumnLayout, row.columns.length);
    this.syncRowLayoutEditor(row);
    this.iboolDirty = true;
    this.closeContextMenu();
  }

  removeColumnFromRow(rowIndex: number): void {
    const row = this.layoutRows[rowIndex];
    if (!row || row.columns.length <= 1) return;

    row.columns.pop();
    const newColumnCount = Math.max(1, row.columns.length);
    const defaultSpan = 12 / newColumnCount;
    row.columns = row.columns.map((column, index) => ({
      ...column,
      id: `${row.id}-col-${index + 1}`,
      span: defaultSpan
    }));

    this.iarrCanvasControls.forEach(control => {
      if (control.layoutRowIndex === rowIndex && control.layoutColumnIndex !== undefined && control.layoutColumnIndex >= row.columns.length) {
        control.layoutColumnIndex = row.columns.length - 1;
      }
    });
    this.inumColumnLayout = Math.max(1, this.layoutRows.reduce((max, current) => Math.max(max, current.columns.length), 1));
    this.selectedColumnIndex = null;
    this.selectedColumnRowIndex = null;
    this.syncRowLayoutEditor(row);
    this.iboolDirty = true;
    this.closeContextMenu();
  }

  controlsInCell(rowIndex: number, columnIndex: number): CanvasControl[] {
    return this.iarrCanvasControls.filter(control => control.layoutRowIndex === rowIndex && control.layoutColumnIndex === columnIndex);
  }

  dropToColumn(aObjEvent: CdkDragDrop<any>, rowIndex: number, columnIndex: number): void {
    this.ensureLayoutRows();
    if (!this.layoutRows[rowIndex] || !this.layoutRows[rowIndex].columns[columnIndex]) {
      return;
    }

    const draggedData = aObjEvent.item.data as unknown;
    const isCanvasControl = typeof draggedData === 'object' && draggedData !== null && 'tempId' in draggedData;
    const draggedControl = isCanvasControl
      ? this.iarrCanvasControls.find(control => control.tempId === (draggedData as CanvasControl).tempId)
      : undefined;

    if (aObjEvent.previousContainer === aObjEvent.container && draggedControl) {
      const sourceRowIndex = draggedControl.layoutRowIndex ?? rowIndex;
      const sourceColumnIndex = draggedControl.layoutColumnIndex ?? columnIndex;
      const currentCell = this.controlsInCell(sourceRowIndex, sourceColumnIndex);
      const fromIndex = currentCell.findIndex(control => control.tempId === draggedControl.tempId);
      const toIndex = Math.min(aObjEvent.currentIndex, currentCell.length - 1);

      if (fromIndex >= 0 && fromIndex !== toIndex) {
        const originalList = [...this.iarrCanvasControls];
        const sourceIndex = originalList.findIndex(control => control.tempId === draggedControl.tempId);
        const targetIndex = sourceIndex + (toIndex - fromIndex);

        if (sourceIndex >= 0 && targetIndex >= 0) {
          const [moved] = originalList.splice(sourceIndex, 1);
          originalList.splice(Math.max(0, targetIndex), 0, moved);
          this.iarrCanvasControls = originalList;
        }
      }

      draggedControl.layoutRowIndex = rowIndex;
      draggedControl.layoutColumnIndex = columnIndex;
      this.syncControlLayoutAssignments();
      this.iboolDirty = true;
      return;
    }

    if (draggedControl) {
      this.iarrCanvasControls = this.iarrCanvasControls.filter(control => control.tempId !== draggedControl.tempId);
      draggedControl.layoutRowIndex = rowIndex;
      draggedControl.layoutColumnIndex = columnIndex;
      this.iarrCanvasControls.push(draggedControl);
      this.select(draggedControl);
    } else {
      const lobjCt: ControlType = aObjEvent.item.data as ControlType;
      const lobjNewControl: CanvasControl = {
        tempId: crypto.randomUUID(),
        controlKey: `${lobjCt.controlCode.toLowerCase()}_${Date.now()}`,
        controlTypeCode: lobjCt.controlCode,
        label: lobjCt.controlName,
        placeholder: `Enter ${lobjCt.controlName.toLowerCase()}`,
        isRequired: false,
        isReadOnly: false,
        isVisible: true,
        displayOrder: 0,
        layoutRowIndex: rowIndex,
        layoutColumnIndex: columnIndex
      };
      this.iarrCanvasControls.push(lobjNewControl);
      this.select(lobjNewControl);
    }

    this.iarrCanvasControls.forEach((control, index) => {
      control.displayOrder = index;
      if (control.layoutRowIndex == null) {
        control.layoutRowIndex = rowIndex;
      }
      if (control.layoutColumnIndex == null) {
        control.layoutColumnIndex = columnIndex;
      }
    });

    this.syncControlLayoutAssignments();
    this.iboolDirty = true;
  }

  select(aObjC: CanvasControl): void {
    this.iobjSelected = aObjC;
  }

  removeSelected(): void {
    if (!this.iobjSelected) return;
    this.iarrCanvasControls = this.iarrCanvasControls.filter(c => c !== this.iobjSelected);
    this.iobjSelected = null;
    this.syncControlLayoutAssignments();
    this.iboolDirty = true;
  }

  onOptionsChange(aStrValue: string): void {
    if (!this.iobjSelected) return;
    let lobjProps: any = {};
    if (this.iobjSelected.propertiesJson) {
      try { lobjProps = JSON.parse(this.iobjSelected.propertiesJson); } catch { lobjProps = {}; }
    }
    lobjProps.SeedData = aStrValue;
    this.iobjSelected.propertiesJson = JSON.stringify(lobjProps);
    this.iboolDirty = true;
  }

  onCheckboxTextChange(aStrValue: string): void {
    if (!this.iobjSelected) return;
    let lobjProps: any = {};
    if (this.iobjSelected.propertiesJson) {
      try { lobjProps = JSON.parse(this.iobjSelected.propertiesJson); } catch { lobjProps = {}; }
    }
    lobjProps.CheckboxText = aStrValue;
    this.iobjSelected.propertiesJson = JSON.stringify(lobjProps);
    this.iboolDirty = true;
  }

  save(aFnOnSaved?: () => void, aBoolSkipConfirmation = false): void {
    // Nothing to save until the form has unsaved changes; the Save button is disabled to match.
    if (!this.iboolDirty || this.iboolSaving || this.iboolLoadingVersion || this.iboolLoadFailed) return;

    if (!aBoolSkipConfirmation) {
      this.dialogConfig = {
        title: 'Save Confirmation',
        message: this.istrStatus === 'Published'
          ? 'You are editing a Published form. Saving will create a new Draft version while keeping the existing Published form unchanged. Do you want to continue?'
          : 'Do you want to save the changes made to this form?',
        type: 'confirmation',
        buttons: [
          { action: 'confirm', label: 'Yes', variant: 'primary', callback: () => this.save(aFnOnSaved, true) },
          { action: 'cancel', label: 'No', variant: 'outline-secondary' }
        ]
      };
      return;
    }

    if (this.inumTemplateId == null) {
      this.toastr.error('A form must be selected before it can be saved.', 'Error');
      return;
    }

    this.syncControlLayoutAssignments();
    const larrControlsWithRules = this.iarrCanvasControls.map(({ tempId, ...rest }) => ({
      ...rest,
      rules: rest.rules ?? []
    }));

    const lobjLayout = {
      version: 2,
      columnLayout: this.inumColumnLayout,
      rows: this.layoutRows.map(row => ({
        id: row.id,
        borderEnabled: row.borderEnabled ?? false,
        columnBordersEnabled: row.columnBordersEnabled ?? false,
        borderStyle: row.borderStyle ?? 'solid',
        borderWidth: row.borderWidth ?? 1,
        borderColor: row.borderColor ?? '#d1d5db',
        columns: row.columns.map(column => ({
          id: column.id,
          span: column.span
        }))
      }))
    };

    const lobjDto = {
      formId: this.inumTemplateId,
      formVersionId: this.istrStatus === 'Published' ? null : this.inumVersionId,
      versionDescription: this.istrVersionDescription || 'Untitled Form',
      formDefinitionJson: JSON.stringify({ controls: larrControlsWithRules }),
      layoutDefinitionJson: JSON.stringify(lobjLayout),
      controls: larrControlsWithRules
    };

    const lstrSuccessMessage = lobjDto.formVersionId == null
      ? 'Form version created successfully.'
      : 'Form version updated successfully.';
    this.iboolSaving = true;
    this.iobjFormService.saveVersion(lobjDto).subscribe({
      next: res => {
        if (!res.success || !res.data) {
          this.toastr.error(res.message || res.errors?.join(' ') || 'Unable to save the form.', 'Error');
          return;
        }

        this.inumTemplateId = res.data.formId;
        this.inumVersionId = res.data.formVersionId;
        this.inumVersionNo = res.data.versionNo;
        this.istrStatus = res.data.status;
        this.istrVersionDescription = res.data.versionDescription;
        this.iboolDirty = false;
        this.toastr.success(lstrSuccessMessage, 'Success');
        aFnOnSaved?.();
      },
      error: err => {
        this.iboolSaving = false;
        console.error('Unable to save form version:', err);
        this.toastr.error(err?.error?.message || 'Unable to save the form. Please try again.', 'Error');
      },
      complete: () => this.iboolSaving = false
    });
  }

  // Publish is offered only for a saved Draft version with no unsaved changes.
  // A Published version must be changed and saved first, which creates a new Draft.
  get iboolCanPublish(): boolean {
    return this.inumTemplateId != null
      && this.inumVersionId != null
      && this.istrStatus === 'Draft'
      && !this.iboolDirty
      && !this.iboolSaving
      && !this.iboolLoadingVersion
      && !this.iboolLoadFailed;
  }

  publish(): void {
    if (!this.iboolCanPublish || !this.inumTemplateId || !this.inumVersionId) return;
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

  isRequiredOnCanvas(aObjC: CanvasControl): boolean {
    return (aObjC.rules ?? []).some(r => r.isActive && r.ruleType === 'Required');
  }

  goToRules(): void {
    const lstrTarget = 'Validation Rules';

    if (!this.iboolDirty) {
      this.navigateToRules();
      return;
    }

    this.dialogConfig = {
      title: 'Unsaved Changes',
      message: `You have unsaved changes. Save the form before opening ${lstrTarget}?`,
      type: 'confirmation',
      buttons: [
        {
          action: 'save-and-continue',
          label: 'Save and continue',
          variant: 'primary',
          callback: () => this.save(() => this.navigateToRules(), true)
        },
        { action: 'cancel', label: 'Cancel', variant: 'outline-secondary' }
      ]
    };
  }

  closeDialog(): void {
    this.dialogConfig = null;
  }

  onDialogShow(): void {
    this.iboolDialogVisible = true;
  }

  onDialogHide(): void {
    this.iboolDialogVisible = false;
  }

  onDialogActionError(event: { action: string; error: unknown }): void {
    console.error(`Dialog action "${event.action}" failed:`, event.error);
    this.toastr.error('The requested action could not be completed.', 'Error');
  }

  onVersionDescriptionChange(aStrValue: string): void {
    this.istrVersionDescription = aStrValue;
    this.iboolDirty = true;
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

  previewError(aStrControlKey: string): string {
    return this.iobjPreviewTouched[aStrControlKey]
      ? (this.iobjPreviewErrors[aStrControlKey] ?? '')
      : '';
  }

  private ensureLayoutRows(): void {
    if (this.layoutRows.length === 0) {
      this.layoutRows = createDefaultLayoutDefinition(1).rows;
      this.selectedRowIndex = 0;
      this.inumColumnLayout = 1;
    }
  }

  private configureRowLayoutEditor(row: FormLayoutRow): void {
    const spans = row.columns.map(column => column.span);
    const equal = spans.every(span => Math.abs(span - spans[0]) < 0.001);
    const count = spans.length;
    if (equal && this.equalLayoutOptions.includes(count)) {
      this.rowLayoutMode = 'preset';
      this.selectedPresetColumnCount = count;
    } else {
      this.rowLayoutMode = 'custom';
    }
    this.customColumnWidths = this.toIntegerWidths(spans);
    this.customWidthsRowId = row.id;
    this.selectedLayoutColumns = [];
  }

  private syncRowLayoutEditor(row: FormLayoutRow): void {
    this.customColumnWidths = this.toIntegerWidths(row.columns.map(column => column.span));
    this.customWidthsRowId = row.id;
    const spans = row.columns.map(column => column.span);
    const equal = spans.every(span => Math.abs(span - spans[0]) < 0.001);
    if (equal && this.equalLayoutOptions.includes(spans.length)) {
      this.rowLayoutMode = 'preset';
      this.selectedPresetColumnCount = spans.length;
    } else {
      this.rowLayoutMode = 'custom';
    }
    this.selectedLayoutColumns = [];
  }

  private replaceRowColumns(row: FormLayoutRow, spans: number[]): void {
    const rowIndex = this.layoutRows.indexOf(row);
    const oldColumns = row.columns;
    const columns = spans.map((span, index) => ({ id: `${row.id}-col-${index + 1}`, span }));

    if (rowIndex >= 0 && oldColumns.length !== columns.length) {
      const oldTotal = oldColumns.reduce((total, column) => total + column.span, 0) || 12;
      const newTotal = spans.reduce((total, span) => total + span, 0) || 12;
      this.iarrCanvasControls.forEach(control => {
        if (control.layoutRowIndex !== rowIndex || control.layoutColumnIndex === undefined) return;
        const oldColumnIndex = Math.min(oldColumns.length - 1, Math.max(0, control.layoutColumnIndex));
        const oldStart = oldColumns.slice(0, oldColumnIndex).reduce((total, column) => total + column.span, 0);
        const center = ((oldStart + oldColumns[oldColumnIndex].span / 2) / oldTotal) * newTotal;
        let targetIndex = spans.length - 1;
        let boundary = 0;
        for (let index = 0; index < spans.length; index += 1) {
          boundary += spans[index];
          if (center <= boundary) {
            targetIndex = index;
            break;
          }
        }
        control.layoutColumnIndex = targetIndex;
      });
    }

    row.columns = columns;
  }

  private setRowColumns(row: FormLayoutRow, columns: FormLayoutRow['columns']): void {
    row.columns = columns.map((column, index) => ({
      ...column,
      id: `${row.id}-col-${index + 1}`
    }));
    this.inumColumnLayout = Math.max(1, this.layoutRows.reduce((max, current) => Math.max(max, current.columns.length), 1));
  }

  private toIntegerWidths(spans: number[]): number[] {
    if (spans.length === 0) return [];
    const total = spans.reduce((sum, span) => sum + span, 0) || 12;
    const exact = spans.map(span => (span / total) * 12);
    const widths = exact.map(value => Math.max(1, Math.floor(value)));
    let difference = 12 - widths.reduce((sum, width) => sum + width, 0);

    while (difference > 0) {
      const target = exact.reduce((bestIndex, value, index) =>
        value - Math.floor(value) > exact[bestIndex] - Math.floor(exact[bestIndex]) ? index : bestIndex, 0);
      widths[target] += 1;
      exact[target] = Math.floor(exact[target]);
      difference -= 1;
    }

    while (difference < 0) {
      const target = widths.reduce((bestIndex, width, index) => width > widths[bestIndex] ? index : bestIndex, 0);
      if (widths[target] <= 1) break;
      widths[target] -= 1;
      difference += 1;
    }
    return widths;
  }

  private buildLayoutFromControls(rawLayoutJson: string): FormLayoutRow[] {
    const parsed = normalizeLayoutDefinition(rawLayoutJson);
    const rows = parsed.rows.length > 0 ? parsed.rows : createDefaultLayoutDefinition(this.inumColumnLayout).rows;
    this.inumColumnLayout = parsed.columnLayout;
    return rows;
  }

  private syncControlLayoutAssignments(): void {
    this.ensureLayoutRows();
    const safeRows = this.layoutRows;

    this.iarrCanvasControls.forEach((control, index) => {
      const targetRowIndex = typeof control.layoutRowIndex === 'number' && control.layoutRowIndex >= 0 && control.layoutRowIndex < safeRows.length
        ? control.layoutRowIndex
        : 0;
      const targetRow = safeRows[targetRowIndex];
      const targetColumnIndex = typeof control.layoutColumnIndex === 'number' && targetRow && control.layoutColumnIndex >= 0 && control.layoutColumnIndex < targetRow.columns.length
        ? control.layoutColumnIndex
        : 0;

      control.layoutRowIndex = targetRowIndex;
      control.layoutColumnIndex = targetColumnIndex;
      control.displayOrder = index;
    });

    this.inumColumnLayout = Math.max(1, safeRows.reduce((max, row) => Math.max(max, row.columns.length), 1));
  }

  private recomputePreviewEffects(): void {
    this.iobjPreviewEffects = this.iobjRuleEngine.computeEffects(
      this.iarrPreviewRules,
      this.iobjPreviewValues,
      this.iarrCanvasControls
    );
  }

  private recomputePreviewErrors(): void {
    const lobjResult = this.iobjRuleEngine.evaluateAll(this.iarrPreviewRules, this.iobjPreviewValues);
    const lobjErrors: Record<string, string> = {};

    for (const lobjFailure of lobjResult.failures) {
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
}