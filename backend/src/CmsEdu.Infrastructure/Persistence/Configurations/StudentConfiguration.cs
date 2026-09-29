using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.RemainingSessions).IsConcurrencyToken();
        builder.ToTable("Students", t => {
            t.HasCheckConstraint("CK_Students_CourseMonths", "[CourseMonths] >= 0");
            t.HasCheckConstraint("CK_Students_RemainingSessions", "[RemainingSessions] >= 0");
        });

        builder.Property(s => s.StudentCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(s => s.StudentCode)
            .IsUnique();

        builder.Property(s => s.FullName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Gender)
            .HasConversion<int>();

        builder.Property(s => s.LearningNote)
            .HasMaxLength(500);

        builder.Property(s => s.IsArchived)
            .HasDefaultValue(false);
    }
}
