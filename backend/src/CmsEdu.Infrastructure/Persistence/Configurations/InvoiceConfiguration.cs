using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", table =>
        {
            table.HasCheckConstraint("CK_Invoices_AmountDue", "[AmountDue] > 0");
            table.HasCheckConstraint("CK_Invoices_Period", "[PeriodStart] <= [PeriodEnd]");
            table.HasCheckConstraint("CK_Invoices_DueDate", "[DueDate] <= [PeriodEnd]");
            table.HasCheckConstraint(
                "CK_Invoices_SixMonthPeriod",
                "[PeriodEnd] = DATEADD(day, -1, DATEADD(month, 6, [PeriodStart]))");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(i => i.InvoiceNumber)
            .IsUnique();

        builder.Property(i => i.AmountDue)
            .HasPrecision(18, 0)
            .IsRequired();

        builder.Property(i => i.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.Note)
            .HasMaxLength(500);

        builder.Property(i => i.CreatedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(i => i.CancelledBy)
            .HasMaxLength(450);

        builder.Property(i => i.CancelReason)
            .HasMaxLength(500);

        builder.HasOne(i => i.Enrollment)
            .WithMany(e => e.Invoices)
            .HasForeignKey(i => i.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
