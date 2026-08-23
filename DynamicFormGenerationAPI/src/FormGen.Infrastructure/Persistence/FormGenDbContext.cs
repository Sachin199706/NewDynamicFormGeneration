using Microsoft.EntityFrameworkCore;
using FormGen.Domain.Entities;

namespace FormGen.Infrastructure.Persistence
{
    /// <summary>
    /// Database-First: this context is hand-mapped to match database/01_Schema.sql exactly.
    /// Schema changes start in SQL; this context (and its Configurations/*) are updated to match —
    /// not the other way around (no EF Migrations generating the schema).
    /// </summary>
    public class FormGenDbContext : DbContext
    {
        public FormGenDbContext(DbContextOptions<FormGenDbContext> options) : base(options) { }

        public DbSet<Form> Forms => Set<Form>();
        public DbSet<FormVersion> FormVersions => Set<FormVersion>();

        public DbSet<ControlType> ControlTypes => Set<ControlType>();
        public DbSet<FormControl> FormControls => Set<FormControl>();
        public DbSet<FormLayout> FormLayouts => Set<FormLayout>();
        public DbSet<FormRule> FormRules => Set<FormRule>();

        public DbSet<FormDataSource> FormDataSources => Set<FormDataSource>();
        public DbSet<FormDataSourceItem> FormDataSourceItems => Set<FormDataSourceItem>();

        public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();
        public DbSet<FormSubmissionValue> FormSubmissionValues => Set<FormSubmissionValue>();
        public DbSet<FormFile> FormFiles => Set<FormFile>();

        public DbSet<FormPublishHistory> FormPublishHistories => Set<FormPublishHistory>();
        public DbSet<FormAuditLog> FormAuditLogs => Set<FormAuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(FormGenDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
