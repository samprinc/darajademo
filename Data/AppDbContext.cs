using DarajaDemo.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarajaDemo.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<MpesaTransaction> MpesaTransactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MpesaTransaction>(entity =>
        {
            entity.HasIndex(e => e.TransId).IsUnique();
            entity.HasIndex(e => e.CheckoutRequestId);
            entity.HasIndex(e => e.PhoneNumber);
        });
    }
}