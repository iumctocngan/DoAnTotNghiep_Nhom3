using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", table =>
        {
            table.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PaymentNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(p => p.PaymentNumber)
            .IsUnique();

        builder.Property(p => p.ReceiptNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(p => p.ReceiptNumber)
            .IsUnique();

        builder.Property(p => p.Amount)
            .HasPrecision(18, 0)
            .IsRequired();

        builder.Property(p => p.Method)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.Note)
            .HasMaxLength(500);

        builder.Property(p => p.CreatedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(p => p.CancelledBy)
            .HasMaxLength(450);

        builder.Property(p => p.CancelReason)
            .HasMaxLength(500);

        builder.HasOne(p => p.Invoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
