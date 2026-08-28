using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("Sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.Note)
            .HasMaxLength(500);

        builder.HasOne(s => s.Class)
            .WithMany(c => c.Sessions)
            .HasForeignKey(s => s.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Lesson)
            .WithMany(l => l.Sessions)
            .HasForeignKey(s => s.LessonId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
