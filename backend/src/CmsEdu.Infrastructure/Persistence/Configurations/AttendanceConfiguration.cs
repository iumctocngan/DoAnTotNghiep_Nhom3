using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.ToTable("Attendances");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.Note)
            .HasMaxLength(500);

        builder.Property(a => a.MarkedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(a => a.UpdatedBy)
            .HasMaxLength(450);

        builder.HasIndex(a => new { a.SessionId, a.EnrollmentId })
            .IsUnique();

        builder.HasOne(a => a.Session)
            .WithMany(s => s.Attendances)
            .HasForeignKey(a => a.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Enrollment)
            .WithMany(e => e.Attendances)
            .HasForeignKey(a => a.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
