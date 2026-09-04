using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("Enrollments", table =>
        {
            table.HasCheckConstraint(
                "CK_Enrollments_DateRange",
                "[EndDate] IS NULL OR [StartDate] <= [EndDate]");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.EndReason)
            .HasMaxLength(500);

        builder.Property(e => e.PauseReason)
            .HasMaxLength(500);

        builder.HasIndex(e => e.StudentId)
            .HasDatabaseName("UX_Enrollments_Student_ActiveOrPaused")
            .HasFilter("[Status] IN (1, 2)")
            .IsUnique();

        builder.HasOne(e => e.Student)
            .WithMany(s => s.Enrollments)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Class)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.ClassId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
