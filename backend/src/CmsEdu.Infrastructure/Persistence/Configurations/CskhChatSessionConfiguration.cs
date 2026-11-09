using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class CskhChatSessionConfiguration : IEntityTypeConfiguration<CskhChatSession>
{
    public void Configure(EntityTypeBuilder<CskhChatSession> builder)
    {
        builder.ToTable("CskhChatSessions");

        // PK là UUID string, không phải auto-increment int
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(s => s.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.StartedAt)
            .IsRequired();

        builder.Property(s => s.LastActiveAt)
            .IsRequired();

        // Index để lọc theo nhân viên và học sinh
        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.StudentId);

        builder.HasOne(s => s.Student)
            .WithMany()
            .HasForeignKey(s => s.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
