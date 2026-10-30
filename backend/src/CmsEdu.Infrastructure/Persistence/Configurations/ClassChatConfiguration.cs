using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class ClassChatConversationConfiguration : IEntityTypeConfiguration<ClassChatConversation>
{
    public void Configure(EntityTypeBuilder<ClassChatConversation> builder)
    {
        builder.ToTable("ClassChatConversations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TeacherUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(c => c.Title)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        builder.HasOne(c => c.Class)
            .WithMany()
            .HasForeignKey(c => c.ClassId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.ClassId, c.TeacherUserId });
    }
}

public class ClassChatMessageConfiguration : IEntityTypeConfiguration<ClassChatMessage>
{
    public void Configure(EntityTypeBuilder<ClassChatMessage> builder)
    {
        builder.ToTable("ClassChatMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Role)
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(m => m.Content)
            .IsRequired();

        builder.Property(m => m.IsFromCache)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        builder.HasIndex(m => new { m.ConversationId, m.CreatedAt });
    }
}
