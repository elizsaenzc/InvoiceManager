using InvoiceManager.Data;
using InvoiceManager.Models;
using Microsoft.EntityFrameworkCore;

namespace InvoiceManager.Services;

/// <summary>Mantiene sincronizado el estado "Vencida" con la fecha de vencimiento.</summary>
public class InvoiceStatusService
{
    private readonly AppDbContext _db;
    public InvoiceStatusService(AppDbContext db) => _db = db;

    public async Task RefreshAsync()
    {
        var today = DateTime.Today;
        var changed = await _db.Invoices
            .Where(i => (i.Status == InvoiceStatus.Pending && i.DueDate < today) ||
                        (i.Status == InvoiceStatus.Overdue && i.DueDate >= today))
            .ToListAsync();

        foreach (var inv in changed)
            inv.Status = inv.DueDate < today ? InvoiceStatus.Overdue : InvoiceStatus.Pending;

        if (changed.Count > 0)
            await _db.SaveChangesAsync();
    }

    /// <summary>Estado que corresponde a una factura emitida y no cobrada.</summary>
    public static InvoiceStatus OpenStatusFor(DateTime dueDate) =>
        dueDate.Date < DateTime.Today ? InvoiceStatus.Overdue : InvoiceStatus.Pending;
}
