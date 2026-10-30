using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Title)
            .HasMaxLength(255);

        builder.Property(d => d.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.StoragePath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.FileSize)
            .IsRequired();

        builder.Property(d => d.DocumentType)
            .HasMaxLength(10)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(d => d.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("processing")
            .IsRequired();

        builder.Property(d => d.ErrorMessage)
            .HasMaxLength(500);

        builder.Property(d => d.Scope)
            .HasConversion<int>()
            .HasDefaultValue(KnowledgeScope.Global)
            .IsRequired();

        builder.Property(d => d.UploadedByUserId)
            .HasMaxLength(450);

        builder.Property(d => d.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        builder.HasOne(d => d.Course)
            .WithMany()
            .HasForeignKey(d => d.CourseId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.Level)
            .WithMany()
            .HasForeignKey(d => d.LevelId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.Class)
            .WithMany()
            .HasForeignKey(d => d.ClassId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(d => new { d.Scope, d.CourseId, d.LevelId, d.ClassId });
    }
}
