using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NewDynamicFormGenAPI.Models.Entities;

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
}