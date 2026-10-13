import { FieldLabelType, OcrLine, OcrResult, OcrWord } from '../models/form-image.model';
import { FormImageMapperService } from './form-image-mapper';

// OCR results of the two sample images (leave-request.png and student-registration.png),
// captured from Tesseract.js so the rules can be tested without running OCR.
function w(aStrText: string, aNumX0: number, aNumX1: number, aNumConfidence: number): OcrWord {
  return { text: aStrText, x0: aNumX0, x1: aNumX1, confidence: aNumConfidence };
}

function line(aNumTop: number, aNumHeight: number, aArrWords: OcrWord[]): OcrLine {
  return { top: aNumTop, height: aNumHeight, words: aArrWords };
}

const LEAVE_FORM_OCR: OcrResult = {
  text: "Employee Leave Request Form\nEmployee Name *\n\nEmployee ID *\n\nEmail Address\n\nLeave Type *\n\nFrom Date * To Date *\nNumber of Days\n\nHalf Day\n\nQO Yes QO No\n\nNotify\n\n[] Manager [J HR [] Team Lead\nMedical Certificate\n\nNo file chosen\n\n[1 1 confirm that the above information is correct",
  confidence: 87,
  imageWidth: 900,
  lines: [
    line(46, 32, [w("Employee", 61, 220, 95), w("Leave", 231, 326, 95), w("Request", 337, 472, 96), w("Form", 480, 563, 96)]),
    line(124, 19, [w("Employee", 61, 148, 96), w("Name", 156, 207, 93), w("*", 213, 220, 93)]),
    line(219, 19, [w("Employee", 61, 148, 96), w("ID", 157, 174, 92), w("*", 180, 187, 92)]),
    line(314, 15, [w("Email", 61, 109, 95), w("Address", 119, 187, 96)]),
    line(410, 18, [w("Leave", 61, 113, 95), w("Type", 120, 162, 92), w("*", 169, 176, 92)]),
    line(505, 14, [w("From", 61, 106, 96), w("Date", 113, 153, 93), w("*", 160, 167, 92), w("To", 480, 500, 95), w("Date", 508, 548, 93), w("*", 555, 562, 92)]),
    line(599, 19, [w("Number", 61, 130, 96), w("of", 138, 154, 96), w("Days", 160, 204, 95)]),
    line(694, 19, [w("Half", 61, 96, 96), w("Day", 102, 137, 96)]),
    line(724, 21, [w("QO", 60, 81, 42), w("Yes", 91, 118, 96), w("QO", 190, 211, 76), w("No", 221, 242, 95)]),
    line(774, 19, [w("Notify", 61, 111, 96)]),
    line(804, 21, [w("[]", 60, 81, 31), w("Manager", 91, 161, 95), w("[J", 230, 251, 58), w("HR", 261, 285, 58), w("[]", 400, 421, 50), w("Team", 430, 473, 96), w("Lead", 480, 518, 96)]),
    line(854, 15, [w("Medical", 61, 127, 96), w("Certificate", 135, 223, 93)]),
    line(891, 12, [w("No", 236, 252, 79), w("file", 260, 279, 77), w("chosen", 286, 336, 90)]),
    line(944, 21, [w("[1", 60, 81, 0), w("1", 93, 96, 88), w("confirm", 103, 160, 95), w("that", 166, 196, 96), w("the", 201, 225, 96), w("above", 232, 279, 96), w("information", 286, 373, 95), w("is", 380, 391, 96), w("correct", 398, 452, 96)]),
  ]
};

const STUDENT_FORM_OCR: OcrResult = {
  text: "Student Registration Form\n\nPersonal Details\n\nFirst Name * Last Name *\nDate of Birth * Age Mobile Number *\nGender *\n\nO Male O Female \u00a9 Other\n\nEmail Address *\n\nAddress\n\nCourse Details\n\nCourse * Batch\nHobbies\n\n[ Reading [ sports [J Music OJ Travel\nUpload Photo *\n\nNotie aon\n\n[J agree to the terms and conditions",
  confidence: 83,
  imageWidth: 1000,
  lines: [
    line(36, 30, [w("Student", 51, 168, 96), w("Registration", 180, 363, 96), w("Form", 376, 452, 96)]),
    line(104, 16, [w("Personal", 51, 142, 96), w("Details", 150, 220, 93)]),
    line(159, 13, [w("First", 51, 85, 96), w("Name", 91, 137, 93), w("*", 143, 150, 92), w("Last", 521, 554, 96), w("Name", 560, 606, 92), w("*", 612, 619, 92)]),
    line(249, 17, [w("Date", 51, 87, 96), w("of", 94, 108, 95), w("Birth", 114, 149, 93), w("*", 155, 162, 91), w("Age", 360, 391, 96), w("Mobile", 671, 722, 96), w("Number", 729, 792, 93), w("*", 797, 804, 91)]),
    line(339, 13, [w("Gender", 51, 110, 93), w("*", 115, 122, 92)]),
    line(367, 19, [w("O", 50, 69, 86), w("Male", 77, 112, 94), w("O", 200, 219, 59), w("Female", 227, 282, 95), w("\u00a9", 350, 369, 57), w("Other", 377, 419, 89)]),
    line(419, 13, [w("Email", 51, 94, 95), w("Address", 99, 164, 91), w("*", 170, 177, 91)]),
    line(509, 13, [w("Address", 50, 115, 96)]),
    line(609, 16, [w("Course", 51, 126, 96), w("Details", 133, 203, 96)]),
    line(664, 13, [w("Course", 51, 107, 87), w("*", 113, 120, 87), w("Batch", 521, 565, 96)]),
    line(754, 13, [w("Hobbies", 51, 115, 96)]),
    line(782, 19, [w("[", 50, 69, 66), w("Reading", 77, 139, 96), w("[", 210, 229, 36), w("sports", 237, 285, 96), w("[J", 370, 389, 8), w("Music", 397, 440, 96), w("OJ", 530, 549, 0), w("Travel", 556, 602, 86)]),
    line(834, 17, [w("Upload", 51, 106, 96), w("Photo", 113, 158, 91), w("*", 164, 171, 91)]),
    line(869, 11, [w("Notie", 206, 247, 57), w("aon", 253, 299, 0)]),
    line(927, 19, [w("[J", 50, 69, 64), w("agree", 90, 132, 54), w("to", 138, 151, 96), w("the", 157, 180, 96), w("terms", 185, 227, 96), w("and", 233, 260, 96), w("conditions", 266, 341, 77)]),
  ]
};

/** Builds an AI answer that gives the listed types, in order. */
function aiTypes(aArrTypes: (string | null)[]): FieldLabelType[] {
  return aArrTypes.map((aStrType, aNumIndex) => ({ index: aNumIndex + 1, label: '', controlType: aStrType }));
}

function file(aStrName: string, aStrType: string, aNumSize: number): File {
  return new File([new Uint8Array(aNumSize)], aStrName, { type: aStrType });
}

describe('FormImageMapperService', () => {
  const lobjMapper = new FormImageMapperService();

  describe('validateImageFile', () => {
    it('accepts PNG and JPG images', () => {
      expect(lobjMapper.validateImageFile(file('form.png', 'image/png', 100))).toBeNull();
      expect(lobjMapper.validateImageFile(file('form.jpg', 'image/jpeg', 100))).toBeNull();
    });

    it('rejects a missing, wrong, empty or too large file', () => {
      expect(lobjMapper.validateImageFile(null)).toContain('choose an image');
      expect(lobjMapper.validateImageFile(file('form.pdf', 'application/pdf', 100))).toContain('PNG and JPG');
      expect(lobjMapper.validateImageFile(file('form.png', 'image/png', 0))).toContain('empty');
      expect(lobjMapper.validateImageFile(file('form.png', 'image/png', 6 * 1024 * 1024))).toContain('too large');
    });
  });

  describe('buildFieldsFromOcr (fixed rules)', () => {
    it('finds the title, required marks, radio, tick boxes, file upload and the confirm checkbox', () => {
      const lobjForm = lobjMapper.buildFieldsFromOcr(LEAVE_FORM_OCR);
      const lobjByLabel = new Map(lobjForm.fields.map(aObjField => [aObjField.label, aObjField]));

      expect(lobjForm.formTitle).toBe('Employee Leave Request Form');
      expect(lobjForm.fields.map(aObjField => aObjField.label)).toEqual([
        'Employee Name', 'Employee ID', 'Email Address', 'Leave Type', 'From Date', 'To Date',
        'Number of Days', 'Half Day', 'Notify', 'Medical Certificate',
        'I confirm that the above information is correct'
      ]);
      expect(lobjByLabel.get('Employee Name')!.isRequired).toBe(true);
      expect(lobjByLabel.get('Email Address')!.isRequired).toBe(false);
      expect(lobjByLabel.get('Half Day')).toMatchObject({ controlType: 'Radio', options: ['Yes', 'No'] });
      expect(lobjByLabel.get('Notify')).toMatchObject({ controlType: 'CheckboxList', options: ['Manager', 'HR', 'Team Lead'] });
      expect(lobjByLabel.get('Medical Certificate')).toMatchObject({ controlType: 'File', isRequired: false });
      expect(lobjByLabel.get('I confirm that the above information is correct')).toMatchObject({
        controlType: 'Checkbox',
        checkboxText: 'I confirm that the above information is correct'
      });
    });

    it('sends only the labels the rules cannot decide to the AI', () => {
      const lobjForm = lobjMapper.buildFieldsFromOcr(LEAVE_FORM_OCR);

      expect(lobjMapper.getFieldsNeedingAi(lobjForm.fields).map(aObjField => aObjField.label)).toEqual([
        'Employee Name', 'Employee ID', 'Email Address', 'Leave Type', 'From Date', 'To Date', 'Number of Days'
      ]);
    });

    it('drops low-confidence noise and keeps a sentence tick box separate from the label above', () => {
      const lobjForm = lobjMapper.buildFieldsFromOcr(STUDENT_FORM_OCR);
      const lobjByLabel = new Map(lobjForm.fields.map(aObjField => [aObjField.label, aObjField]));

      expect(lobjForm.formTitle).toBe('Student Registration Form');
      expect(lobjForm.fields).toHaveLength(15);
      expect(lobjByLabel.get('Gender')).toMatchObject({ controlType: 'Radio', isRequired: true, options: ['Male', 'Female', 'Other'] });
      expect(lobjByLabel.get('Hobbies')!.options).toHaveLength(4);
      expect(lobjByLabel.get('Upload Photo')).toMatchObject({ controlType: null, isRequired: true });
      expect(lobjByLabel.get('agree to the terms and conditions')!.controlType).toBe('Checkbox');
    });
  });

  describe('applyAiTypes', () => {
    it('applies the AI types in order, removes Ignore and uses TextBox with a warning for a missing type', () => {
      const lobjForm = lobjMapper.buildFieldsFromOcr(LEAVE_FORM_OCR);

      const lobjResult = lobjMapper.applyAiTypes(lobjForm.fields, aiTypes(
        ['TextBox', 'Ignore', 'TextBox', 'Dropdown', 'Date', 'Date', null]));

      const lobjByLabel = new Map(lobjResult.fields.map(aObjField => [aObjField.label, aObjField]));
      expect(lobjResult.fields).toHaveLength(10);
      expect(lobjByLabel.has('Employee ID')).toBe(false);
      expect(lobjByLabel.get('Leave Type')).toMatchObject({ controlType: 'Dropdown', source: 'ai' });
      expect(lobjByLabel.get('Number of Days')!.controlType).toBe('TextBox');
      expect(lobjResult.warnings).toHaveLength(1);
      expect(lobjResult.warnings[0].message).toContain('Number of Days');
    });

    it('treats an unknown AI type as missing', () => {
      const lobjForm = lobjMapper.buildFieldsFromOcr(LEAVE_FORM_OCR);

      const lobjResult = lobjMapper.applyAiTypes(lobjForm.fields, aiTypes(['Password']));

      expect(lobjResult.fields[0].controlType).toBe('TextBox');
      expect(lobjResult.warnings.length).toBeGreaterThan(0);
    });
  });

  describe('getWarnings and validateFieldsForCreate', () => {
    it('warns about a choice field without options and about low OCR confidence', () => {
      const lobjForm = lobjMapper.buildFieldsFromOcr(LEAVE_FORM_OCR);
      lobjMapper.applyAiTypes(lobjForm.fields, aiTypes(['TextBox', 'TextBox', 'TextBox', 'Dropdown', 'Date', 'Date', 'Number']));

      const larrMessages = lobjMapper.getWarnings(lobjForm.fields, 50).map(aObjWarning => aObjWarning.message);

      expect(larrMessages.some(lstrMessage => lstrMessage.includes('hard to read'))).toBe(true);
      expect(larrMessages.some(lstrMessage => lstrMessage.includes('"Leave Type" is a Dropdown but has no options'))).toBe(true);
    });

    it('blocks creating a form with no fields, an empty label or a missing type', () => {
      const lobjField = lobjMapper.createEmptyField();

      expect(lobjMapper.validateFieldsForCreate([])).toContain('no fields');
      expect(lobjMapper.validateFieldsForCreate([lobjField])).toContain('label');
      expect(lobjMapper.validateFieldsForCreate([{ ...lobjField, label: 'Name', controlType: null }])).toContain('type');
      expect(lobjMapper.validateFieldsForCreate([{ ...lobjField, label: 'Name' }])).toBeNull();
    });
  });

  describe('buildFormVersion (controls and layout)', () => {
    function buildLeaveForm() {
      const lobjForm = lobjMapper.buildFieldsFromOcr(LEAVE_FORM_OCR);
      const lobjResult = lobjMapper.applyAiTypes(lobjForm.fields, aiTypes(
        ['TextBox', 'TextBox', 'TextBox', 'Dropdown', 'Date', 'Date', 'Number']));
      return lobjMapper.buildFormVersion(lobjResult.fields, LEAVE_FORM_OCR.imageWidth);
    }

    it('puts fields that are side by side in the image into one row with matching column spans', () => {
      const lobjDraft = buildLeaveForm();
      const lobjLayout = JSON.parse(lobjDraft.layoutDefinitionJson);
      const lobjFromDate = lobjDraft.controls.find(aObjControl => aObjControl.label === 'From Date')!;
      const lobjToDate = lobjDraft.controls.find(aObjControl => aObjControl.label === 'To Date')!;

      expect(lobjLayout.version).toBe(2);
      expect(lobjLayout.rows).toHaveLength(10);
      expect(lobjFromDate.layoutRowIndex).toBe(lobjToDate.layoutRowIndex);
      expect([lobjFromDate.layoutColumnIndex, lobjToDate.layoutColumnIndex]).toEqual([0, 1]);
      expect(lobjLayout.rows[lobjFromDate.layoutRowIndex!].columns.map((aObjColumn: { span: number }) => aObjColumn.span)).toEqual([6, 6]);
      expect(lobjLayout.columnLayout).toBe(2);
    });

    it('builds three columns of span 4 for three fields on one line', () => {
      const lobjForm = lobjMapper.buildFieldsFromOcr(STUDENT_FORM_OCR);
      lobjMapper.applyDefaultTypes(lobjForm.fields);

      const lobjDraft = lobjMapper.buildFormVersion(lobjForm.fields, STUDENT_FORM_OCR.imageWidth);
      const lobjLayout = JSON.parse(lobjDraft.layoutDefinitionJson);
      const lobjAge = lobjDraft.controls.find(aObjControl => aObjControl.label === 'Age')!;

      expect(lobjLayout.rows[lobjAge.layoutRowIndex!].columns.map((aObjColumn: { span: number }) => aObjColumn.span)).toEqual([4, 4, 4]);
      expect(lobjAge.layoutColumnIndex).toBe(1);
    });

    it('stores required, options, checkbox text and section headings the same way the Form Builder does', () => {
      const lobjDraft = buildLeaveForm();
      const lobjName = lobjDraft.controls.find(aObjControl => aObjControl.label === 'Employee Name')!;
      const lobjNotify = lobjDraft.controls.find(aObjControl => aObjControl.label === 'Notify')!;
      const lobjConfirm = lobjDraft.controls.find(aObjControl => aObjControl.controlTypeCode === 'Checkbox')!;

      expect(lobjName.controlKey).toMatch(/^textbox_\d+$/);
      expect(lobjName.isRequired).toBe(false);
      expect(lobjName.rules).toEqual([expect.objectContaining({
        ruleType: 'Required', controlKey: lobjName.controlKey, errorMessage: 'Employee Name is required.', isActive: true
      })]);
      expect(JSON.parse(lobjNotify.propertiesJson!)).toEqual({ SeedData: 'Manager,HR,Team Lead' });
      expect(JSON.parse(lobjConfirm.propertiesJson!).CheckboxText).toBe('I confirm that the above information is correct');
      expect(new Set(lobjDraft.controls.map(aObjControl => aObjControl.controlKey)).size).toBe(lobjDraft.controls.length);
      expect(JSON.parse(lobjDraft.formDefinitionJson).controls).toHaveLength(lobjDraft.controls.length);
    });

    it('puts fields added by the user on their own full-width rows at the end', () => {
      const lobjAdded = { ...lobjMapper.createEmptyField(), label: 'Remarks' };

      const lobjDraft = lobjMapper.buildFormVersion([lobjAdded], 0);
      const lobjLayout = JSON.parse(lobjDraft.layoutDefinitionJson);

      expect(lobjDraft.controls[0]).toMatchObject({ label: 'Remarks', layoutRowIndex: 0, layoutColumnIndex: 0 });
      expect(lobjLayout.rows[0].columns[0].span).toBe(12);
    });
  });
});
