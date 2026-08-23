/* ============================================================
   Form Generation System
   Database Schema (SQL Server)
   Database-First: this file is the source of truth. EF Core
   entities/configurations are hand-mapped to match it exactly.

   No login/auth in this application — the dashboard and all
   screens are open by URL. There is no Users/Roles/UserRoles
   table, and no CreatedBy/ModifiedBy/PublishedBy/SubmittedBy
   attribution anywhere in the schema.
   ============================================================ */
IF DB_ID('FormGenerationSystem') IS NULL
    BEGIN
        CREATE DATABASE FormGenerationSystem;
    END


GO
USE FormGenerationSystem;


GO
/* ---------- 1. Forms / Versions ---------- */
CREATE TABLE Forms (
    FormId           INT            IDENTITY (1, 1) PRIMARY KEY,
    FormCode         NVARCHAR (50)  NOT NULL UNIQUE,
    FormName         NVARCHAR (150) NOT NULL,
    Description      NVARCHAR (500) NULL,
    Status           VARCHAR (20)   DEFAULT ('Draft') NOT NULL, -- Draft | Published | Archived
    CurrentVersionId INT            NULL, -- FK added after FormVersions exists (below)
    IsActive         BIT            DEFAULT (1) NOT NULL,
    CreatedDate      DATETIME       DEFAULT (GETUTCDATE()) NOT NULL,
    ModifiedDate     DATETIME       NULL
);


GO
CREATE TABLE FormVersions (
    FormVersionId        INT            IDENTITY (1, 1) PRIMARY KEY,
    FormId               INT            NOT NULL FOREIGN KEY REFERENCES Forms (FormId),
    VersionNo            INT            NOT NULL,
    VersionName          NVARCHAR (100) NULL,
    Status               VARCHAR (20)   DEFAULT ('Draft') NOT NULL, -- Draft | Published | Archived
    FormDefinitionJson   NVARCHAR (MAX) NOT NULL, -- full canvas snapshot (controls + layout tree)
    LayoutDefinitionJson NVARCHAR (MAX) NULL, -- layout-only snapshot (sections/rows/cols)
    CreatedDate          DATETIME       DEFAULT (GETUTCDATE()) NOT NULL,
    PublishedDate        DATETIME       NULL,
    CONSTRAINT UQ_FormVersions UNIQUE (FormId, VersionNo)
);


GO
ALTER TABLE Forms
    ADD CONSTRAINT FK_Forms_CurrentVersion FOREIGN KEY (CurrentVersionId) REFERENCES FormVersions (FormVersionId);


GO
/* ---------- 2. Control catalog ---------- */
CREATE TABLE ControlTypes (
    ControlTypeId         INT            IDENTITY (1, 1) PRIMARY KEY,
    ControlCode           NVARCHAR (50)  NOT NULL UNIQUE, -- TextBox, Number, Dropdown, ...
    ControlName           NVARCHAR (100) NOT NULL,
    Category              NVARCHAR (50)  NULL, -- Basic | Choice | Files | Layout
    ComponentName         NVARCHAR (100) NULL, -- Angular component selector
    DefaultPropertiesJson NVARCHAR (MAX) NULL, -- default PropertyItem set
    IsActive              BIT            DEFAULT (1) NOT NULL,
    DisplayOrder          INT            DEFAULT (0) NOT NULL
);


GO
CREATE TABLE FormControls (
    ControlId       INT            IDENTITY (1, 1) PRIMARY KEY,
    FormVersionId   INT            NOT NULL FOREIGN KEY REFERENCES FormVersions (FormVersionId),
    ControlKey      NVARCHAR (100) NOT NULL, -- stable field key, e.g. "employeeName"
    ControlTypeId   INT            NOT NULL FOREIGN KEY REFERENCES ControlTypes (ControlTypeId),
    ControlName     NVARCHAR (150) NULL,
    Label           NVARCHAR (150) NULL,
    Placeholder     NVARCHAR (150) NULL,
    DefaultValue    NVARCHAR (500) NULL,
    IsRequired      BIT            DEFAULT (0) NOT NULL,
    IsReadOnly      BIT            DEFAULT (0) NOT NULL,
    IsVisible       BIT            DEFAULT (1) NOT NULL,
    DisplayOrder    INT            DEFAULT (0) NOT NULL,
    ParentControlId INT            NULL FOREIGN KEY REFERENCES FormControls (ControlId), -- nesting (e.g. inside a Grid)
    PropertiesJson  NVARCHAR (MAX) NULL, -- control-specific overrides (SeedData, MaxLength, etc.)
    ValidationJson  NVARCHAR (MAX) NULL, -- denormalized cache of active FormRules for this control (perf)
    DataSourceId    INT            NULL, -- FK added after FormDataSources exists (below)
    CreatedDate     DATETIME       DEFAULT (GETUTCDATE()) NOT NULL,
    ModifiedDate    DATETIME       NULL,
    CONSTRAINT UQ_FormControls_Key UNIQUE (FormVersionId, ControlKey)
);


GO
CREATE TABLE FormLayouts (
    LayoutId       INT            IDENTITY (1, 1) PRIMARY KEY,
    FormVersionId  INT            NOT NULL FOREIGN KEY REFERENCES FormVersions (FormVersionId),
    LayoutType     VARCHAR (20)   NOT NULL, -- Section | Row | Column | Tab | Accordion | Panel | Group
    ParentLayoutId INT            NULL FOREIGN KEY REFERENCES FormLayouts (LayoutId),
    Name           NVARCHAR (100) NULL,
    DisplayOrder   INT            DEFAULT (0) NOT NULL,
    PropertiesJson NVARCHAR (MAX) NULL -- e.g. {"columnSpan":6,"collapsible":true,"visible":true}
);


GO
/* ---------- 3. Rule Validation Engine ---------- */
CREATE TABLE FormRules (
    RuleId          INT            IDENTITY (1, 1) PRIMARY KEY,
    FormVersionId   INT            NOT NULL FOREIGN KEY REFERENCES FormVersions (FormVersionId),
    ControlId       INT            NOT NULL FOREIGN KEY REFERENCES FormControls (ControlId),
    RuleType        VARCHAR (30)   NOT NULL, -- Required|MinLength|MaxLength|Regex|Range|Email|Date|CrossField|Visibility|Custom
    RuleDetailsJson NVARCHAR (MAX) NULL, -- type-specific config, see ARCHITECTURE.md
    ErrorMessage    NVARCHAR (300) NOT NULL,
    Severity        VARCHAR (10)   DEFAULT ('Error') NOT NULL, -- Error | Warning
    DisplayOrder    INT            DEFAULT (0) NOT NULL,
    IsActive        BIT            DEFAULT (1) NOT NULL,
    CreatedDate     DATETIME       DEFAULT (GETUTCDATE()) NOT NULL,
    ModifiedDate    DATETIME       NULL
);


GO
/* ---------- 4. Data sources (for Dropdown/Lookup controls) ---------- */
CREATE TABLE FormDataSources (
    DataSourceId INT            IDENTITY (1, 1) PRIMARY KEY,
    Name         NVARCHAR (150) NOT NULL,
    SourceType   VARCHAR (20)   DEFAULT ('Static') NOT NULL, -- Static | Api | Sql
    ConfigJson   NVARCHAR (MAX) NULL, -- api url / sql text, when not Static
    CreatedDate  DATETIME       DEFAULT (GETUTCDATE()) NOT NULL
);


GO
CREATE TABLE FormDataSourceItems (
    DataSourceItemId INT            IDENTITY (1, 1) PRIMARY KEY,
    DataSourceId     INT            NOT NULL FOREIGN KEY REFERENCES FormDataSources (DataSourceId),
    ItemValue        NVARCHAR (200) NOT NULL,
    ItemText         NVARCHAR (200) NOT NULL,
    DisplayOrder     INT            DEFAULT (0) NOT NULL
);


GO
ALTER TABLE FormControls
    ADD CONSTRAINT FK_FormControls_DataSource FOREIGN KEY (DataSourceId) REFERENCES FormDataSources (DataSourceId);


GO
/* ---------- 5. Submissions ---------- */
CREATE TABLE FormSubmissions (
    SubmissionId  INT            IDENTITY (1, 1) PRIMARY KEY,
    FormId        INT            NOT NULL FOREIGN KEY REFERENCES Forms (FormId),
    FormVersionId INT            NOT NULL FOREIGN KEY REFERENCES FormVersions (FormVersionId),
    SubmittedOn   DATETIME       DEFAULT (GETUTCDATE()) NOT NULL,
    JsonData      NVARCHAR (MAX) NOT NULL -- full raw snapshot of submitted key/value pairs
);


GO
CREATE TABLE FormSubmissionValues (
    SubmissionValueId INT            IDENTITY (1, 1) PRIMARY KEY,
    SubmissionId      INT            NOT NULL FOREIGN KEY REFERENCES FormSubmissions (SubmissionId),
    ControlId         INT            NOT NULL FOREIGN KEY REFERENCES FormControls (ControlId),
    Value             NVARCHAR (MAX) NULL -- normalized, per-field row (used for reporting/queries)
);


GO
CREATE TABLE FormFiles (
    FileId        INT            IDENTITY (1, 1) PRIMARY KEY,
    SubmissionId  INT            NOT NULL FOREIGN KEY REFERENCES FormSubmissions (SubmissionId),
    ControlId     INT            NOT NULL FOREIGN KEY REFERENCES FormControls (ControlId),
    FileName      NVARCHAR (260) NOT NULL,
    StoragePath   NVARCHAR (500) NOT NULL, -- files live on disk/blob storage; DB stores metadata only
    ContentType   NVARCHAR (100) NULL,
    FileSizeBytes BIGINT         NULL,
    UploadedOn    DATETIME       DEFAULT (GETUTCDATE()) NOT NULL
);


GO
/* ---------- 6. Publish history / audit ---------- */
CREATE TABLE FormPublishHistory (
    PublishHistoryId INT            IDENTITY (1, 1) PRIMARY KEY,
    FormId           INT            NOT NULL FOREIGN KEY REFERENCES Forms (FormId),
    FormVersionId    INT            NOT NULL FOREIGN KEY REFERENCES FormVersions (FormVersionId),
    PublishedOn      DATETIME       DEFAULT (GETUTCDATE()) NOT NULL,
    Notes            NVARCHAR (500) NULL
);


GO
CREATE TABLE FormAuditLogs (
    AuditLogId  INT            IDENTITY (1, 1) PRIMARY KEY,
    FormId      INT            NULL FOREIGN KEY REFERENCES Forms (FormId),
    Action      NVARCHAR (100) NOT NULL, -- Created | Updated | Published | Archived | Deleted
    Details     NVARCHAR (MAX) NULL,
    CreatedDate DATETIME       DEFAULT (GETUTCDATE()) NOT NULL
);


GO
/* ---------- Helpful indexes ---------- */
CREATE INDEX IX_FormVersions_FormId
    ON FormVersions(FormId);

CREATE INDEX IX_FormControls_FormVersionId
    ON FormControls(FormVersionId);

CREATE INDEX IX_FormRules_FormVersionId
    ON FormRules(FormVersionId);

CREATE INDEX IX_FormRules_ControlId
    ON FormRules(ControlId);

CREATE INDEX IX_FormSubmissions_FormId
    ON FormSubmissions(FormId);

CREATE INDEX IX_FormSubmissionValues_SubId
    ON FormSubmissionValues(SubmissionId);