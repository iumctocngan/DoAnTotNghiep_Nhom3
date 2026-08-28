using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class LevelConfiguration : IEntityTypeConfiguration<Level>
{
    public void Configure(EntityTypeBuilder<Level> builder)
    {
        builder.ToTable("Levels");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(l => new { l.CourseId, l.Code })
            .IsUnique();

        builder.HasIndex(l => new { l.CourseId, l.SortOrder })
            .IsUnique();

        builder.HasOne(l => l.Course)
            .WithMany(c => c.Levels)
            .HasForeignKey(l => l.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
