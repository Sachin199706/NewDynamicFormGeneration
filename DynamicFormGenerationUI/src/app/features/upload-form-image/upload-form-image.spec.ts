import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { of, throwError } from 'rxjs';
import { OcrLine, OcrResult, OcrWord } from '../../core/models/form-image.model';
import { FormService } from '../../core/services/form';
import { FormImageService } from '../../core/services/form-image';
import { FormImageOcrService } from '../../core/services/form-image-ocr';
import { UploadFormImage } from './upload-form-image';

function w(aStrText: string, aNumX0: number): OcrWord {
  return { text: aStrText, x0: aNumX0, x1: aNumX0 + aStrText.length * 9, confidence: 95 };
}

function line(aNumTop: number, aArrWords: OcrWord[]): OcrLine {
  return { top: aNumTop, height: 18, words: aArrWords };
}

/** A small form: a title, two text fields side by side, a radio group and a date. */
const SAMPLE_OCR: OcrResult = {
  text: 'Joining Form\nFull Name * City\nGender\nO Male O Female\nJoining Date *',
  confidence: 90,
  imageWidth: 900,
  lines: [
    line(20, [w('Joining', 50), w('Form', 125)]),
    line(100, [w('Full', 50), w('Name', 92), w('*', 140), w('City', 480)]),
    line(200, [w('Gender', 50)]),
    line(230, [w('O', 50), w('Male', 75), w('O', 200), w('Female', 225)]),
    line(300, [w('Joining', 50), w('Date', 125), w('*', 170)])
  ]
};

function pngFile(): File {
  return new File([new Uint8Array(10)], 'form.png', { type: 'image/png' });
}

describe('UploadFormImage', () => {
  let lobjFormImageService: { getStatus: ReturnType<typeof vi.fn>; classifyLabels: ReturnType<typeof vi.fn> };
  let lobjFormService: { createTemplate: ReturnType<typeof vi.fn>; saveVersion: ReturnType<typeof vi.fn> };
  let lobjOcrService: { readImage: ReturnType<typeof vi.fn>; terminate: ReturnType<typeof vi.fn> };
  let lobjToastr: { error: ReturnType<typeof vi.fn>; success: ReturnType<typeof vi.fn> };
  let lobjRouter: { navigate: ReturnType<typeof vi.fn> };

  function createComponent(aBoolAiAvailable = true): UploadFormImage {
    lobjFormImageService.getStatus.mockReturnValue(of({
      success: true, data: { isAvailable: aBoolAiAvailable, modelName: 'llama3.2' }, errors: []
    }));

    const lobjFixture = TestBed.createComponent(UploadFormImage);
    lobjFixture.detectChanges();
    return lobjFixture.componentInstance;
  }

  /** Selects a file the same way the file input does. */
  function chooseFile(aObjComponent: UploadFormImage, aObjFile: File): void {
    aObjComponent.onFileInputChange({ target: { files: [aObjFile], value: '' } } as unknown as Event);
  }

  beforeEach(async () => {
    lobjFormImageService = { getStatus: vi.fn(), classifyLabels: vi.fn() };
    lobjFormService = { createTemplate: vi.fn(), saveVersion: vi.fn() };
    lobjOcrService = { readImage: vi.fn().mockResolvedValue(SAMPLE_OCR), terminate: vi.fn().mockResolvedValue(undefined) };
    lobjToastr = { error: vi.fn(), success: vi.fn() };
    lobjRouter = { navigate: vi.fn().mockResolvedValue(true) };

    // jsdom has no object URLs for the image preview.
    URL.createObjectURL = vi.fn(() => 'blob:preview');
    URL.revokeObjectURL = vi.fn();

    await TestBed.configureTestingModule({
      imports: [UploadFormImage],
      providers: [
        { provide: FormImageService, useValue: lobjFormImageService },
        { provide: FormService, useValue: lobjFormService },
        { provide: FormImageOcrService, useValue: lobjOcrService },
        { provide: ToastrService, useValue: lobjToastr },
        { provide: Router, useValue: lobjRouter }
      ]
    }).compileComponents();
  });

  it('checks the AI status when it opens', () => {
    const lobjComponent = createComponent(false);

    expect(lobjFormImageService.getStatus).toHaveBeenCalled();
    expect(lobjComponent.iboolAiAvailable()).toBe(false);
  });

  it('rejects a file that is not a PNG or JPG image', () => {
    const lobjComponent = createComponent();

    chooseFile(lobjComponent, new File(['x'], 'form.pdf', { type: 'application/pdf' }));

    expect(lobjComponent.istrFileError()).toContain('PNG and JPG');
    expect(lobjComponent.iobjSelectedFile()).toBeNull();
  });

  it('reads the image, asks the AI only for the undecided labels and shows the fields for review', async () => {
    lobjFormImageService.classifyLabels.mockReturnValue(of({
      success: true,
      data: { fields: [
        { index: 1, label: 'Full Name', controlType: 'TextBox' },
        { index: 2, label: 'City', controlType: 'Dropdown' },
        { index: 3, label: 'Joining Date', controlType: 'Date' }
      ] },
      errors: []
    }));
    const lobjComponent = createComponent();
    chooseFile(lobjComponent, pngFile());

    await lobjComponent.analyzeImage();

    expect(lobjFormImageService.classifyLabels).toHaveBeenCalledWith(['Full Name', 'City', 'Joining Date']);
    expect(lobjComponent.istrStage()).toBe('review');
    expect(lobjComponent.iarrFields().map(aObjField => [aObjField.label, aObjField.controlType])).toEqual([
      ['Full Name', 'TextBox'], ['City', 'Dropdown'], ['Gender', 'Radio'], ['Joining Date', 'Date']
    ]);
    expect(lobjComponent.iobjDetailsForm.getRawValue()).toMatchObject({ formName: 'Joining Form', formCode: 'JOINING-FORM' });
  });

  it('uses TextBox with a warning and does not call the AI when the AI is not available', async () => {
    const lobjComponent = createComponent(false);
    chooseFile(lobjComponent, pngFile());

    await lobjComponent.analyzeImage();

    expect(lobjFormImageService.classifyLabels).not.toHaveBeenCalled();
    expect(lobjComponent.iarrFields().find(aObjField => aObjField.label === 'Joining Date')?.controlType).toBe('TextBox');
    expect(lobjComponent.iarrWarnings[0].message).toContain('not available');
  });

  it('shows the AI error message and still lets the user review when the AI call fails', async () => {
    lobjFormImageService.classifyLabels.mockReturnValue(of({
      success: false, message: 'The AI is busy with another form. Please try again in a minute.', errors: []
    }));
    const lobjComponent = createComponent();
    chooseFile(lobjComponent, pngFile());

    await lobjComponent.analyzeImage();

    expect(lobjComponent.istrStage()).toBe('review');
    expect(lobjComponent.iarrWarnings[0].message).toContain('busy');
  });

  it('shows a message when the image has no readable text', async () => {
    lobjOcrService.readImage.mockResolvedValue({ text: '', confidence: 0, imageWidth: 100, lines: [] });
    const lobjComponent = createComponent();
    chooseFile(lobjComponent, pngFile());

    await lobjComponent.analyzeImage();

    expect(lobjComponent.istrStage()).toBe('idle');
    expect(lobjComponent.istrAnalyzeError()).toContain('No readable text');
  });

  it('creates the template and a Draft version, then opens the Form Builder', async () => {
    lobjFormImageService.classifyLabels.mockReturnValue(of({ success: true, data: { fields: [] }, errors: [] }));
    lobjFormService.createTemplate.mockReturnValue(of({ success: true, data: { formId: 42 }, errors: [] }));
    lobjFormService.saveVersion.mockReturnValue(of({ success: true, data: { formVersionId: 7 }, errors: [] }));
    const lobjComponent = createComponent();
    chooseFile(lobjComponent, pngFile());
    await lobjComponent.analyzeImage();

    await lobjComponent.createForm();

    expect(lobjFormService.createTemplate).toHaveBeenCalledWith({ formName: 'Joining Form', formCode: 'JOINING-FORM', description: undefined });
    const lobjSaved = lobjFormService.saveVersion.mock.calls[0][0];
    expect(lobjSaved).toMatchObject({ formId: 42, formVersionId: null });
    expect(lobjSaved.controls.map((aObjControl: { label: string }) => aObjControl.label)).toEqual(['Full Name', 'City', 'Gender', 'Joining Date']);
    expect(JSON.parse(lobjSaved.layoutDefinitionJson).rows[0].columns).toHaveLength(2);
    expect(lobjRouter.navigate).toHaveBeenCalledWith(['/forms/builder', 42], { queryParams: { tname: 'Joining Form', version: 7 } });
  });

  it('keeps the review open and shows an error when the template cannot be created', async () => {
    lobjFormImageService.classifyLabels.mockReturnValue(of({ success: true, data: { fields: [] }, errors: [] }));
    lobjFormService.createTemplate.mockReturnValue(throwError(() => ({ status: 500 })));
    const lobjComponent = createComponent();
    chooseFile(lobjComponent, pngFile());
    await lobjComponent.analyzeImage();

    await lobjComponent.createForm();

    expect(lobjToastr.error).toHaveBeenCalled();
    expect(lobjFormService.saveVersion).not.toHaveBeenCalled();
    expect(lobjComponent.istrStage()).toBe('review');
  });

  it('does not create the form when Form Name or Form Code is missing', async () => {
    lobjFormImageService.classifyLabels.mockReturnValue(of({ success: true, data: { fields: [] }, errors: [] }));
    const lobjComponent = createComponent();
    chooseFile(lobjComponent, pngFile());
    await lobjComponent.analyzeImage();
    lobjComponent.iobjDetailsForm.controls.formCode.setValue('');

    await lobjComponent.createForm();

    expect(lobjFormService.createTemplate).not.toHaveBeenCalled();
    expect(lobjToastr.error).toHaveBeenCalledWith('Please fill in Form Name and Form Code.', 'Error');
  });
});
