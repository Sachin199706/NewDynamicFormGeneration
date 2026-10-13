/**
 * Models for the "Upload Form Image" feature.
 *
 * Flow: OCR reads the image → fixed rules build the fields → the AI decides the type of the
 * labels the rules could not decide → the user reviews → the fields become form controls.
 */

/** Control types this application supports (ControlTypes.ControlCode in seed.sql). */
export type FormImageControlType =
    | 'TextBox'
    | 'Number'
    | 'Date'
    | 'Dropdown'
    | 'Radio'
    | 'Checkbox'
    | 'CheckboxList'
    | 'File'
    | 'Label';

/** All control types, in the order they are offered on the review screen. */
export const FORM_IMAGE_CONTROL_TYPES: readonly FormImageControlType[] = [
    'TextBox', 'Number', 'Date', 'Dropdown', 'Radio', 'Checkbox', 'CheckboxList', 'File', 'Label'
];

/** Control types that need a list of options. */
export const FORM_IMAGE_CHOICE_TYPES: readonly FormImageControlType[] = ['Dropdown', 'Radio', 'CheckboxList'];

/** Types the AI may return for a label. "Ignore" means the label is not part of the form. */
export type AiLabelType = 'TextBox' | 'Number' | 'Date' | 'Dropdown' | 'File' | 'Label' | 'Ignore';

export const AI_LABEL_TYPES: readonly AiLabelType[] = ['TextBox', 'Number', 'Date', 'Dropdown', 'File', 'Label', 'Ignore'];

/** One word read by OCR, with its horizontal position in pixels. */
export interface OcrWord {
    text: string;
    x0: number;
    x1: number;
    confidence: number;
}

/** One line of text read by OCR. */
export interface OcrLine {
    top: number;
    height: number;
    words: OcrWord[];
}

/** Everything OCR read from one image. */
export interface OcrResult {
    text: string;
    confidence: number;
    imageWidth: number;
    lines: OcrLine[];
}

/** Where the type of a detected field came from. */
export type DetectedFieldSource = 'rules' | 'ai' | 'user';

/** One field found in the image, shown on the review screen before it becomes a form control. */
export interface DetectedFormField {
    tempId: string;
    label: string;
    /** Null until the AI (or the user) gives a type. */
    controlType: FormImageControlType | null;
    isRequired: boolean;
    options: string[];
    /** Text shown next to a single Checkbox, e.g. "I agree to the terms". */
    checkboxText: string | null;
    /** Position of the label in the image, used to build rows and column spans. Null for fields the user adds. */
    top: number | null;
    height: number | null;
    x0: number | null;
    source: DetectedFieldSource;
}

/** The title and fields found in the image. */
export interface DetectedForm {
    formTitle: string;
    fields: DetectedFormField[];
}

/** A message shown on the review screen. */
export interface FormImageWarning {
    tempId: string | null;
    message: string;
}

/** Response of GET api/form-image/status. */
export interface FormImageAiStatus {
    isAvailable: boolean;
    modelName: string;
}

/** One item of the POST api/form-image/classify-labels response. */
export interface FieldLabelType {
    index: number;
    label: string;
    controlType: string | null;
}

/** Response of POST api/form-image/classify-labels. */
export interface ClassifyFieldLabelsResult {
    fields: FieldLabelType[];
}
