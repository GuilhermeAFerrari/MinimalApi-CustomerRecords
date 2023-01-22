using Microsoft.EntityFrameworkCore;
using MinimalApi_CustomerRecords.Models;

namespace MinimalApi_CustomerRecords.Data;

public class MinimalContextDb : DbContext
{
    public MinimalContextDb(DbContextOptions<MinimalContextDb> options) : base(options) { }

    public DbSet<Customer> Customers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>()
            .HasKey(p => p.Id);

        modelBuilder.Entity<Customer>()
            .Property(p => p.Name)
            .IsRequired()
            .HasColumnType("varchar(200)");

        modelBuilder.Entity<Customer>()
            .Property(p => p.Document)
            .IsRequired()
            .HasColumnType("varchar(14)");

        modelBuilder.Entity<Customer>()
            .ToTable("Customers");

        base.OnModelCreating(modelBuilder);
    }
}