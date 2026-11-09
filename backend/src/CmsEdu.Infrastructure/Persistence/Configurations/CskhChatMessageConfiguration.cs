using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class CskhChatMessageConfiguration : IEntityTypeConfiguration<CskhChatMessage>
{
    public void Configure(EntityTypeBuilder<CskhChatMessage> builder)
    {
        builder.ToTable("CskhChatMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.SessionId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.Role)
            .HasMaxLength(20)
            .IsRequired();

        // Nội dung tin nhắn và JSON không giới hạn độ dài
        builder.Property(m => m.Content)
            .IsRequired();

        // ContextDataJson và SuggestedScriptJson có thể null (chỉ set cho vai trò "Assistant")
        builder.Property(m => m.ContextDataJson);

        builder.Property(m => m.SuggestedScriptJson);

        builder.Property(m => m.SentAt)
            .IsRequired();

        // Index để lấy lịch sử chat của một phiên
        builder.HasIndex(m => m.SessionId);

        builder.HasOne(m => m.Session)
            .WithMany(s => s.Messages)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
