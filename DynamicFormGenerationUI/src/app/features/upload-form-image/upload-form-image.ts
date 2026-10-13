import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { firstValueFrom } from 'rxjs';
import {
  DetectedFormField,
  FORM_IMAGE_CHOICE_TYPES,
  FORM_IMAGE_CONTROL_TYPES,
  FormImageAiStatus,
  FormImageControlType,
  FormImageWarning
} from '../../core/models/form-image.model';
import { FormService } from '../../core/services/form';
import { FormImageService } from '../../core/services/form-image';
import { FormImageMapperService } from '../../core/services/form-image-mapper';
import { FormImageOcrService } from '../../core/services/form-image-ocr';

/** The steps of the page, shown one after another. */
type UploadStage = 'idle' | 'reading' | 'classifying' | 'review' | 'creating';

/**
 * "Upload Form Image" page: the user uploads an image of a form, the app finds the fields
 * (OCR + fixed rules in the browser, the local AI only for field types), the user reviews them,
 * and a new form template with a Draft version is created and opened in the Form Builder.
 *
 * State is kept in signals because the app runs without Zone.js and the AI call skips the
 * global loader, so the view must be told about changes made after awaits.
 */
@Component({
  selector: 'app-upload-form-image',
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './upload-form-image.html',
  styleUrl: './upload-form-image.scss',
})
export class UploadFormImage implements OnInit, OnDestroy {
  private static readonly FORM_CODE_MAX_LENGTH = 50;
  private static readonly VERSION_DESCRIPTION = 'Created from form image';

  private iobjFormBuilder = inject(FormBuilder);
  private iobjFormService = inject(FormService);
  private iobjFormImageService = inject(FormImageService);
  private iobjOcrService = inject(FormImageOcrService);
  private iobjMapper = inject(FormImageMapperService);
  private iobjRouter = inject(Router);
  private iobjToastr = inject(ToastrService);

  readonly iarrControlTypes = FORM_IMAGE_CONTROL_TYPES;
  readonly istrAcceptedTypes = FormImageMapperService.ALLOWED_FILE_TYPES.join(',');
  readonly inumMaxFileSizeMb = FormImageMapperService.MAX_FILE_SIZE_MB;

  iobjDetailsForm = this.iobjFormBuilder.group({
    formName: ['', [Validators.required, Validators.maxLength(150)]],
    formCode: ['', [Validators.required, Validators.maxLength(UploadFormImage.FORM_CODE_MAX_LENGTH)]],
    description: ['', [Validators.maxLength(500)]]
  });

  iobjAiStatus = signal<FormImageAiStatus | null>(null);
  iboolAiStatusChecked = signal(false);
  iobjSelectedFile = signal<File | null>(null);
  istrPreviewUrl = signal<string | null>(null);
  istrFileError = signal<string | null>(null);
  iboolDragOver = signal(false);
  istrStage = signal<UploadStage>('idle');
  inumElapsedSeconds = signal(0);
  istrAnalyzeError = signal<string | null>(null);
  iarrFields = signal<DetectedFormField[]>([]);
  iarrAiWarnings = signal<FormImageWarning[]>([]);
  inumOcrConfidence = signal<number | null>(null);
  istrFormTitle = signal('');

  iboolBusy = computed(() => ['reading', 'classifying', 'creating'].includes(this.istrStage()));
  iboolAiAvailable = computed(() => this.iobjAiStatus()?.isAvailable === true);

  private inumImageWidth = 0;
  private iobjElapsedTimer: ReturnType<typeof setInterval> | null = null;
  private iboolDestroyed = false;

  ngOnInit(): void {
    this.checkAiStatus();
  }

  ngOnDestroy(): void {
    this.iboolDestroyed = true;
    this.stopElapsedTimer();
    this.revokePreviewUrl();
    // Frees the OCR engine (about 15 MB) when the user leaves the page.
    void this.iobjOcrService.terminate();
  }

  // ---------------------------------------------------------------------------
  // AI status
  // ---------------------------------------------------------------------------

  checkAiStatus(): void {
    this.iobjFormImageService.getStatus().subscribe({
      next: aObjResult => {
        this.iobjAiStatus.set(aObjResult.data ?? { isAvailable: false, modelName: '' });
        this.iboolAiStatusChecked.set(true);
      },
      error: () => {
        this.iobjAiStatus.set({ isAvailable: false, modelName: '' });
        this.iboolAiStatusChecked.set(true);
      }
    });
  }

  // ---------------------------------------------------------------------------
  // File selection
  // ---------------------------------------------------------------------------

  onFileInputChange(aObjEvent: Event): void {
    const lobjInput = aObjEvent.target as HTMLInputElement;
    this.selectFile(lobjInput.files?.[0] ?? null);
    // Allows choosing the same file again after "Start over".
    lobjInput.value = '';
  }

  onDragOver(aObjEvent: DragEvent): void {
    aObjEvent.preventDefault();
    if (!this.iboolBusy()) {
      this.iboolDragOver.set(true);
    }
  }

  onDragLeave(): void {
    this.iboolDragOver.set(false);
  }

  onDrop(aObjEvent: DragEvent): void {
    aObjEvent.preventDefault();
    this.iboolDragOver.set(false);
    if (!this.iboolBusy()) {
      this.selectFile(aObjEvent.dataTransfer?.files?.[0] ?? null);
    }
  }

  private selectFile(aObjFile: File | null): void {
    const lstrError = this.iobjMapper.validateImageFile(aObjFile);
    this.istrFileError.set(lstrError);
    if (lstrError || !aObjFile) {
      return;
    }

    this.resetResults();
    this.revokePreviewUrl();
    this.iobjSelectedFile.set(aObjFile);
    this.istrPreviewUrl.set(URL.createObjectURL(aObjFile));
  }

  // ---------------------------------------------------------------------------
  // Analysis: OCR → rules → AI
  // ---------------------------------------------------------------------------

  async analyzeImage(): Promise<void> {
    const lobjFile = this.iobjSelectedFile();
    if (!lobjFile || this.iboolBusy()) {
      return;
    }

    this.resetResults();
    this.istrStage.set('reading');
    this.startElapsedTimer();

    try {
      const lobjOcr = await this.iobjOcrService.readImage(lobjFile);
      if (this.iboolDestroyed) return;

      if (!this.iobjMapper.hasReadableText(lobjOcr)) {
        this.failAnalysis('No readable text was found in this image. Please use a clear, straight image of a printed form.');
        return;
      }

      const lobjForm = this.iobjMapper.buildFieldsFromOcr(lobjOcr);
      if (lobjForm.fields.length === 0) {
        this.failAnalysis('No form fields were found in this image.');
        return;
      }

      this.inumImageWidth = lobjOcr.imageWidth;
      this.inumOcrConfidence.set(lobjOcr.confidence);
      this.istrFormTitle.set(lobjForm.formTitle);
      this.prefillDetails(lobjForm.formTitle);

      const larrFields = await this.applyFieldTypes(lobjForm.fields);
      if (this.iboolDestroyed) return;

      this.iarrFields.set(larrFields);
      this.istrStage.set('review');
    } catch (aObjError) {
      console.error('Upload Form Image: analysis failed', aObjError);
      this.failAnalysis('The image could not be read. Please try again or use another image.');
    } finally {
      this.stopElapsedTimer();
    }
  }

  /** Asks the AI for the types the rules could not decide. If the AI cannot help, TextBox is used with a warning. */
  private async applyFieldTypes(aArrFields: DetectedFormField[]): Promise<DetectedFormField[]> {
    const larrFieldsNeedingAi = this.iobjMapper.getFieldsNeedingAi(aArrFields);
    if (larrFieldsNeedingAi.length === 0) {
      return aArrFields;
    }

    if (!this.iboolAiAvailable()) {
      this.iobjMapper.applyDefaultTypes(aArrFields);
      this.iarrAiWarnings.set([{
        tempId: null,
        message: 'The AI service is not available, so fields without a clear type were set to TextBox. Please check the types.'
      }]);
      return aArrFields;
    }

    this.istrStage.set('classifying');

    try {
      const lobjResult = await firstValueFrom(
        this.iobjFormImageService.classifyLabels(larrFieldsNeedingAi.map(aObjField => aObjField.label)));

      if (lobjResult.success && lobjResult.data) {
        const lobjApplied = this.iobjMapper.applyAiTypes(aArrFields, lobjResult.data.fields);
        this.iarrAiWarnings.set(lobjApplied.warnings);
        return lobjApplied.fields;
      }

      this.iarrAiWarnings.set([{ tempId: null, message: `${lobjResult.message ?? 'The AI could not answer.'} Fields without a clear type were set to TextBox.` }]);
    } catch (aObjError) {
      console.error('Upload Form Image: AI request failed', aObjError);
      const lstrMessage = (aObjError as { status?: number })?.status === 429
        ? 'Too many AI requests. Please wait one minute and use "Analyze Again".'
        : 'The AI service did not answer.';
      this.iarrAiWarnings.set([{ tempId: null, message: `${lstrMessage} Fields without a clear type were set to TextBox.` }]);
    }

    this.iobjMapper.applyDefaultTypes(aArrFields);
    return aArrFields;
  }

  private failAnalysis(aStrMessage: string): void {
    this.istrAnalyzeError.set(aStrMessage);
    this.istrStage.set('idle');
  }

  /** Fills Form Name and Form Code from the image title, only when the user has not typed them. */
  private prefillDetails(aStrTitle: string): void {
    if (!aStrTitle) return;

    const lobjNameControl = this.iobjDetailsForm.controls.formName;
    const lobjCodeControl = this.iobjDetailsForm.controls.formCode;

    if (!lobjNameControl.value) {
      lobjNameControl.setValue(aStrTitle.slice(0, 150));
    }
    if (!lobjCodeControl.value) {
      lobjCodeControl.setValue(this.createFormCode(aStrTitle));
    }
  }

  /** Turns a title into a code, e.g. "Employee Leave Request Form" → "EMPLOYEE-LEAVE-REQUEST-FORM". */
  private createFormCode(aStrTitle: string): string {
    return aStrTitle
      .toUpperCase()
      .replace(/[^A-Z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '')
      .slice(0, UploadFormImage.FORM_CODE_MAX_LENGTH);
  }

  // ---------------------------------------------------------------------------
  // Review table
  // ---------------------------------------------------------------------------

  /** Warnings for the review screen: from the AI step plus the current checks (recomputed on every change). */
  get iarrWarnings(): FormImageWarning[] {
    return [...this.iarrAiWarnings(), ...this.iobjMapper.getWarnings(this.iarrFields(), this.inumOcrConfidence())];
  }

  hasWarning(aObjField: DetectedFormField): boolean {
    return this.iarrWarnings.some(aObjWarning => aObjWarning.tempId === aObjField.tempId);
  }

  needsOptions(aObjField: DetectedFormField): boolean {
    return !!aObjField.controlType && FORM_IMAGE_CHOICE_TYPES.includes(aObjField.controlType);
  }

  optionsText(aObjField: DetectedFormField): string {
    return aObjField.options.join(', ');
  }

  onOptionsTextChange(aObjField: DetectedFormField, aStrValue: string): void {
    aObjField.options = aStrValue.split(',').map(lstrOption => lstrOption.trim()).filter(lstrOption => lstrOption.length > 0);
  }

  onControlTypeChange(aObjField: DetectedFormField, aStrType: FormImageControlType): void {
    aObjField.controlType = aStrType;
    aObjField.source = 'user';
    if (aStrType === 'Checkbox' && !aObjField.checkboxText) {
      aObjField.checkboxText = aObjField.label;
    }
  }

  sourceLabel(aObjField: DetectedFormField): string {
    return aObjField.source === 'ai' ? 'AI' : aObjField.source === 'user' ? 'You' : 'Rules';
  }

  addField(): void {
    this.iarrFields.update(aArrFields => [...aArrFields, this.iobjMapper.createEmptyField()]);
  }

  removeField(aObjField: DetectedFormField): void {
    this.iarrFields.update(aArrFields => aArrFields.filter(aObjItem => aObjItem.tempId !== aObjField.tempId));
  }

  trackByTempId(_: number, aObjField: DetectedFormField): string {
    return aObjField.tempId;
  }

  // ---------------------------------------------------------------------------
  // Create form
  // ---------------------------------------------------------------------------

  async createForm(): Promise<void> {
    if (this.iboolBusy()) return;

    this.iobjDetailsForm.markAllAsTouched();
    if (this.iobjDetailsForm.invalid) {
      this.iobjToastr.error('Please fill in Form Name and Form Code.', 'Error');
      return;
    }

    const larrFields = this.iarrFields();
    const lstrFieldsError = this.iobjMapper.validateFieldsForCreate(larrFields);
    if (lstrFieldsError) {
      this.iobjToastr.error(lstrFieldsError, 'Error');
      return;
    }

    const lobjDraft = this.iobjMapper.buildFormVersion(larrFields, this.inumImageWidth);
    const lobjDetails = this.iobjDetailsForm.getRawValue();
    this.istrStage.set('creating');

    let lnumFormId: number;
    try {
      const lobjTemplate = await firstValueFrom(this.iobjFormService.createTemplate({
        formName: (lobjDetails.formName ?? '').trim(),
        formCode: (lobjDetails.formCode ?? '').trim().toUpperCase(),
        description: (lobjDetails.description ?? '').trim() || undefined
      }));

      if (!lobjTemplate.success || !lobjTemplate.data) {
        this.iobjToastr.error(lobjTemplate.message || lobjTemplate.errors?.join(' ') || 'Unable to create the form template.', 'Error');
        this.istrStage.set('review');
        return;
      }
      lnumFormId = lobjTemplate.data.formId;
    } catch (aObjError) {
      console.error('Upload Form Image: create template failed', aObjError);
      this.iobjToastr.error('Unable to create the form template. The Form Code may already be in use.', 'Error');
      this.istrStage.set('review');
      return;
    }

    try {
      const lobjVersion = await firstValueFrom(this.iobjFormService.saveVersion({
        formId: lnumFormId,
        formVersionId: null,
        versionDescription: UploadFormImage.VERSION_DESCRIPTION,
        formDefinitionJson: lobjDraft.formDefinitionJson,
        layoutDefinitionJson: lobjDraft.layoutDefinitionJson,
        controls: lobjDraft.controls
      }));

      if (!lobjVersion.success || !lobjVersion.data) {
        throw new Error(lobjVersion.message || 'Saving the version failed.');
      }

      this.iobjToastr.success('Form created as a Draft. You can now adjust it in the Form Builder.', 'Success');
      await this.iobjRouter.navigate(['/forms/builder', lnumFormId], {
        queryParams: { tname: lobjDetails.formName, version: lobjVersion.data.formVersionId }
      });
    } catch (aObjError) {
      console.error('Upload Form Image: save version failed', aObjError);
      this.iobjToastr.error(
        'The form template was created, but its fields could not be saved. Open it from Form Templates and try again.',
        'Error');
      this.istrStage.set('review');
    }
  }

  /** Clears everything and goes back to the empty page. */
  startOver(): void {
    if (this.iboolBusy()) return;

    this.resetResults();
    this.revokePreviewUrl();
    this.iobjSelectedFile.set(null);
    this.istrFileError.set(null);
    this.iobjDetailsForm.reset({ formName: '', formCode: '', description: '' });
  }

  isInvalid(aStrControlName: 'formName' | 'formCode' | 'description'): boolean {
    const lobjControl = this.iobjDetailsForm.controls[aStrControlName];
    return lobjControl.invalid && (lobjControl.touched || lobjControl.dirty);
  }

  // ---------------------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------------------

  private resetResults(): void {
    this.istrStage.set('idle');
    this.istrAnalyzeError.set(null);
    this.iarrFields.set([]);
    this.iarrAiWarnings.set([]);
    this.inumOcrConfidence.set(null);
    this.istrFormTitle.set('');
    this.inumImageWidth = 0;
  }

  private startElapsedTimer(): void {
    this.stopElapsedTimer();
    this.inumElapsedSeconds.set(0);
    this.iobjElapsedTimer = setInterval(() => this.inumElapsedSeconds.update(lnumSeconds => lnumSeconds + 1), 1000);
  }

  private stopElapsedTimer(): void {
    if (this.iobjElapsedTimer !== null) {
      clearInterval(this.iobjElapsedTimer);
      this.iobjElapsedTimer = null;
    }
  }

  private revokePreviewUrl(): void {
    const lstrUrl = this.istrPreviewUrl();
    if (lstrUrl) {
      URL.revokeObjectURL(lstrUrl);
      this.istrPreviewUrl.set(null);
    }
  }
}
