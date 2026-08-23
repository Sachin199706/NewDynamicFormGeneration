using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FormGen.Domain.Entities;

namespace FormGen.Infrastructure.Persistence.Configurations
{
    public class FormDataSourceConfig : IEntityTypeConfiguration<FormDataSource>
    {
        public void Configure(EntityTypeBuilder<FormDataSource> b)
        {
            b.ToTable("FormDataSources");
            b.HasKey(x => x.DataSourceId);
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.SourceType).HasMaxLength(20).IsRequired();
        }
    }

    public class FormDataSourceItemConfig : IEntityTypeConfiguration<FormDataSourceItem>
    {
        public void Configure(EntityTypeBuilder<FormDataSourceItem> b)
        {
            b.ToTable("FormDataSourceItems");
            b.HasKey(x => x.DataSourceItemId);
            b.Property(x => x.ItemValue).HasMaxLength(200).IsRequired();
            b.Property(x => x.ItemText).HasMaxLength(200).IsRequired();

            b.HasOne(x => x.DataSource).WithMany(d => d.Items)
                .HasForeignKey(x => x.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class FormSubmissionConfig : IEntityTypeConfiguration<FormSubmission>
    {
        public void Configure(EntityTypeBuilder<FormSubmission> b)
        {
            b.ToTable("FormSubmissions");
            b.HasKey(x => x.SubmissionId);
            b.Property(x => x.JsonData).IsRequired();

            b.HasOne(x => x.Form).WithMany(f => f.Submissions)
                .HasForeignKey(x => x.FormId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class FormSubmissionValueConfig : IEntityTypeConfiguration<FormSubmissionValue>
    {
        public void Configure(EntityTypeBuilder<FormSubmissionValue> b)
        {
            b.ToTable("FormSubmissionValues");
            b.HasKey(x => x.SubmissionValueId);

            b.HasOne(x => x.Submission).WithMany(s => s.Values)
                .HasForeignKey(x => x.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class FormFileConfig : IEntityTypeConfiguration<FormFile>
    {
        public void Configure(EntityTypeBuilder<FormFile> b)
        {
            b.ToTable("FormFiles");
            b.HasKey(x => x.FileId);
            b.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            b.Property(x => x.StoragePath).HasMaxLength(500).IsRequired();

            b.HasOne(x => x.Submission).WithMany(s => s.Files)
                .HasForeignKey(x => x.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class FormPublishHistoryConfig : IEntityTypeConfiguration<FormPublishHistory>
    {
        public void Configure(EntityTypeBuilder<FormPublishHistory> b)
        {
            b.ToTable("FormPublishHistory");
            b.HasKey(x => x.PublishHistoryId);
            b.Property(x => x.Notes).HasMaxLength(500);
        }
    }

    public class FormAuditLogConfig : IEntityTypeConfiguration<FormAuditLog>
    {
        public void Configure(EntityTypeBuilder<FormAuditLog> b)
        {
            b.ToTable("FormAuditLogs");
            b.HasKey(x => x.AuditLogId);
            b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        }
    }
}
