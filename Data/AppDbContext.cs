using Fakturio.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Ico).HasMaxLength(20);
            entity.Property(c => c.Dic).HasMaxLength(20);
            entity.Property(c => c.IcDph).HasMaxLength(30);
            entity.Property(c => c.Email).HasMaxLength(200);
            entity.Property(c => c.Phone).HasMaxLength(50);
            entity.Property(c => c.Street).HasMaxLength(200);
            entity.Property(c => c.City).HasMaxLength(100);
            entity.Property(c => c.PostalCode).HasMaxLength(20);
            entity.Property(c => c.Country).HasMaxLength(2).HasDefaultValue("SK");
            entity.HasIndex(c => c.Ico);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Number).HasMaxLength(50).IsRequired();
            entity.HasIndex(i => i.Number).IsUnique();
            entity.Property(i => i.Currency).HasMaxLength(3).HasDefaultValue("EUR");
            entity.Property(i => i.VariableSymbol).HasMaxLength(20);
            entity.Property(i => i.ConstantSymbol).HasMaxLength(10);
            entity.Property(i => i.SpecificSymbol).HasMaxLength(20);
            entity.Property(i => i.PaymentMethod).HasMaxLength(50);
            entity.Property(i => i.BankIban).HasMaxLength(34);
            entity.Property(i => i.BankBic).HasMaxLength(11);
            entity.Property(i => i.Notes).HasMaxLength(2000);
            entity.Property(i => i.Subtotal).HasPrecision(18, 2);
            entity.Property(i => i.VatTotal).HasPrecision(18, 2);
            entity.Property(i => i.Total).HasPrecision(18, 2);
            entity.HasOne(i => i.Customer)
                .WithMany(c => c.Invoices)
                .HasForeignKey(i => i.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(ii => ii.Id);
            entity.Property(ii => ii.Description).HasMaxLength(500).IsRequired();
            entity.Property(ii => ii.Unit).HasMaxLength(20).HasDefaultValue("ks");
            entity.Property(ii => ii.Quantity).HasPrecision(18, 4);
            entity.Property(ii => ii.UnitPrice).HasPrecision(18, 4);
            entity.Property(ii => ii.VatRate).HasPrecision(5, 2);
            entity.Ignore(ii => ii.Subtotal);
            entity.Ignore(ii => ii.VatAmount);
            entity.Ignore(ii => ii.Total);
            entity.HasOne(ii => ii.Invoice)
                .WithMany(i => i.Items)
                .HasForeignKey(ii => ii.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
