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
        modelBuilder.Entity<Invoice>()
            .Property(i => i.TaxRate)
            .HasColumnType("decimal(5,2)");

        modelBuilder.Entity<InvoiceLine>()
            .Property(l => l.Quantity)
            .HasColumnType("decimal(10,2)");

        modelBuilder.Entity<InvoiceLine>()
            .Property(l => l.UnitPrice)
            .HasColumnType("decimal(10,2)");

        // Seed data
        modelBuilder.Entity<Client>().HasData(
            new Client { Id = 1, Name = "Acme Corp S.L.", Email = "contacto@acme.es", Phone = "+34 91 123 4567", Address = "Calle Gran Vía 45, Madrid", TaxId = "B12345678", CreatedAt = DateTime.UtcNow },
            new Client { Id = 2, Name = "Inversiones Norte S.A.", Email = "admin@invertnorte.com", Phone = "+34 93 987 6543", Address = "Paseo de Gracia 10, Barcelona", TaxId = "A87654321", CreatedAt = DateTime.UtcNow },
            new Client { Id = 3, Name = "TechStart Galicia", Email = "hola@techstart.gal", Phone = "+34 981 456 789", Address = "Rúa Real 22, A Coruña", TaxId = "B11223344", CreatedAt = DateTime.UtcNow }
        );
    }
}
