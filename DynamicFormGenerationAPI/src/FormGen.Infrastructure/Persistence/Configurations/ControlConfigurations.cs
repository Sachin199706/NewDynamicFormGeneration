using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FormGen.Domain.Entities;

namespace FormGen.Infrastructure.Persistence.Configurations
{
    public class ControlTypeConfig : IEntityTypeConfiguration<ControlType>
    {
        public void Configure(EntityTypeBuilder<ControlType> b)
        {
            b.ToTable("ControlTypes");
            b.HasKey(x => x.ControlTypeId);
            b.Property(x => x.ControlCode).HasMaxLength(50).IsRequired();
            b.HasIndex(x => x.ControlCode).IsUnique();
            b.Property(x => x.ControlName).HasMaxLength(100).IsRequired();
        }
    }

    public class FormControlConfig : IEntityTypeConfiguration<FormControl>
    {
        public void Configure(EntityTypeBuilder<FormControl> b)
        {
            b.ToTable("FormControls");
            b.HasKey(x => x.ControlId);
            b.HasIndex(x => new { x.FormVersionId, x.ControlKey }).IsUnique();
            b.Property(x => x.ControlKey).HasMaxLength(100).IsRequired();

            b.HasOne(x => x.FormVersion).WithMany(v => v.Controls)
                .HasForeignKey(x => x.FormVersionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.ControlType).WithMany()
                .HasForeignKey(x => x.ControlTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.ParentControl).WithMany()
                .HasForeignKey(x => x.ParentControlId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            b.HasOne(x => x.DataSource).WithMany()
                .HasForeignKey(x => x.DataSourceId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        }
    }

    public class FormLayoutConfig : IEntityTypeConfiguration<FormLayout>
    {
        public void Configure(EntityTypeBuilder<FormLayout> b)
        {
            b.ToTable("FormLayouts");
            b.HasKey(x => x.LayoutId);
            b.Property(x => x.LayoutType).HasMaxLength(20).IsRequired();

            b.HasOne(x => x.FormVersion).WithMany(v => v.Layouts)
                .HasForeignKey(x => x.FormVersionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.ParentLayout).WithMany()
                .HasForeignKey(x => x.ParentLayoutId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        }
    }

    public class FormRuleConfig : IEntityTypeConfiguration<FormRule>
    {
        public void Configure(EntityTypeBuilder<FormRule> b)
        {
            b.ToTable("FormRules");
            b.HasKey(x => x.RuleId);
            b.Property(x => x.RuleType).HasMaxLength(30).IsRequired();
            b.Property(x => x.ErrorMessage).HasMaxLength(300).IsRequired();
            b.Property(x => x.Severity).HasMaxLength(10).IsRequired();

            b.HasOne(x => x.FormVersion).WithMany(v => v.Rules)
                .HasForeignKey(x => x.FormVersionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Control).WithMany(c => c.Rules)
                .HasForeignKey(x => x.ControlId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
