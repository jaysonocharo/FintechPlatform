using Microsoft.EntityFrameworkCore;
using FintechBackend.Models;
using Microsoft.AspNetCore.DataProtection;

namespace FintechBackend.Data;

public class AppDbContext: DbContext
{
    private readonly IDataProtector _protector;
    public AppDbContext(DbContextOptions<AppDbContext> options, IDataProtectionProvider provider) : base(options)
    {
        // "FintechBackend.PII.v1" isolates cryptographic keys specifically for PII
        _protector = provider.CreateProtector("FintechBackend.PII.v1");
    }

    public DbSet<Transaction> Transactions{ get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Instantiate the encryption converter
        var encryptionConverter = new EncryptionConverter(_protector);

        // Apply encryption to sensitive PII columns
        modelBuilder.Entity<User>()
            .Property(u => u.FirstName)
            .HasConversion(encryptionConverter);

        modelBuilder.Entity<User>()
            .Property(u => u.LastName)
            .HasConversion(encryptionConverter);

        modelBuilder.Entity<Transaction>()
            .Property(t => t.AccountHolder)
            .HasConversion(encryptionConverter);

        // Enforce unique emails at the database level
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
            
        modelBuilder.Entity<Transaction>()
            .HasIndex(t => t.IdempotencyKey)
            .IsUnique();
    }
}