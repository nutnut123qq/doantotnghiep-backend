using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.ValueObjects;

namespace StockInvestment.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for User entity
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        // Email Value Object configuration
        builder.Property(u => u.Email)
            .HasConversion(
                email => email.Value,
                value => Email.Create(value))
            .HasColumnName("Email")
            .IsRequired()
            .HasMaxLength(255);

        // Create unique index on Email for fast lookups
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");

        // Password configuration
        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(u => u.FullName)
            .HasMaxLength(200)
            .IsRequired(false);

        // Role configuration with index
        builder.Property(u => u.Role)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(u => u.Role)
            .HasDatabaseName("IX_Users_Role");

        // IsActive index for filtering active users
        builder.HasIndex(u => u.IsActive)
            .HasDatabaseName("IX_Users_IsActive");

        builder.HasIndex(u => u.LockoutEnd)
            .HasDatabaseName("IX_Users_LockoutEnd");

        // IsEmailVerified index
        builder.HasIndex(u => u.IsEmailVerified)
            .HasDatabaseName("IX_Users_IsEmailVerified");

        // CreatedAt index for sorting
        builder.HasIndex(u => u.CreatedAt)
            .HasDatabaseName("IX_Users_CreatedAt");

        // Composite index for common queries (Role + IsActive)
        builder.HasIndex(u => new { u.Role, u.IsActive })
            .HasDatabaseName("IX_Users_Role_IsActive");

        // Other properties
        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .IsRequired(false);

        builder.Property(u => u.IsEmailVerified)
            .IsRequired();

        builder.Property(u => u.IsActive)
            .IsRequired();

        builder.Property(u => u.LockoutEnabled)
            .IsRequired();

        builder.Property(u => u.LockoutEnd)
            .IsRequired(false);
    }
}

