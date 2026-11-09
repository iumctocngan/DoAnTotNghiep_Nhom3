using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class StudentRenewalPredictionConfiguration : IEntityTypeConfiguration<StudentRenewalPrediction>
{
    public void Configure(EntityTypeBuilder<StudentRenewalPrediction> builder)
    {
        builder.ToTable("StudentRenewalPredictions", table =>
        {
            table.HasCheckConstraint("CK_SRP_Probability", "[RenewalProbability] >= 0.0 AND [RenewalProbability] <= 1.0");
            table.HasCheckConstraint("CK_SRP_DaysUntilPeriodEnd", "[DaysUntilPeriodEnd] >= 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.RenewalProbability)
            .IsRequired();

        builder.Property(p => p.RiskLevel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.DaysUntilPeriodEnd)
            .IsRequired();

        builder.Property(p => p.TopDriversJson)
            .IsRequired();

        builder.Property(p => p.ModelVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        // Indexes cho truy vấn thường dùng: lọc theo học sinh, lớp, ngày dự đoán
        builder.HasIndex(p => p.StudentId);
        builder.HasIndex(p => p.EnrollmentId);
        builder.HasIndex(p => new { p.StudentId, p.PredictionDate });

        builder.HasOne(p => p.Student)
            .WithMany()
            .HasForeignKey(p => p.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Enrollment)
            .WithMany()
            .HasForeignKey(p => p.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
