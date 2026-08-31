USE FormGenerationSystem;


GO
/* ---------- Control types — the 9 functionally-distinct controls per the SOW
   (Radio and "Radio List" are the same control; TextArea/Switch/Grid/Lookup/
   SectionHeader are not in scope) ---------- */
INSERT  INTO ControlTypes (
    ControlCode,
    ControlName,
    Category,
    ComponentName,
    DisplayOrder
)
VALUES ('TextBox', 'Text', 'Basic', 'dfg-textbox', 1),
('Number', 'Number', 'Basic', 'dfg-number', 2),
('Date', 'Date', 'Basic', 'dfg-date', 3),
('Dropdown', 'Dropdown', 'Choice', 'dfg-dropdown', 4),
('Radio', 'Radio', 'Choice', 'dfg-radio', 5),
('Checkbox', 'Checkbox', 'Choice', 'dfg-checkbox', 6),
('CheckboxList', 'Checkbox List', 'Choice', 'dfg-checkboxlist', 7),
('File', 'File Upload', 'Files', 'dfg-file', 8),
('Label', 'Label', 'Basic', 'dfg-label', 9),
('Image', 'Image Upload', 'Files', 'dfg-image', 10);