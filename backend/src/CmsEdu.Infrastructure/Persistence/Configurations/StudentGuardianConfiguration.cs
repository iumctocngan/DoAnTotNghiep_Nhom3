using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class StudentGuardianConfiguration : IEntityTypeConfiguration<StudentGuardian>
{
    public void Configure(EntityTypeBuilder<StudentGuardian> builder)
    {
        builder.ToTable("StudentGuardians");

        builder.HasKey(sg => new { sg.StudentId, sg.GuardianId });

        builder.Property(sg => sg.Relationship)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(sg => sg.IsPrimary)
            .HasDefaultValue(false);

        builder.HasIndex(sg => new { sg.StudentId, sg.IsPrimary })
            .HasFilter("[IsPrimary] = 1")
            .IsUnique();

        builder.HasOne(sg => sg.Student)
            .WithMany(s => s.StudentGuardians)
            .HasForeignKey(sg => sg.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sg => sg.Guardian)
            .WithMany(g => g.StudentGuardians)
            .HasForeignKey(sg => sg.GuardianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
