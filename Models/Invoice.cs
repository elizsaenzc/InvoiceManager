using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

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

    /// <summary>Se asigna al emitir; los borradores no consumen número de la serie.</summary>
    [Display(Name = "Número")]
    [ValidateNever]
    public string Number { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un cliente")]
    [Display(Name = "Cliente")]
    public int ClientId { get; set; }

    [ValidateNever]
    public Client? Client { get; set; }

    [Required(ErrorMessage = "La fecha de emisión es obligatoria")]
    [Display(Name = "Fecha de emisión")]
    [DataType(DataType.Date)]
    public DateTime IssueDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "La fecha de vencimiento es obligatoria")]
    [Display(Name = "Vencimiento")]
    [DataType(DataType.Date)]
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(30);

    [Display(Name = "Estado")]
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    [Display(Name = "Notas")]
    [StringLength(500)]
    public string? Notes { get; set; }

    [Display(Name = "IVA")]
    [Range(0, 21, ErrorMessage = "Tipo de IVA no válido")]
    public decimal TaxRate { get; set; } = 21m;

    [Display(Name = "Fecha de cobro")]
    [DataType(DataType.Date)]
    public DateTime? PaidDate { get; set; }

    [Display(Name = "Forma de pago")]
    [StringLength(40)]
    public string? PaymentMethod { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<InvoiceLine> Lines { get; set; } = new();

    [NotMapped] public decimal Subtotal  => Math.Round(Lines.Sum(l => l.Total), 2, MidpointRounding.AwayFromZero);
    [NotMapped] public decimal TaxAmount => Math.Round(Subtotal * TaxRate / 100, 2, MidpointRounding.AwayFromZero);
    [NotMapped] public decimal Total     => Subtotal + TaxAmount;

    [NotMapped] public bool IsDraft     => Status == InvoiceStatus.Draft;
    [NotMapped] public bool IsOpen      => Status is InvoiceStatus.Pending or InvoiceStatus.Overdue;
    [NotMapped] public bool IsEditable  => Status is InvoiceStatus.Draft or InvoiceStatus.Pending or InvoiceStatus.Overdue;
    [NotMapped] public int  DaysOverdue => IsOpen ? Math.Max(0, (DateTime.Today - DueDate.Date).Days) : 0;
    [NotMapped] public string DisplayNumber => string.IsNullOrEmpty(Number) ? "Borrador" : Number;
}

public class InvoiceLine
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    [ValidateNever]
    public Invoice? Invoice { get; set; }

    [Required(ErrorMessage = "La descripción es obligatoria")]
    [StringLength(300)]
    [Display(Name = "Concepto")]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 99999, ErrorMessage = "La cantidad debe ser mayor que 0")]
    [Display(Name = "Cantidad")]
    public decimal Quantity { get; set; } = 1;

    [Range(0, 999999, ErrorMessage = "El precio no puede ser negativo")]
    [Display(Name = "Precio unitario")]
    public decimal UnitPrice { get; set; }

    [NotMapped] public decimal Total => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
}

public static class PaymentMethods
{
    public static readonly string[] All = { "Transferencia", "Domiciliación SEPA", "Tarjeta", "Bizum", "Efectivo" };
}
