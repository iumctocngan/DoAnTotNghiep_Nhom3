using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

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
