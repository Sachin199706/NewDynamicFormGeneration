import { Injectable } from '@angular/core';
import { FormControlDef, FormLayoutDefinition, FormLayoutRow } from '../models/form.model';
import { FormRule } from '../models/rule.model';
import {
  AI_LABEL_TYPES,
  AiLabelType,
  DetectedForm,
  DetectedFormField,
  FieldLabelType,
  FORM_IMAGE_CHOICE_TYPES,
  FORM_IMAGE_CONTROL_TYPES,
  FormImageControlType,
  FormImageWarning,
  OcrLine,
  OcrResult
} from '../models/form-image.model';

/** The controls and layout of a new form version, in the same shape the Form Builder saves. */
export interface FormImageVersionDraft {
  controls: FormControlDef[];
  formDefinitionJson: string;
  layoutDefinitionJson: string;
}

/** Result of applying the AI types to the detected fields. */
export interface AiTypesResult {
  fields: DetectedFormField[];
  warnings: FormImageWarning[];
}

/** One part of an OCR line: plain text, or an option after a radio / tick-box mark. */
interface LineItem {
  kind: 'text' | 'radio' | 'checkbox';
  text: string;
  x0: number;
}

/** A field placed in the layout grid. */
interface LayoutCell {
  field: DetectedFormField;
  rowIndex: number;
  columnIndex: number;
  span: number;
}

/**
 * All TypeScript logic of the "Upload Form Image" feature, as plain functions without UI:
 * 1. checks the uploaded file,
 * 2. applies the fixed rules to the OCR result (title, required mark, radio / tick-box options, file upload, noise),
 * 3. applies and checks the types returned by the AI,
 * 4. builds the controls and the layout (rows, columns, column span) in the Form Builder's format.
 *
 * The rules were tested on sample forms with `test-form-image-v2.mjs` before they were written here.
 */
@Injectable({
  providedIn: 'root',
})
export class FormImageMapperService {
  static readonly ALLOWED_FILE_TYPES: readonly string[] = ['image/png', 'image/jpeg'];
  static readonly MAX_FILE_SIZE_MB = 5;
  static readonly MAX_FIELDS = 100;

  private static readonly GRID_COLUMNS = 12;
  private static readonly MIN_TEXT_LENGTH = 10;
  /** Lines whose words have a lower average OCR confidence are treated as noise (grey hint text, smudges). */
  private static readonly MIN_LINE_CONFIDENCE = 60;
  /** Below this overall OCR confidence the review screen shows a warning. */
  private static readonly LOW_OCR_CONFIDENCE = 70;
  /** A single tick box followed by this many words or more is a stand-alone Checkbox, e.g. "I agree to the terms". */
  private static readonly MIN_SENTENCE_WORDS = 3;
  private static readonly MAX_LABEL_LENGTH = 200;
  /** Same values as the Form Builder's default layout row. */
  private static readonly DEFAULT_BORDER_COLOR = '#d1d5db';
  private static readonly SECTION_HEADING_LEVEL = 'h4';

  /** How OCR shows a round radio button in front of an option. */
  private static readonly RADIO_MARK_PATTERN = /^(Q?O|o|0|©|@|\(\)|\(|\(c\)|®)$/;
  /** How OCR shows a square tick box in front of an option. */
  private static readonly CHECKBOX_MARK_PATTERN = /^(\[.{0,2}|[|LO0][\]J]|□|☐)$/;
  /** Text shown by file-upload controls; it means the label above is a File field. */
  private static readonly FILE_HINT_PATTERN = /^(no file chosen|choose file|browse\.*|no file selected)$/i;
  /** Button text that is never a field. */
  private static readonly BUTTON_TEXT_PATTERN = /^(submit|cancel|reset|save|register|send|clear|next|back|close|ok)$/i;
  private static readonly REQUIRED_MARK_PATTERN = /\*|\(required\)/i;

  // ---------------------------------------------------------------------------
  // 1. File check
  // ---------------------------------------------------------------------------

  /** Checks the uploaded file. Returns a message for the user, or null when the file can be used. */
  validateImageFile(aObjFile: File | null | undefined): string | null {
    if (!aObjFile) {
      return 'Please choose an image of a form.';
    }
    if (!FormImageMapperService.ALLOWED_FILE_TYPES.includes(aObjFile.type)) {
      return 'Only PNG and JPG images are allowed.';
    }
    if (aObjFile.size === 0) {
      return 'The file is empty.';
    }
    if (aObjFile.size > FormImageMapperService.MAX_FILE_SIZE_MB * 1024 * 1024) {
      return `The image is too large. The limit is ${FormImageMapperService.MAX_FILE_SIZE_MB} MB.`;
    }
    return null;
  }

  /** Returns true when OCR found enough text to look for fields. */
  hasReadableText(aObjOcr: OcrResult): boolean {
    return aObjOcr.text.trim().length >= FormImageMapperService.MIN_TEXT_LENGTH && aObjOcr.lines.length > 0;
  }

  // ---------------------------------------------------------------------------
  // 2. Fixed rules
  // ---------------------------------------------------------------------------

  /**
   * Applies the fixed rules to the OCR lines and returns the title and the fields.
   * Fields whose type the rules cannot decide have controlType null; the AI decides those.
   */
  buildFieldsFromOcr(aObjOcr: OcrResult): DetectedForm {
    const larrFields: DetectedFormField[] = [];
    let lstrFormTitle = '';
    let lboolTitleChecked = false;
    let lobjPendingLabel: DetectedFormField | null = null;

    for (const lobjLine of aObjOcr.lines) {
      if (this.isNoiseLine(lobjLine)) {
        continue;
      }

      const larrItems = this.splitLineIntoItems(lobjLine);
      const larrOptionItems = larrItems.filter(aObjItem => aObjItem.kind !== 'text');
      const larrTextItems = larrItems.filter(aObjItem => aObjItem.kind === 'text');

      // Rule 1: the first readable line is the form title.
      if (!lboolTitleChecked) {
        lboolTitleChecked = true;
        if (larrOptionItems.length === 0 && larrTextItems.length === 1) {
          lstrFormTitle = this.cleanLabel(larrTextItems[0].text);
          continue;
        }
      }

      // Rule 2: radio / tick-box options belong to a label on the same line or the line above.
      if (larrOptionItems.length > 0) {
        lobjPendingLabel = this.applyOptionLine(lobjLine, larrTextItems, larrOptionItems, lobjPendingLabel, larrFields);
        continue;
      }

      // Rule 3: "No file chosen" / "Choose File" means the label above is a file upload.
      if (larrTextItems.some(aObjItem => FormImageMapperService.FILE_HINT_PATTERN.test(aObjItem.text))) {
        if (lobjPendingLabel) {
          lobjPendingLabel.controlType = 'File';
          lobjPendingLabel.source = 'rules';
        }
        lobjPendingLabel = null;
        continue;
      }

      // Rule 4: every other text item is a label; button text is skipped.
      const larrNewFields = larrTextItems
        .filter(aObjItem => !FormImageMapperService.BUTTON_TEXT_PATTERN.test(this.cleanLabel(aObjItem.text)))
        .map(aObjItem => this.createField(aObjItem.text, lobjLine, aObjItem.x0))
        .filter(aObjField => aObjField.label.length > 0);

      larrFields.push(...larrNewFields);
      lobjPendingLabel = larrNewFields.length === 1 ? larrNewFields[0] : null;
    }

    return { formTitle: lstrFormTitle, fields: larrFields.slice(0, FormImageMapperService.MAX_FIELDS) };
  }

  /** Returns the fields whose type the rules could not decide, in form order. These go to the AI. */
  getFieldsNeedingAi(aArrFields: DetectedFormField[]): DetectedFormField[] {
    return aArrFields.filter(aObjField => aObjField.controlType === null);
  }

  // ---------------------------------------------------------------------------
  // 3. AI types
  // ---------------------------------------------------------------------------

  /**
   * Applies the types returned by the AI. The n-th AI item belongs to the n-th field that needed AI.
   * "Ignore" removes the field; a missing or unknown type becomes TextBox with a warning.
   */
  applyAiTypes(aArrFields: DetectedFormField[], aArrAiTypes: FieldLabelType[]): AiTypesResult {
    const larrWarnings: FormImageWarning[] = [];
    const larrFieldsNeedingAi = this.getFieldsNeedingAi(aArrFields);
    const lobjIgnoredIds = new Set<string>();

    larrFieldsNeedingAi.forEach((aObjField, aNumIndex) => {
      const lstrAiType = aArrAiTypes.find(aObjItem => aObjItem.index === aNumIndex + 1)?.controlType ?? null;

      if (lstrAiType === 'Ignore') {
        lobjIgnoredIds.add(aObjField.tempId);
        return;
      }

      if (lstrAiType !== null && (AI_LABEL_TYPES as readonly string[]).includes(lstrAiType)) {
        aObjField.controlType = lstrAiType as Exclude<AiLabelType, 'Ignore'>;
      } else {
        aObjField.controlType = 'TextBox';
        larrWarnings.push({
          tempId: aObjField.tempId,
          message: `The AI gave no type for "${aObjField.label}", so it was set to TextBox. Please check it.`
        });
      }
      aObjField.source = 'ai';
    });

    return {
      fields: aArrFields.filter(aObjField => !lobjIgnoredIds.has(aObjField.tempId)),
      warnings: larrWarnings
    };
  }

  /** Gives every field that still has no type the TextBox type, e.g. when the AI is not available. */
  applyDefaultTypes(aArrFields: DetectedFormField[]): void {
    for (const lobjField of this.getFieldsNeedingAi(aArrFields)) {
      lobjField.controlType = 'TextBox';
    }
  }

  // ---------------------------------------------------------------------------
  // 4. Checks for the review screen
  // ---------------------------------------------------------------------------

  /** Returns the warnings shown on the review screen. They do not block creating the form. */
  getWarnings(aArrFields: DetectedFormField[], aNumOcrConfidence: number | null): FormImageWarning[] {
    const larrWarnings: FormImageWarning[] = [];

    if (aNumOcrConfidence !== null && aNumOcrConfidence < FormImageMapperService.LOW_OCR_CONFIDENCE) {
      larrWarnings.push({
        tempId: null,
        message: `The image text was hard to read (${aNumOcrConfidence}% sure). Please check every field.`
      });
    }

    const lobjSeenLabels = new Set<string>();
    for (const lobjField of aArrFields) {
      const lstrKey = lobjField.label.trim().toLowerCase();
      if (lstrKey && lobjSeenLabels.has(lstrKey)) {
        larrWarnings.push({ tempId: lobjField.tempId, message: `"${lobjField.label}" appears more than once.` });
      }
      lobjSeenLabels.add(lstrKey);

      if (lobjField.controlType && FORM_IMAGE_CHOICE_TYPES.includes(lobjField.controlType)
        && this.cleanOptions(lobjField.options).length === 0) {
        larrWarnings.push({
          tempId: lobjField.tempId,
          message: `"${lobjField.label}" is a ${lobjField.controlType} but has no options. Please add them.`
        });
      }
    }

    return larrWarnings;
  }

  /** Checks the fields before the form is created. Returns a message for the user, or null when they are valid. */
  validateFieldsForCreate(aArrFields: DetectedFormField[]): string | null {
    if (aArrFields.length === 0) {
      return 'There are no fields. Add at least one field.';
    }
    if (aArrFields.length > FormImageMapperService.MAX_FIELDS) {
      return `A form can have at most ${FormImageMapperService.MAX_FIELDS} fields.`;
    }
    if (aArrFields.some(aObjField => aObjField.label.trim().length === 0)) {
      return 'Every field needs a label.';
    }
    if (aArrFields.some(aObjField => !aObjField.controlType || !FORM_IMAGE_CONTROL_TYPES.includes(aObjField.controlType))) {
      return 'Every field needs a type.';
    }
    return null;
  }

  /** Creates an empty field for the "Add field" button on the review screen. */
  createEmptyField(): DetectedFormField {
    return {
      tempId: crypto.randomUUID(),
      label: '',
      controlType: 'TextBox',
      isRequired: false,
      options: [],
      checkboxText: null,
      top: null,
      height: null,
      x0: null,
      source: 'user'
    };
  }

  // ---------------------------------------------------------------------------
  // 5. Controls and layout
  // ---------------------------------------------------------------------------

  /**
   * Turns the reviewed fields into controls and a layout, in the same format the Form Builder saves.
   * Fields that were side by side in the image share a row; each column gets a span (1-12).
   */
  buildFormVersion(aArrFields: DetectedFormField[], aNumImageWidth: number): FormImageVersionDraft {
    const larrCells = this.buildLayoutCells(aArrFields, aNumImageWidth);
    const lnumKeyStamp = Date.now();

    const larrRows: FormLayoutRow[] = [];
    for (const lobjCell of larrCells) {
      if (!larrRows[lobjCell.rowIndex]) {
        larrRows[lobjCell.rowIndex] = this.createLayoutRow();
      }
      const lobjRow = larrRows[lobjCell.rowIndex];
      lobjRow.columns.push({ id: `${lobjRow.id}-col-${lobjCell.columnIndex + 1}`, span: lobjCell.span });
    }

    const larrControls = larrCells.map((aObjCell, aNumIndex) =>
      this.createControl(aObjCell, aNumIndex, `${aObjCell.field.controlType!.toLowerCase()}_${lnumKeyStamp + aNumIndex}`));

    const lobjLayout: FormLayoutDefinition = {
      version: 2,
      columnLayout: Math.max(1, ...larrRows.map(aObjRow => aObjRow.columns.length)),
      rows: larrRows.length > 0 ? larrRows : [this.createLayoutRow(true)]
    };

    return {
      controls: larrControls,
      formDefinitionJson: JSON.stringify({ controls: larrControls }),
      layoutDefinitionJson: JSON.stringify(lobjLayout)
    };
  }

  // ---------------------------------------------------------------------------
  // Private helpers: rules
  // ---------------------------------------------------------------------------

  /** Handles a line that has radio / tick-box options and returns the new pending label (always null). */
  private applyOptionLine(
    aObjLine: OcrLine,
    aArrTextItems: LineItem[],
    aArrOptionItems: LineItem[],
    aObjPendingLabel: DetectedFormField | null,
    aArrFields: DetectedFormField[]
  ): DetectedFormField | null {
    const lstrKind = aArrOptionItems[0].kind;
    const larrOptionTexts = aArrOptionItems.map(aObjItem => this.cleanLabel(aObjItem.text)).filter(lstrText => lstrText.length > 0);
    const lobjSameLineLabel = aArrTextItems.find(aObjItem => aObjItem.x0 < aArrOptionItems[0].x0);
    const lboolSentenceCheckbox = lstrKind === 'checkbox'
      && larrOptionTexts.length === 1
      && larrOptionTexts[0].split(' ').length >= FormImageMapperService.MIN_SENTENCE_WORDS;

    let lobjField: DetectedFormField | null = null;
    if (lobjSameLineLabel) {
      lobjField = this.createField(lobjSameLineLabel.text, aObjLine, lobjSameLineLabel.x0);
      aArrFields.push(lobjField);
    } else if (aObjPendingLabel && !lboolSentenceCheckbox) {
      lobjField = aObjPendingLabel;
    }

    if (lobjField === null && lboolSentenceCheckbox) {
      // A single tick box with a sentence and no label, e.g. "I agree to the terms".
      // The label above (if any) keeps waiting for its type, e.g. "Upload Photo" before "I agree ...".
      const lobjCheckbox = this.createField(aArrOptionItems[0].text, aObjLine, aArrOptionItems[0].x0);
      lobjCheckbox.controlType = 'Checkbox';
      lobjCheckbox.checkboxText = lobjCheckbox.label;
      aArrFields.push(lobjCheckbox);
      return aObjPendingLabel;
    }

    if (lobjField !== null) {
      lobjField.source = 'rules';
      if (lstrKind === 'radio') {
        lobjField.controlType = 'Radio';
        lobjField.options.push(...larrOptionTexts);
      } else if (larrOptionTexts.length === 1 && lobjField.options.length === 0) {
        lobjField.controlType = 'Checkbox';
        lobjField.checkboxText = larrOptionTexts[0];
      } else {
        lobjField.controlType = 'CheckboxList';
        if (lobjField.checkboxText) {
          // Options continued on a second line: the first one was taken as a single tick box.
          lobjField.options.push(lobjField.checkboxText);
          lobjField.checkboxText = null;
        }
        lobjField.options.push(...larrOptionTexts);
      }
    }

    return null;
  }

  /** Returns true when the text words of a line were read with low confidence (usually grey hint text or noise). */
  private isNoiseLine(aObjLine: OcrLine): boolean {
    const larrTextWords = aObjLine.words.filter(aObjWord =>
      !FormImageMapperService.RADIO_MARK_PATTERN.test(aObjWord.text)
      && !FormImageMapperService.CHECKBOX_MARK_PATTERN.test(aObjWord.text));

    if (larrTextWords.length === 0) {
      return false;
    }

    const lnumAverage = larrTextWords.reduce((lnumTotal, aObjWord) => lnumTotal + aObjWord.confidence, 0) / larrTextWords.length;
    return lnumAverage < FormImageMapperService.MIN_LINE_CONFIDENCE;
  }

  /** Splits one OCR line into items. A new item starts at a large gap between words or at a radio / tick-box mark. */
  private splitLineIntoItems(aObjLine: OcrLine): LineItem[] {
    const lnumGapLimit = aObjLine.height * 1.5;
    const larrItems: { kind: LineItem['kind']; words: string[]; x0: number; x1: number }[] = [];
    let lobjCurrent: (typeof larrItems)[number] | null = null;

    for (const lobjWord of aObjLine.words) {
      const lstrMarkKind: LineItem['kind'] | null = FormImageMapperService.RADIO_MARK_PATTERN.test(lobjWord.text) ? 'radio'
        : FormImageMapperService.CHECKBOX_MARK_PATTERN.test(lobjWord.text) ? 'checkbox'
        : null;
      const lboolBigGap = lobjCurrent !== null && lobjWord.x0 - lobjCurrent.x1 > lnumGapLimit;

      if (lobjCurrent === null || lstrMarkKind !== null || lboolBigGap) {
        lobjCurrent = {
          kind: lstrMarkKind ?? 'text',
          words: lstrMarkKind !== null ? [] : [lobjWord.text],
          x0: lobjWord.x0,
          x1: lobjWord.x1
        };
        larrItems.push(lobjCurrent);
      } else {
        lobjCurrent.words.push(lobjWord.text);
        lobjCurrent.x1 = lobjWord.x1;
      }
    }

    return larrItems
      .map(aObjItem => ({ kind: aObjItem.kind, text: aObjItem.words.join(' ').trim(), x0: aObjItem.x0 }))
      .filter(aObjItem => aObjItem.text.length > 0);
  }

  /** Creates a field found by the rules, keeping the position of its label. */
  private createField(aStrRawLabel: string, aObjLine: OcrLine, aNumX0: number): DetectedFormField {
    return {
      tempId: crypto.randomUUID(),
      label: this.cleanLabel(aStrRawLabel),
      controlType: null,
      isRequired: FormImageMapperService.REQUIRED_MARK_PATTERN.test(aStrRawLabel),
      options: [],
      checkboxText: null,
      top: aObjLine.top,
      height: aObjLine.height,
      x0: aNumX0,
      source: 'rules'
    };
  }

  /** Removes the "*" mark, "(required)", a trailing ":" and extra spaces, and fixes a leading "1" read instead of "I". */
  private cleanLabel(aStrText: string): string {
    return aStrText
      .replace(/\*/g, '')
      .replace(/\(required\)/ig, '')
      .replace(/[:\s]+$/, '')
      .replace(/\s+/g, ' ')
      .trim()
      .replace(/^[1l|] (?=[a-z])/, 'I ')
      .slice(0, FormImageMapperService.MAX_LABEL_LENGTH);
  }

  /** Trims the options, removes empty and repeated ones, and replaces commas (options are stored comma-separated). */
  private cleanOptions(aArrOptions: string[]): string[] {
    const larrOptions = aArrOptions
      .map(lstrOption => lstrOption.replace(/,/g, ' ').replace(/\s+/g, ' ').trim())
      .filter(lstrOption => lstrOption.length > 0);
    return [...new Set(larrOptions)];
  }

  // ---------------------------------------------------------------------------
  // Private helpers: controls and layout
  // ---------------------------------------------------------------------------

  /**
   * Puts fields whose labels are at the same height into one row and gives each column a span
   * from where its label starts. Fields without a position (added by the user) get their own row at the end.
   */
  private buildLayoutCells(aArrFields: DetectedFormField[], aNumImageWidth: number): LayoutCell[] {
    const larrRows: { top: number; height: number; fields: DetectedFormField[] }[] = [];
    const larrUnplaced: DetectedFormField[] = [];

    for (const lobjField of aArrFields) {
      if (lobjField.top === null || lobjField.height === null || lobjField.x0 === null) {
        larrUnplaced.push(lobjField);
        continue;
      }

      const lobjRow = larrRows.find(aObjRow =>
        Math.abs(aObjRow.top - lobjField.top!) <= Math.max(aObjRow.height, lobjField.height!) / 2);
      if (lobjRow) {
        lobjRow.fields.push(lobjField);
      } else {
        larrRows.push({ top: lobjField.top, height: lobjField.height, fields: [lobjField] });
      }
    }

    larrRows.sort((aObjFirst, aObjSecond) => aObjFirst.top - aObjSecond.top);
    const larrCells: LayoutCell[] = [];

    larrRows.forEach((aObjRow, aNumRowIndex) => {
      const larrRowFields = [...aObjRow.fields].sort((aObjFirst, aObjSecond) => aObjFirst.x0! - aObjSecond.x0!);
      const larrSpans = this.calculateSpans(larrRowFields.map(aObjField => aObjField.x0!), aNumImageWidth);

      larrRowFields.forEach((aObjField, aNumColumnIndex) => {
        larrCells.push({ field: aObjField, rowIndex: aNumRowIndex, columnIndex: aNumColumnIndex, span: larrSpans[aNumColumnIndex] });
      });
    });

    larrUnplaced.forEach((aObjField, aNumIndex) => {
      larrCells.push({ field: aObjField, rowIndex: larrRows.length + aNumIndex, columnIndex: 0, span: FormImageMapperService.GRID_COLUMNS });
    });

    return larrCells;
  }

  /**
   * Returns the span of each column in a row. Column starts come from the label positions;
   * every span is at least 1 and the spans always add up to 12.
   */
  private calculateSpans(aArrStartPixels: number[], aNumImageWidth: number): number[] {
    const lnumGrid = FormImageMapperService.GRID_COLUMNS;
    const lnumCount = Math.min(aArrStartPixels.length, lnumGrid);

    const larrStarts = aArrStartPixels.slice(0, lnumCount).map((lnumPixel, lnumIndex) => {
      if (lnumIndex === 0) return 0;
      return aNumImageWidth > 0
        ? Math.round((lnumPixel / aNumImageWidth) * lnumGrid)
        : Math.round((lnumIndex * lnumGrid) / lnumCount);
    });

    for (let lnumIndex = 1; lnumIndex < lnumCount; lnumIndex++) {
      const lnumMaxStart = lnumGrid - (lnumCount - lnumIndex);
      larrStarts[lnumIndex] = Math.min(Math.max(larrStarts[lnumIndex], larrStarts[lnumIndex - 1] + 1), lnumMaxStart);
    }

    return larrStarts.map((lnumStart, lnumIndex) => (larrStarts[lnumIndex + 1] ?? lnumGrid) - lnumStart);
  }

  /** Creates a layout row with the same default style as the Form Builder. */
  private createLayoutRow(aBoolWithDefaultColumn = false): FormLayoutRow {
    const lstrRowId = `row-${crypto.randomUUID()}`;
    return {
      id: lstrRowId,
      borderEnabled: false,
      columnBordersEnabled: false,
      borderStyle: 'solid',
      borderWidth: 1,
      borderColor: FormImageMapperService.DEFAULT_BORDER_COLOR,
      columns: aBoolWithDefaultColumn ? [{ id: `${lstrRowId}-col-1`, span: FormImageMapperService.GRID_COLUMNS }] : []
    };
  }

  /** Turns one reviewed field into a Form Builder control, with the same properties the builder sets. */
  private createControl(aObjCell: LayoutCell, aNumDisplayOrder: number, aStrControlKey: string): FormControlDef {
    const lobjField = aObjCell.field;
    const lstrType = lobjField.controlType as FormImageControlType;
    const lstrLabel = lobjField.label.trim();
    const lobjProperties: Record<string, string> = {};
    let lstrPlaceholder = '';

    if (FORM_IMAGE_CHOICE_TYPES.includes(lstrType)) {
      lobjProperties['SeedData'] = this.cleanOptions(lobjField.options).join(',');
    }
    if (lstrType === 'Checkbox') {
      const lstrCheckboxText = (lobjField.checkboxText ?? lstrLabel).trim();
      lobjProperties['CheckboxText'] = lstrCheckboxText;
      lstrPlaceholder = lstrCheckboxText;
    }
    if (lstrType === 'Label') {
      lobjProperties['LabelHeadingLevel'] = FormImageMapperService.SECTION_HEADING_LEVEL;
    }
    if (lstrType === 'TextBox' || lstrType === 'Number') {
      lstrPlaceholder = `Enter ${lstrLabel.toLowerCase()}`;
    }

    // Required is stored as a "Required" rule, the same way the Form Builder does it.
    const larrRules: FormRule[] = lobjField.isRequired && lstrType !== 'Label'
      ? [{
        ruleId: crypto.randomUUID(),
        controlKey: aStrControlKey,
        ruleType: 'Required',
        errorMessage: `${lstrLabel} is required.`,
        severity: 'Error',
        displayOrder: 0,
        isActive: true
      }]
      : [];

    return {
      controlKey: aStrControlKey,
      controlTypeCode: lstrType,
      label: lstrLabel,
      placeholder: lstrPlaceholder,
      isRequired: false,
      isReadOnly: false,
      isVisible: true,
      displayOrder: aNumDisplayOrder,
      layoutRowIndex: aObjCell.rowIndex,
      layoutColumnIndex: aObjCell.columnIndex,
      propertiesJson: Object.keys(lobjProperties).length > 0 ? JSON.stringify(lobjProperties) : undefined,
      rules: larrRules
    };
  }
}
