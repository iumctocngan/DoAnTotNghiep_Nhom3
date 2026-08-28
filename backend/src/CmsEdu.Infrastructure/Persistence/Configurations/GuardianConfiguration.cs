using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> builder)
    {
        builder.ToTable("Guardians");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.FullName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(g => g.Phone)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(g => g.Email)
            .HasMaxLength(100);

        builder.Property(g => g.IsActive)
            .HasDefaultValue(true);
    }
}
