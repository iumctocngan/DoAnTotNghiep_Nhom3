using CmsEdu.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmsEdu.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens", table =>
        {
            table.HasCheckConstraint(
                "CK_RefreshTokens_Expiration",
                "[CreatedAt] < [ExpiresAt]");
        });

        builder.HasKey(token => token.Id);

        builder.Property(token => token.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(token => token.CreatedByIp)
            .HasMaxLength(45);

        builder.Property(token => token.RevokedByIp)
            .HasMaxLength(45);

        builder.Property(token => token.ReplacedByTokenHash)
            .HasMaxLength(64)
            .IsFixedLength();

        builder.Property(token => token.RevokeReason)
            .HasMaxLength(500);

        builder.HasIndex(token => token.TokenHash)
            .IsUnique();

        builder.HasIndex(token => new { token.UserId, token.FamilyId });

        builder.HasIndex(token => token.ExpiresAt);

        builder.HasOne(token => token.User)
            .WithMany(user => user.RefreshTokens)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
