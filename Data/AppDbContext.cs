using Microsoft.EntityFrameworkCore;
using InvoiceManager.Models;

namespace InvoiceManager.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>(e =>
        {
            e.Property(i => i.TaxRate).HasColumnType("decimal(5,2)");
            // Un número de factura no puede repetirse (los borradores no tienen número)
            e.HasIndex(i => i.Number).IsUnique().HasFilter("\"Number\" <> ''");
            e.HasMany(i => i.Lines).WithOne(l => l.Invoice!).HasForeignKey(l => l.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);
            // Un cliente con facturas no se borra en cascada: se bloquea desde el controlador
            e.HasOne(i => i.Client).WithMany(c => c.Invoices).HasForeignKey(i => i.ClientId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceLine>(e =>
        {
            e.Property(l => l.Quantity).HasColumnType("decimal(10,2)");
            e.Property(l => l.UnitPrice).HasColumnType("decimal(10,2)");
        });
    }
}
