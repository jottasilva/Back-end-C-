using AuthService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(user => user.Id);
            builder.HasIndex(user => user.Email).IsUnique();
            builder.Property(user => user.Email).HasMaxLength(180).IsRequired();
            builder.Property(user => user.Name).HasMaxLength(120).IsRequired();
            builder.Property(user => user.PasswordHash).HasMaxLength(200).IsRequired();
            builder.Property(user => user.Role).HasMaxLength(20).IsRequired();
            builder.Property(user => user.Permissions).HasMaxLength(300).IsRequired();
            builder.Property(user => user.AvatarUrl).HasColumnType("text").IsRequired();
            builder.Property(user => user.CreatedAt).IsRequired();
        });
    }
}
