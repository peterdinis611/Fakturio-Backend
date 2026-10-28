using Fakturio.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
    public DbSet<ServiceCatalogItem> ServiceCatalogItems => Set<ServiceCatalogItem>();

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

        modelBuilder.Entity<CompanySettings>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
            entity.Property(c => c.LegalForm).HasMaxLength(50);
            entity.Property(c => c.Ico).HasMaxLength(20);
            entity.Property(c => c.Dic).HasMaxLength(20);
            entity.Property(c => c.IcDph).HasMaxLength(30);
            entity.Property(c => c.CommercialRegister).HasMaxLength(500);
            entity.Property(c => c.Email).HasMaxLength(200);
            entity.Property(c => c.InvoiceEmail).HasMaxLength(200);
            entity.Property(c => c.Phone).HasMaxLength(50);
            entity.Property(c => c.Website).HasMaxLength(300);
            entity.Property(c => c.ContactPerson).HasMaxLength(200);
            entity.Property(c => c.Street).HasMaxLength(200);
            entity.Property(c => c.City).HasMaxLength(100);
            entity.Property(c => c.PostalCode).HasMaxLength(20);
            entity.Property(c => c.Country).HasMaxLength(2).HasDefaultValue("SK");
            entity.Property(c => c.BankIban).HasMaxLength(34);
            entity.Property(c => c.BankBic).HasMaxLength(11);
            entity.Property(c => c.BankName).HasMaxLength(100);
            entity.Property(c => c.DefaultVatRate).HasPrecision(5, 2);
            entity.Property(c => c.DefaultCurrency).HasMaxLength(3).HasDefaultValue("EUR");
            entity.Property(c => c.InvoicePrefix).HasMaxLength(20);
            entity.Property(c => c.DefaultPaymentMethod).HasMaxLength(50).HasDefaultValue("bank_transfer");
            entity.Property(c => c.DefaultConstantSymbol).HasMaxLength(10);
            entity.Property(c => c.DefaultInvoiceNotes).HasMaxLength(2000);
            entity.Property(c => c.InvoiceFooter).HasMaxLength(2000);
            entity.Property(c => c.LogoUrl).HasMaxLength(500);
            entity.Property(c => c.Notes).HasMaxLength(2000);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Number).HasMaxLength(50).IsRequired();
            entity.HasIndex(i => i.Number).IsUnique();
            entity.Property(i => i.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(i => i.CustomerIco).HasMaxLength(20);
            entity.Property(i => i.CustomerDic).HasMaxLength(20);
            entity.Property(i => i.CustomerIcDph).HasMaxLength(30);
            entity.Property(i => i.CustomerEmail).HasMaxLength(200);
            entity.Property(i => i.CustomerStreet).HasMaxLength(200);
            entity.Property(i => i.CustomerCity).HasMaxLength(100);
            entity.Property(i => i.CustomerPostalCode).HasMaxLength(20);
            entity.Property(i => i.CustomerCountry).HasMaxLength(2).HasDefaultValue("SK");
            entity.Property(i => i.SellerName).HasMaxLength(200);
            entity.Property(i => i.SellerIco).HasMaxLength(20);
            entity.Property(i => i.SellerDic).HasMaxLength(20);
            entity.Property(i => i.SellerIcDph).HasMaxLength(30);
            entity.Property(i => i.SellerIsVatPayer).HasDefaultValue(true);
            entity.Property(i => i.SellerStreet).HasMaxLength(200);
            entity.Property(i => i.SellerCity).HasMaxLength(100);
            entity.Property(i => i.SellerPostalCode).HasMaxLength(20);
            entity.Property(i => i.SellerCountry).HasMaxLength(2);
            entity.Property(i => i.Currency).HasMaxLength(3).HasDefaultValue("EUR");
            entity.Property(i => i.VariableSymbol).HasMaxLength(20);
            entity.Property(i => i.ConstantSymbol).HasMaxLength(10);
            entity.Property(i => i.SpecificSymbol).HasMaxLength(20);
            entity.Property(i => i.PaymentMethod).HasMaxLength(50);
            entity.Property(i => i.BankIban).HasMaxLength(34);
            entity.Property(i => i.BankBic).HasMaxLength(11);
            entity.Property(i => i.Notes).HasMaxLength(2000);
            entity.Property(i => i.CancelReason).HasMaxLength(500);
            entity.Property(i => i.Subtotal).HasPrecision(18, 2);
            entity.Property(i => i.VatTotal).HasPrecision(18, 2);
            entity.Property(i => i.Total).HasPrecision(18, 2);
            entity.HasIndex(i => i.Status);
            entity.HasIndex(i => i.IssueDate);
            entity.HasIndex(i => i.DueDate);
            entity.HasIndex(i => i.VariableSymbol);
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

        modelBuilder.Entity<ServiceCatalogItem>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Description).HasMaxLength(500).IsRequired();
            entity.Property(s => s.Unit).HasMaxLength(20).HasDefaultValue("ks");
            entity.Property(s => s.UnitPrice).HasPrecision(18, 4);
            entity.Property(s => s.VatRate).HasPrecision(5, 2);
            entity.HasIndex(s => s.IsActive);
            entity.HasIndex(s => s.SortOrder);
        });
    }
}
