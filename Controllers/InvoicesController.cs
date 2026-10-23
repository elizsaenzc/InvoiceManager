using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InvoiceManager.Data;
using InvoiceManager.Models;
using InvoiceManager.Services;
using System.Text;

namespace InvoiceManager.Controllers;

public class InvoicesController : Controller
{
    private readonly AppDbContext _db;
    private readonly InvoiceNumberService _numbering;

    public InvoicesController(AppDbContext db, InvoiceNumberService numbering)
    {
        _db = db;
        _numbering = numbering;
    }

    public async Task<IActionResult> Index(string? search, string? status)
    {
        var query = _db.Invoices
            .Include(i => i.Client)
            .Include(i => i.Lines)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(i => i.Number.Contains(search) || i.Client!.Name.Contains(search));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InvoiceStatus>(status, out var s))
            query = query.Where(i => i.Status == s);

        ViewBag.Search = search;
        ViewBag.StatusFilter = status;
        ViewBag.Statuses = Enum.GetValues<InvoiceStatus>()
            .Select(s => new SelectListItem(StatusLabel(s), s.ToString()));

        return View(await query.OrderByDescending(i => i.IssueDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Client)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id);

        return invoice == null ? NotFound() : View(invoice);
    }

    public async Task<IActionResult> Create()
    {
        await LoadClients();
        var model = new Invoice
        {
            IssueDate = DateTime.Today,
            DueDate   = DateTime.Today.AddDays(30),
            Lines     = new List<InvoiceLine> { new() }
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Invoice invoice)
    {
        // Remove lines with empty description
        invoice.Lines = invoice.Lines
            .Where(l => !string.IsNullOrWhiteSpace(l.Description))
            .ToList();

        if (!invoice.Lines.Any())
            ModelState.AddModelError("Lines", "Añade al menos una línea a la factura.");

        if (!ModelState.IsValid)
        {
            await LoadClients(invoice.ClientId);
            return View(invoice);
        }

        invoice.Number    = _numbering.Generate();
        invoice.CreatedAt = DateTime.UtcNow;
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.Status    = InvoiceStatus.Pending;

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Factura {invoice.Number} creada correctamente.";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null) return NotFound();
        await LoadClients(invoice.ClientId);
        return View(invoice);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Invoice invoice)
    {
        if (id != invoice.Id) return BadRequest();

        invoice.Lines = invoice.Lines
            .Where(l => !string.IsNullOrWhiteSpace(l.Description))
            .ToList();

        if (!invoice.Lines.Any())
            ModelState.AddModelError("Lines", "Añade al menos una línea.");

        if (!ModelState.IsValid)
        {
            await LoadClients(invoice.ClientId);
            return View(invoice);
        }

        // Keep existing lines, remove deleted ones, add new ones
        var existing = await _db.InvoiceLines.Where(l => l.InvoiceId == id).ToListAsync();
        _db.InvoiceLines.RemoveRange(existing);

        invoice.UpdatedAt = DateTime.UtcNow;
        _db.Update(invoice);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Factura actualizada.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, InvoiceStatus status)
    {
        var invoice = await _db.Invoices.FindAsync(id);
        if (invoice == null) return NotFound();

        invoice.Status    = status;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Estado cambiado a «{StatusLabel(status)}».";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var invoice = await _db.Invoices.FindAsync(id);
        if (invoice != null)
        {
            _db.Invoices.Remove(invoice);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Factura eliminada.";
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Print(int id)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Client)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null) return NotFound();
        return View(invoice);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task LoadClients(int? selected = null)
    {
        ViewBag.Clients = new SelectList(
            await _db.Clients.OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", selected);
    }

    public static string StatusLabel(InvoiceStatus s) => s switch
    {
        InvoiceStatus.Draft     => "Borrador",
        InvoiceStatus.Pending   => "Pendiente",
        InvoiceStatus.Paid      => "Pagado",
        InvoiceStatus.Overdue   => "Vencido",
        InvoiceStatus.Cancelled => "Cancelado",
        _ => s.ToString()
    };
}
