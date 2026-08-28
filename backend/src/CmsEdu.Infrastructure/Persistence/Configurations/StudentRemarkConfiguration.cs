using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class StudentRemarkConfiguration : IEntityTypeConfiguration<StudentRemark>
{
    public void Configure(EntityTypeBuilder<StudentRemark> builder)
    {
        builder.ToTable("StudentRemarks");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Content)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(r => r.CreatedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(r => r.UpdatedBy)
            .HasMaxLength(450);

        builder.HasOne(r => r.Enrollment)
            .WithMany(e => e.StudentRemarks)
            .HasForeignKey(r => r.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Session)
            .WithMany(s => s.StudentRemarks)
            .HasForeignKey(r => r.SessionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
