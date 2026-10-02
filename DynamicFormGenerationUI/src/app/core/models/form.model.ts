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
     sectionKey?: string;
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
    layoutRowIndex?: number;
    layoutColumnIndex?: number;
    parentControlId?: number | null;
    propertiesJson?: string;
    rules?: FormRule[];
}

export interface FormLayoutColumn {
    id: string;
    span: number;
}

export interface FormLayoutRow {
    id: string;
    columns: FormLayoutColumn[];
    borderEnabled?: boolean;
    columnBordersEnabled?: boolean;
    borderStyle?: 'solid' | 'dashed' | 'dotted' | 'double';
    borderWidth?: number;
    borderColor?: string;
}

export interface FormLayoutDefinition {
    version: number;
    columnLayout: number;
    rows: FormLayoutRow[];
}

export function clampLayoutColumnCount(value: number | null | undefined): number {
    const safeValue = Number.isFinite(value) ? Number(value) : 1;
    return Math.min(4, Math.max(1, Math.round(safeValue)));
}

function normalizeLayoutBorderStyle(value: unknown): FormLayoutRow['borderStyle'] {
    return value === 'dashed' || value === 'dotted' || value === 'double' ? value : 'solid';
}

function normalizeLayoutBorderWidth(value: unknown): number {
    const width = Number(value);
    return Number.isFinite(width) ? Math.min(8, Math.max(1, Math.round(width))) : 1;
}

function normalizeLayoutBorderColor(value: unknown): string {
    return typeof value === 'string' && /^#[0-9a-fA-F]{6}$/.test(value) ? value : '#d1d5db';
}

export function createDefaultLayoutDefinition(columnLayout: number = 1): FormLayoutDefinition {
    const safeColumnLayout = clampLayoutColumnCount(columnLayout);
    const rowId = `row-${crypto.randomUUID()}`;
    const columns = Array.from({ length: safeColumnLayout }, (_, index) => ({
        id: `${rowId}-col-${index + 1}`,
        span: Math.max(1, Math.round(12 / safeColumnLayout))
    }));

    return {
        version: 2,
        columnLayout: safeColumnLayout,
        rows: [{
            id: rowId,
            columns,
            borderEnabled: false,
            columnBordersEnabled: false,
            borderStyle: 'solid',
            borderWidth: 1,
            borderColor: '#d1d5db'
        }]
    };
}

export function normalizeLayoutDefinition(rawLayoutJson?: string | null): FormLayoutDefinition {
    const fallback = createDefaultLayoutDefinition(1);

    if (!rawLayoutJson) {
        return fallback;
    }

    try {
        const layout = JSON.parse(rawLayoutJson);
        if (Array.isArray(layout?.rows) && layout.rows.length > 0) {
            const rows: FormLayoutRow[] = layout.rows.map((row: any, rowIndex: number) => ({
                id: row?.id || `row-${rowIndex + 1}`,
                borderEnabled: row?.borderEnabled === true,
                columnBordersEnabled: row?.columnBordersEnabled === true,
                borderStyle: normalizeLayoutBorderStyle(row?.borderStyle),
                borderWidth: normalizeLayoutBorderWidth(row?.borderWidth),
                borderColor: normalizeLayoutBorderColor(row?.borderColor),
                columns: Array.isArray(row?.columns) && row.columns.length > 0
                    ? row.columns.map((column: any, columnIndex: number) => ({
                        id: column?.id || `row-${rowIndex + 1}-col-${columnIndex + 1}`,
                        span: Math.min(12, Math.max(1, Number(column?.span) || 12))
                    }))
                    : [{ id: `row-${rowIndex + 1}-col-1`, span: 12 }]
            }));

            const derivedColumnLayout = Math.max(
                1,
                clampLayoutColumnCount(
                    typeof layout.columnLayout === 'number' ? layout.columnLayout : Math.max(...rows.map((r: FormLayoutRow) => r.columns.length))
                )
            );

            return {
                version: 2,
                columnLayout: derivedColumnLayout,
                rows
            };
        }

        if (typeof layout?.columnLayout === 'number') {
            return createDefaultLayoutDefinition(layout.columnLayout);
        }
    } catch {
        return fallback;
    }

    return fallback;
}


export interface SaveFormVersionRequest {
    formId: number | null;
    formVersionId?: number | null;
    versionDescription?: string;
    formDefinitionJson: string;
    layoutDefinitionJson?: string;
    controls: FormControlDef[];
}

export interface FormVersion {
    formVersionId: number;
    formId: number;
    formName: string;
    versionDescription: string;
    versionNo: number;
    status: string;
    formDefinitionJson: string;
    layoutDefinitionJson?: string;
    controls: FormControlDef[];
    createdDate: string;
    sections?: FormSection[];
}

export interface FormRenderPayload {
    formId: number;
    formVersionId: number;
    formName: string;
    layoutDefinitionJson?: string;
    controls: FormControlDef[];
    rules: FormRule[];
    sections?: FormSection[];
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
  versionDescription:string;
}

export interface FormPublishHistoryItem {
  formId: number;
  formVersionId: number;
  versionDescription: string;
  formName: string;
  versionNo: number;
  publishedOn: string;
}

export interface DashboardItems {
    totalForms: number;
    totalVersions: number;
    draftForms: number;
    publishedForms: number;
    archivedForms: number;
    recentForms: FormVersionListItem[];
}
export interface FormSection {
    sectionKey: string;
    title: string;
    displayOrder: number;
}
