using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NewDynamicFormGenAPI.Models.Entities;

namespace FormGen.Infrastructure.Persistence.Configurations
{
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

    public class FormPublishHistoryConfig : IEntityTypeConfiguration<FormPublishHistory>
    {
        public void Configure(EntityTypeBuilder<FormPublishHistory> b)
        {
            b.ToTable("FormPublishHistory");
            b.HasKey(x => x.PublishHistoryId);
            b.Property(x => x.Notes).HasMaxLength(500);
        }
    }

}