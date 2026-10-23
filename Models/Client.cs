using System.ComponentModel.DataAnnotations;

namespace InvoiceManager.Models;

public class Client
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(150)]
    [Display(Name = "Nombre / Empresa")]
    public string Name { get; set; } = string.Empty;

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

    [StringLength(50)]
    [Display(Name = "NIF / CIF")]
    public string? TaxId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
