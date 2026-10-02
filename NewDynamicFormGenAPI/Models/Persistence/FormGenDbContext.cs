using Microsoft.EntityFrameworkCore;
using NewDynamicFormGenAPI.Models.Entities;

namespace FormGen.Infrastructure.Persistence
{
    /// <summary>
    /// Entity Framework Core DbContext for the form generation system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FormGenDbContext follows a database-first approach where schema changes are defined
    /// in SQL scripts rather than generated from code. This context is hand-mapped to match
    /// the database schema exactly without relying on EF Migrations.
    /// </para>
    /// <para>
    /// Database schema source: <c>database/01_Schema.sql</c>
    /// </para>
    /// <para>
    /// When the schema changes:
    /// <list type="number">
    ///   <item><description>Modify the SQL schema script</description></item>
    ///   <item><description>Apply changes to the actual database</description></item>
    ///   <item><description>Update this context's DbSet properties and entity configurations</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public class FormGenDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FormGenDbContext"/> class.
        /// </summary>
        /// <param name="options">The EF Core options configuration including connection string.</param>
        public FormGenDbContext(DbContextOptions<FormGenDbContext> options) : base(options) { }

        /// <summary>
        /// Gets or sets the DbSet for Form entities.
        /// </summary>
        /// <remarks>
        /// Forms represent the top-level form templates in the system.
        /// </remarks>
        public DbSet<Form> Forms => Set<Form>();

        /// <summary>
        /// Gets or sets the DbSet for FormVersion entities.
        /// </summary>
        /// <remarks>
        /// Form versions represent distinct revisions of forms, tracking the complete
        /// form structure including controls, rules, and layout as JSON snapshots.
        /// </remarks>
        public DbSet<FormVersion> FormVersions => Set<FormVersion>();

        /// <summary>
        /// Gets or sets the DbSet for ControlType entities.
        /// </summary>
        /// <remarks>
        /// Control types define the catalog of available controls for form designers
        /// (e.g., TextBox, Dropdown, DatePicker).
        /// </remarks>
        public DbSet<ControlType> ControlTypes => Set<ControlType>();

        /// <summary>
        /// Gets or sets the DbSet for FormSubmission entities.
        /// </summary>
        /// <remarks>
        /// Form submissions represent user-submitted form responses, including
        /// submitted data values and submission metadata.
        /// </remarks>
        public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();

        /// <summary>
        /// Gets or sets the DbSet for FormPublishHistory entities.
        /// </summary>
        /// <remarks>
        /// Publish history records track when form versions were published,
        /// providing audit trails and governance tracking.
        /// </remarks>
        public DbSet<FormPublishHistory> FormPublishHistories => Set<FormPublishHistory>();

        /// <summary>
        /// Configures entity mappings and model relationships.
        /// </summary>
        /// <param name="modelBuilder">The model builder used to configure entities.</param>
        /// <remarks>
        /// This method applies all entity type configurations from the current assembly,
        /// enabling centralized configuration management through dedicated Configuration classes.
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(FormGenDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
