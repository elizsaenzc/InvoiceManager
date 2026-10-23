using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvoiceManager.Models;

public enum InvoiceStatus
{
    Draft,
    Pending,
    Paid,
    Overdue,
    Cancelled
}

public class Invoice
{
    public int Id { get; set; }

    [Display(Name = "Número")]
    public string Number { get; set; } = string.Empty;

    [Required]
    public int ClientId { get; set; }
    public Client? Client { get; set; }

    [Required(ErrorMessage = "La fecha de emisión es obligatoria")]
    [Display(Name = "Fecha de emisión")]
    [DataType(DataType.Date)]
    public DateTime IssueDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "La fecha de vencimiento es obligatoria")]
    [Display(Name = "Fecha de vencimiento")]
    [DataType(DataType.Date)]
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(30);

    [Display(Name = "Estado")]
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    [Display(Name = "Notas")]
    [StringLength(500)]
    public string? Notes { get; set; }

    [Display(Name = "% IVA")]
    [Range(0, 100)]
    public decimal TaxRate { get; set; } = 21m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();

    // Computed
    [NotMapped]
    public decimal Subtotal => Lines.Sum(l => l.Total);

    [NotMapped]
    public decimal TaxAmount => Subtotal * TaxRate / 100;

    [NotMapped]
    public decimal Total => Subtotal + TaxAmount;

    [NotMapped]
    public bool IsOverdue => Status == InvoiceStatus.Pending && DueDate < DateTime.Today;
}

public class InvoiceLine
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    [Required(ErrorMessage = "La descripción es obligatoria")]
    [StringLength(300)]
    [Display(Name = "Descripción")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 99999, ErrorMessage = "La cantidad debe ser mayor que 0")]
    [Display(Name = "Cantidad")]
    public decimal Quantity { get; set; } = 1;

    [Required]
    [Range(0, 999999, ErrorMessage = "El precio debe ser positivo")]
    [Display(Name = "Precio unitario")]
    public decimal UnitPrice { get; set; }

    [NotMapped]
    public decimal Total => Quantity * UnitPrice;
}
