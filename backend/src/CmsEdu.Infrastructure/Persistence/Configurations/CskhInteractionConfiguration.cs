using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class CskhInteractionConfiguration : IEntityTypeConfiguration<CskhInteraction>
{
    public void Configure(EntityTypeBuilder<CskhInteraction> builder)
    {
        builder.ToTable("CskhInteractions");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.StaffUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(i => i.Channel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.GuardianFeedback)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.Notes)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .IsRequired();

        // Indexes để lọc theo học sinh và nhân viên
        builder.HasIndex(i => i.StudentId);
        builder.HasIndex(i => i.StaffUserId);
        builder.HasIndex(i => i.CreatedAt);

        builder.HasOne(i => i.Student)
            .WithMany()
            .HasForeignKey(i => i.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Guardian)
            .WithMany()
            .HasForeignKey(i => i.GuardianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
