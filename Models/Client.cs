using System.ComponentModel.DataAnnotations;

namespace InvoiceManager.Models;

public class Client
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(150)]
    [Display(Name = "Razón social")]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Persona de contacto")]
    public string? ContactName { get; set; }

    [Required(ErrorMessage = "El email es obligatorio")]
    [EmailAddress(ErrorMessage = "Email no válido")]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Teléfono no válido")]
    [StringLength(30)]
    [Display(Name = "Teléfono")]
    public string? Phone { get; set; }

    [StringLength(300)]
    [Display(Name = "Dirección")]
    public string? Address { get; set; }

    [StringLength(120)]
    [Display(Name = "Población")]
    public string? City { get; set; }

    [StringLength(20)]
    [Display(Name = "NIF / CIF")]
    public string? TaxId { get; set; }

    [Range(0, 120, ErrorMessage = "Entre 0 y 120 días")]
    [Display(Name = "Plazo de pago (días)")]
    public int PaymentTermsDays { get; set; } = 30;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public string Initials =>
        string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => char.IsLetter(p[0]))
            .Take(2)
            .Select(p => char.ToUpperInvariant(p[0])));
}
