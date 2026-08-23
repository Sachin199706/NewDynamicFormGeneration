USE FormGenerationSystem;


GO
/* ---------- Control types (mirrors the old controls.xml, now DB-driven) ---------- */
INSERT  INTO ControlTypes (
    ControlCode,
    ControlName,
    Category,
    ComponentName,
    DisplayOrder
)
VALUES                   ('TextBox', 'Text Box', 'Basic', 'dfg-textbox', 1),
('TextArea', 'Text Area', 'Basic', 'dfg-textarea', 2),
('Number', 'Number', 'Basic', 'dfg-number', 3),
('Dropdown', 'Dropdown', 'Choice', 'dfg-dropdown', 4),
('Checkbox', 'Checkbox', 'Choice', 'dfg-checkbox', 5),
('RadioGroup', 'Radio Button', 'Choice', 'dfg-radiogroup', 6),
('DatePicker', 'Date Picker', 'Basic', 'dfg-datepicker', 7),
('File', 'File Upload', 'Files', 'dfg-file', 8),
('Switch', 'Switch', 'Choice', 'dfg-switch', 9),
('Grid', 'Grid', 'Advanced', 'dfg-grid', 10),
('Lookup', 'Lookup', 'Advanced', 'dfg-lookup', 11),
('Label', 'Label', 'Basic', 'dfg-label', 12),
('SectionHeader', 'Section Header', 'Layout', 'dfg-section', 13);