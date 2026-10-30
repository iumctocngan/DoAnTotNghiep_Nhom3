using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class ClassChatMessageConfiguration : IEntityTypeConfiguration<ClassChatMessage>
{
    public void Configure(EntityTypeBuilder<ClassChatMessage> builder)
    {
        builder.ToTable("ClassChatMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(m => m.Role)
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(m => m.Content)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        builder.HasOne(m => m.Class)
            .WithMany()
            .HasForeignKey(m => m.ClassId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.ClassId, m.UserId, m.CreatedAt });
    }
}
