using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NewDynamicFormGenAPI.Models.Entities;


namespace FormGen.Infrastructure.Persistence.Configurations
{
    public class FormConfig : IEntityTypeConfiguration<Form>
    {
        public void Configure(EntityTypeBuilder<Form> b)
        {
            b.ToTable("Forms");
            b.HasKey(x => x.FormId);
            b.Property(x => x.FormCode).HasMaxLength(50).IsRequired();
            b.HasIndex(x => x.FormCode).IsUnique();
            b.Property(x => x.FormName).HasMaxLength(150).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500);
        }
    }

    public class FormVersionConfig : IEntityTypeConfiguration<FormVersion>
    {
        public void Configure(EntityTypeBuilder<FormVersion> b)
        {
            b.ToTable("FormVersions");
            b.HasKey(x => x.FormVersionId);
            b.HasIndex(x => new { x.FormId, x.VersionNo }).IsUnique();
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
            b.Property(x => x.VersionDescription).HasMaxLength(250);
            b.Property(x => x.FormDefinitionJson).IsRequired();

            b.HasOne(x => x.Form).WithMany(f => f.Versions)
                .HasForeignKey(x => x.FormId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
