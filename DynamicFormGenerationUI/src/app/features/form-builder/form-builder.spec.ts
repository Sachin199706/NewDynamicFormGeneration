import { FormLayoutRow, normalizeLayoutDefinition } from '../../core/models/form.model';
import { FormBuilder } from './form-builder';

function createBuilder(row: FormLayoutRow): FormBuilder {
  const builder = new FormBuilder({} as any, {} as any, {} as any, {} as any, {} as any, {} as any);
  builder.layoutRows = [row];
  builder.selectRow(0);
  return builder;
}

function createControl(controlKey: string, columnIndex: number): any {
  return {
    tempId: controlKey,
    controlKey,
    controlTypeCode: 'TextBox',
    isRequired: false,
    isReadOnly: false,
    isVisible: true,
    displayOrder: 0,
    layoutRowIndex: 0,
    layoutColumnIndex: columnIndex
  };
}

function createRow(spans: number[]): FormLayoutRow {
  return {
    id: 'row-test',
    columns: spans.map((span, index) => ({ id: `col-${index + 1}`, span })),
    borderEnabled: true,
    columnBordersEnabled: true,
    borderStyle: 'dashed',
    borderWidth: 2,
    borderColor: '#123456'
  };
}

describe('FormBuilder row layout editor', () => {
  it('creates all four equal layouts with a total width of twelve', () => {
    const builder = createBuilder(createRow([12]));
    const expectedSpans = [[12], [6, 6], [4, 4, 4], [3, 3, 3, 3]];

    expectedSpans.forEach((spans, index) => {
      builder.chooseEqualLayout(index + 1);
      expect(builder.layoutRows[0].columns.map(column => column.span)).toEqual(spans);
      expect(builder.layoutRows[0].columns.reduce((total, column) => total + column.span, 0)).toBe(12);
    });
  });

  it('preserves multiple controls when changing from a single section to a preset layout', () => {
    const builder = createBuilder(createRow([12]));
    builder.iarrCanvasControls = [createControl('first', 0), createControl('second', 0)];

    builder.chooseEqualLayout(2);

    expect(builder.layoutRows[0].columns.map(column => column.span)).toEqual([6, 6]);
    expect(builder.iarrCanvasControls).toHaveLength(2);
    expect(builder.iarrCanvasControls.every(control => control.layoutRowIndex === 0 && control.layoutColumnIndex === 0)).toBe(true);
  });

  it('applies custom widths only when they total twelve and preserves row borders', () => {
    const builder = createBuilder(createRow([4, 4, 4]));
    builder.chooseCustomLayout();

    builder.updateCustomColumnWidth(0, 5);
    expect(builder.customWidthsAreValid).toBe(false);
    expect(builder.layoutRows[0].columns.map(column => column.span)).toEqual([4, 4, 4]);

    builder.updateCustomColumnWidth(0, 3);
    builder.updateCustomColumnWidth(1, 6);
    builder.updateCustomColumnWidth(2, 3);

    expect(builder.customWidthsAreValid).toBe(true);
    expect(builder.layoutRows[0].columns.map(column => column.span)).toEqual([3, 6, 3]);
    expect(builder.layoutRows[0]).toMatchObject({
      borderEnabled: true,
      columnBordersEnabled: true,
      borderStyle: 'dashed',
      borderWidth: 2,
      borderColor: '#123456'
    });
  });

  it('persists a valid heading level on Label controls without replacing other properties', () => {
    const builder = createBuilder(createRow([12]));
    builder.iobjSelected = {
      ...createControl('form-heading', 0),
      controlTypeCode: 'Label',
      propertiesJson: JSON.stringify({ SeedData: 'kept' })
    };

    expect(builder.selectedLabelHeadingLevel).toBe('h3');
    builder.updateLabelHeadingLevel('h1');

    expect(builder.selectedLabelHeadingLevel).toBe('h1');
    expect(JSON.parse(builder.iobjSelected?.propertiesJson ?? '{}')).toEqual({ SeedData: 'kept', LabelHeadingLevel: 'h1' });
    builder.updateLabelHeadingLevel('normal');
    expect(builder.selectedLabelHeadingLevel).toBe('normal');
    builder.updateLabelHeadingLevel('h7');
    expect(builder.selectedLabelHeadingLevel).toBe('normal');
  });

  it('adds and removes equal columns without losing controls', () => {
    const builder = createBuilder(createRow([6, 6]));
    builder.iarrCanvasControls = [createControl('left', 0), createControl('right', 1)];

    builder.addColumnToRow(0);
    expect(builder.layoutRows[0].columns.map(column => column.span)).toEqual([4, 4, 4]);
    builder.iarrCanvasControls.push(createControl('last', 2));
    builder.removeColumnFromRow(0);

    expect(builder.layoutRows[0].columns.map(column => column.span)).toEqual([6, 6]);
    expect(builder.iarrCanvasControls.map(control => control.controlKey)).toEqual(['left', 'right', 'last']);
    expect(builder.iarrCanvasControls.map(control => control.layoutColumnIndex)).toEqual([0, 1, 1]);
  });

  it('reorders rows while keeping controls and the selected row attached', () => {
    const builder = createBuilder(createRow([12]));
    const secondRow = { ...createRow([12]), id: 'row-second' };
    builder.layoutRows.push(secondRow);
    builder.iarrCanvasControls = [createControl('first-row-control', 0), { ...createControl('second-row-control', 0), layoutRowIndex: 1 }];
    builder.selectRow(1);

    builder.reorderRows({ previousIndex: 1, currentIndex: 0 });

    expect(builder.layoutRows.map(row => row.id)).toEqual(['row-second', 'row-test']);
    expect(builder.iarrCanvasControls.map(control => control.layoutRowIndex)).toEqual([1, 0]);
    expect(builder.selectedRowIndex).toBe(0);
    expect(builder.selectedRow?.id).toBe('row-second');
  });

  it('inserts rows above and below the selected row without moving existing controls', () => {
    const builder = createBuilder(createRow([12]));
    const secondRow = { ...createRow([6, 6]), id: 'row-second' };
    builder.layoutRows.push(secondRow);
    builder.iarrCanvasControls = [
      createControl('first-row-control', 0),
      { ...createControl('second-row-control', 1), layoutRowIndex: 1 }
    ];
    builder.selectRow(1);

    builder.addRowAbove();
    expect(builder.layoutRows[1].columns.map(column => column.span)).toEqual([12]);
    expect(builder.iarrCanvasControls.map(control => control.layoutRowIndex)).toEqual([0, 2]);
    expect(builder.selectedRowIndex).toBe(1);

    builder.addRowBelow();
    expect(builder.layoutRows[2].columns.map(column => column.span)).toEqual([12]);
    expect(builder.iarrCanvasControls.map(control => control.layoutRowIndex)).toEqual([0, 3]);
    expect(builder.selectedRowIndex).toBe(2);
  });

  it('merges adjacent columns and splits them without losing or detaching controls', () => {
    const builder = createBuilder(createRow([3, 6, 3]));
    builder.iarrCanvasControls = [
      createControl('first-a', 0),
      createControl('first-b', 0),
      createControl('middle', 1),
      createControl('last', 2)
    ];
    builder.selectedLayoutColumns = [0, 1];

    builder.mergeSelectedColumns();
    expect(builder.layoutRows[0].columns.map(column => column.span)).toEqual([9, 3]);
    expect(builder.iarrCanvasControls.map(control => control.layoutColumnIndex)).toEqual([0, 0, 0, 1]);
    expect(builder.iarrCanvasControls.length).toBe(4);

    builder.selectedLayoutColumns = [0];
    builder.splitSelectedColumn();
    expect(builder.layoutRows[0].columns.map(column => column.span)).toEqual([4, 5, 3]);
    expect(builder.iarrCanvasControls.map(control => control.layoutColumnIndex)).toEqual([0, 0, 0, 2]);
    expect(builder.iarrCanvasControls.length).toBe(4);
  });

  it('normalizes existing layouts without border metadata and retains configured borders', () => {
    const legacyLayout = normalizeLayoutDefinition(JSON.stringify({
      rows: [{ id: 'legacy', columns: [{ id: 'legacy-col', span: 12 }] }]
    }));
    const borderedLayout = normalizeLayoutDefinition(JSON.stringify({
      rows: [{
        id: 'bordered',
        borderEnabled: true,
        columnBordersEnabled: true,
        borderStyle: 'dotted',
        borderWidth: 3,
        borderColor: '#abcdef',
        columns: [{ id: 'bordered-col', span: 12 }]
      }]
    }));

    expect(legacyLayout.rows[0].columns[0].span).toBe(12);
    expect(legacyLayout.rows[0].borderEnabled).toBe(false);
    expect(borderedLayout.rows[0]).toMatchObject({
      borderEnabled: true,
      columnBordersEnabled: true,
      borderStyle: 'dotted',
      borderWidth: 3,
      borderColor: '#abcdef'
    });
  });
});