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
    FormId            INT IDENTITY(1,1) PRIMARY KEY,
    FormCode          NVARCHAR(50)  NOT NULL UNIQUE,
    FormName          NVARCHAR(150) NOT NULL,
    Description       NVARCHAR(500) NULL,
    CreatedDate       DATETIME      NOT NULL DEFAULT (GETUTCDATE()),
    ModifiedDate      DATETIME      NULL
);
GO

CREATE TABLE FormVersions (
    FormVersionId        INT IDENTITY(1,1) PRIMARY KEY,
    FormId               INT NOT NULL FOREIGN KEY REFERENCES Forms(FormId),
    VersionNo            INT NOT NULL,
    VersionDescription   NVARCHAR(250) NULL,
    Status               VARCHAR(20)   NOT NULL DEFAULT ('Draft'),
    FormDefinitionJson   NVARCHAR(MAX) NOT NULL,   -- controls + rules, fully embedded — source of truth now
    LayoutDefinitionJson NVARCHAR(MAX) NULL,
    CreatedDate          DATETIME NOT NULL DEFAULT (GETUTCDATE()),
    PublishedDate        DATETIME NULL,
    CONSTRAINT UQ_FormVersions UNIQUE (FormId, VersionNo)
);
GO


/* ---------- 2. Control catalog (toolbox only — NOT per-form controls) ---------- */

CREATE TABLE ControlTypes (
    ControlTypeId         INT IDENTITY(1,1) PRIMARY KEY,
    ControlCode           NVARCHAR(50)  NOT NULL UNIQUE,
    ControlName           NVARCHAR(100) NOT NULL,
    Category              NVARCHAR(50)  NULL,
    ComponentName         NVARCHAR(100) NULL,
    DefaultPropertiesJson NVARCHAR(MAX) NULL,
    IsActive              BIT NOT NULL DEFAULT (1),
    DisplayOrder          INT NOT NULL DEFAULT (0)
);
GO

/* ---------- 3. Submissions ---------- */

CREATE TABLE FormSubmissions (
    SubmissionId   INT IDENTITY(1,1) PRIMARY KEY,
    FormId         INT NOT NULL FOREIGN KEY REFERENCES Forms(FormId),
    FormVersionId  INT NOT NULL FOREIGN KEY REFERENCES FormVersions(FormVersionId),
    SubmittedOn    DATETIME NOT NULL DEFAULT (GETUTCDATE()),
    JsonData       NVARCHAR(MAX) NOT NULL,
      IsRead          BIT NOT NULL DEFAULT (0),
    SubmissionCode  NVARCHAR(150) not null unique
);
GO

/* ---------- 4. Publish history ---------- */

CREATE TABLE FormPublishHistory (
    PublishHistoryId  INT IDENTITY(1,1) PRIMARY KEY,
    FormId            INT NOT NULL FOREIGN KEY REFERENCES Forms(FormId),
    FormVersionId     INT NOT NULL FOREIGN KEY REFERENCES FormVersions(FormVersionId),
    PublishedOn       DATETIME NOT NULL DEFAULT (GETUTCDATE()),
    Notes             NVARCHAR(500) NULL
);
GO

CREATE INDEX IX_FormVersions_FormId    ON FormVersions(FormId);
CREATE INDEX IX_FormSubmissions_FormId ON FormSubmissions(FormId);
GO