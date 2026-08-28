using CmsEdu.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.EmployeeCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(u => u.EmployeeCode)
            .IsUnique();

        builder.Property(u => u.FullName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Phone)
            .HasMaxLength(20);

        builder.Property(u => u.EmploymentStatus)
            .HasConversion<int>()
            .IsRequired();
    }
}
