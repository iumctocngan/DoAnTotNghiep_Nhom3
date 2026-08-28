using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("Lessons");

        builder.HasKey(ls => ls.Id);

        builder.Property(ls => ls.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(ls => ls.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(ls => ls.Objective)
            .HasMaxLength(500);

        builder.Property(ls => ls.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(ls => new { ls.LevelId, ls.Code })
            .IsUnique();

        builder.HasIndex(ls => new { ls.LevelId, ls.SortOrder })
            .IsUnique();

        builder.HasOne(ls => ls.Level)
            .WithMany(l => l.Lessons)
            .HasForeignKey(ls => ls.LevelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
