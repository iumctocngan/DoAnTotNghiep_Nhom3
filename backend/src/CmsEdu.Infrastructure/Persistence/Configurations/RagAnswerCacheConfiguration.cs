using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class RagAnswerCacheConfiguration : IEntityTypeConfiguration<RagAnswerCache>
{
    public void Configure(EntityTypeBuilder<RagAnswerCache> builder)
    {
        builder.ToTable("RagAnswerCaches");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CacheKey)
            .HasMaxLength(128)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(c => c.NormalizedQuestion)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(c => c.Answer)
            .IsRequired();

        builder.Property(c => c.HitCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        builder.Property(c => c.LastAccessedAt)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        builder.HasIndex(c => c.CacheKey)
            .IsUnique();

        builder.HasIndex(c => c.ClassId);
    }
}
