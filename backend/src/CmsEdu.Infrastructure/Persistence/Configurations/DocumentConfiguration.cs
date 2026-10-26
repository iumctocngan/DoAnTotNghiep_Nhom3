using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(d => d.Id);

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

        builder.Property(d => d.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();
    }
}
