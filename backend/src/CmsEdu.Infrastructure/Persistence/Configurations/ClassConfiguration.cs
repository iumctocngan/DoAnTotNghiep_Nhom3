using CmsEdu.Domain.Entities;
using CmsEdu.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class ClassConfiguration : IEntityTypeConfiguration<Class>
{
    public void Configure(EntityTypeBuilder<Class> builder)
    {
        builder.ToTable("Classes");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ClassCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(c => c.ClassCode)
            .IsUnique();

        builder.Property(c => c.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.MainTeacherUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(c => c.Level)
            .WithMany(l => l.Classes)
            .HasForeignKey(c => c.LevelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.MainTeacherUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
