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

        builder.Property(c => c.UserId)
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

        builder.Property(c => c.Summary)
            .IsRequired(false);

        builder.Property(c => c.SummaryProcessedCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasOne(c => c.Class)
            .WithMany()
            .HasForeignKey(c => c.ClassId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.ClassId, c.UserId, c.UpdatedAt });
    }
}

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

        builder.HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Class)
            .WithMany()
            .HasForeignKey(m => m.ClassId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(m => new { m.ConversationId, m.CreatedAt });
        builder.HasIndex(m => new { m.ClassId, m.UserId, m.CreatedAt });
    }
}
