import { FormRule } from './rule.model';

export interface FormListItem {
    formId: number;
    formCode: string;
    formName: string;
    description?: string;
    status: 'Draft' | 'Published' | 'Archived';
    currentVersionNo?: number;
    modifiedDate: string;
    currentVersionId?: number;
}

export interface FormControlDef {
    controlId?: number;
    controlKey: string;
    controlTypeCode: string;
    label?: string;
    placeholder?: string;
    defaultValue?: string;
    isRequired: boolean;
    isReadOnly: boolean;
    isVisible: boolean;
    displayOrder: number;
    parentControlId?: number | null;
    propertiesJson?: string;
    dataSourceId?: number | null;
}

export interface FormLayoutDef {
    layoutId?: number;
    layoutType: 'Section' | 'Row' | 'Column' | 'Tab' | 'Accordion' | 'Panel' | 'Group';
    parentLayoutId?: number | null;
    name?: string;
    displayOrder: number;
    propertiesJson?: string;
}

export interface SaveFormVersionRequest {
    formId?: number | null;
    formName?: string;
    formDefinitionJson: string;
    layoutDefinitionJson?: string;
    controls: FormControlDef[];
    layouts: FormLayoutDef[];
}

export interface FormVersion {
    formVersionId: number;
    formId: number;
    formName: string;
    versionNo: number;
    status: string;
    formDefinitionJson: string;
    layoutDefinitionJson?: string;
    controls: FormControlDef[];
    layouts: FormLayoutDef[];
    createdDate: string;
}

export interface FormRenderPayload {
    formId: number;
    formVersionId: number;
    formName: string;
    layoutDefinitionJson?: string;
    controls: FormControlDef[];
    layouts: FormLayoutDef[];
    rules: FormRule[];
}

export interface ControlType {
    controlTypeId: number;
    controlCode: string;
    controlName: string;
    category?: string;
    componentName?: string;
    defaultPropertiesJson?: string;
    displayOrder: number;
}

export interface FormVersionListItem {
  formId: number;
  formVersionId: number;
  formName: string;
  versionNo: number;
  status: string;
  modifiedDate: string;
}

export interface FormPublishHistoryItem {
  formId: number;
  formVersionId: number;
  formName: string;
  versionNo: number;
  publishedOn: string;
}
