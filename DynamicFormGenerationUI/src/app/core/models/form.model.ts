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

export interface CreateFormTemplateRequest {
    formName: string;
    formCode: string;
    description?: string;
}

export interface UpdateFormTemplateRequest extends CreateFormTemplateRequest {
    formId: number;
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
}


export interface SaveFormVersionRequest {
    formId?: number | null;
    formName?: string;
    formDefinitionJson: string;
    layoutDefinitionJson?: string;
    controls: FormControlDef[];
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
    createdDate: string;
}

export interface FormRenderPayload {
    formId: number;
    formVersionId: number;
    formName: string;
    layoutDefinitionJson?: string;
    controls: FormControlDef[];
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

export interface DashboardItems {
    totalForms: number;
    draftForms: number;
    publishedForms: number;
    archivedForms: number;
    recentForms: FormVersionListItem[];
}