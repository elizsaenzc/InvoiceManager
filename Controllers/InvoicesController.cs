using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoiceManager.Data;
using InvoiceManager.Models;
using InvoiceManager.Services;

namespace InvoiceManager.Controllers;

public class InvoicesController : Controller
{
    private readonly AppDbContext _db;
    private readonly InvoiceNumberService _numbering;
    private readonly InvoiceStatusService _status;

    public InvoicesController(AppDbContext db, InvoiceNumberService numbering, InvoiceStatusService status)
    {
        _db = db;
        _numbering = numbering;
        _status = status;
    }

    // ─── Listado y exportación ──────────────────────────────────────────────

    public async Task<IActionResult> Index(string? q, string? status, int? clientId, int? year)
    {
        await _status.RefreshAsync();
        var (list, counts) = await FilterAsync(q, status, clientId, year);

        ViewBag.Q = q;
        ViewBag.Status = status;
        ViewBag.ClientId = clientId;
        ViewBag.Year = year;
        ViewBag.Counts = counts;
        ViewBag.Years = await YearsAsync();
        ViewBag.ClientOptions = await _db.Clients.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> Export(string? q, string? status, int? clientId, int? year)
    {
        await _status.RefreshAsync();
        var (list, _) = await FilterAsync(q, status, clientId, year);

        var csv = Csv.Build(
            new[] { "Número", "Fecha", "Vencimiento", "Cliente", "NIF", "Base imponible", "% IVA", "Cuota IVA", "Total", "Estado", "Fecha de cobro", "Forma de pago" },
            list.Select(i => new object?[]
            {
                i.DisplayNumber, i.IssueDate, i.DueDate, i.Client?.Name, i.Client?.TaxId,
                i.Subtotal, i.TaxRate, i.TaxAmount, i.Total, Fmt.StatusLabel(i.Status), i.PaidDate, i.PaymentMethod
            }));
        return File(csv, "text/csv; charset=utf-8", $"facturas_{DateTime.Today:yyyy-MM-dd}.csv");
    }

    private async Task<(List<Invoice> List, Dictionary<string, int> Counts)> FilterAsync(string? q, string? status, int? clientId, int? year)
    {
        var query = _db.Invoices.AsNoTracking().Include(i => i.Client).Include(i => i.Lines).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(i => i.Number.ToLower().Contains(term) ||
                                     i.Client!.Name.ToLower().Contains(term) ||
                                     (i.Client!.TaxId != null && i.Client.TaxId.ToLower().Contains(term)) ||
                                     i.Lines.Any(l => l.Description.ToLower().Contains(term)));
        }
        if (clientId is > 0) query = query.Where(i => i.ClientId == clientId);
        if (year is > 0) query = query.Where(i => i.IssueDate.Year == year);

        var all = await query.ToListAsync();
        var counts = Enum.GetValues<InvoiceStatus>().ToDictionary(s => s.ToString(), s => all.Count(i => i.Status == s));
        counts["All"] = all.Count;
        counts["Open"] = all.Count(i => i.IsOpen);

        IEnumerable<Invoice> filtered = all;
        if (status == "Open") filtered = all.Where(i => i.IsOpen);
        else if (Enum.TryParse<InvoiceStatus>(status, out var st)) filtered = all.Where(i => i.Status == st);

        var list = filtered
            .OrderBy(i => i.IsDraft ? 0 : 1)
            .ThenByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.Number)
            .ToList();
        return (list, counts);
    }

    private async Task<List<int>> YearsAsync()
    {
        var years = await _db.Invoices.Select(i => i.IssueDate.Year).Distinct().ToListAsync();
        return years.OrderByDescending(y => y).ToList();
    }

    // ─── Detalle ────────────────────────────────────────────────────────────

    public async Task<IActionResult> Details(int id)
    {
        await _status.RefreshAsync();
        var invoice = await LoadAsync(id);
        if (invoice == null) return NotFound();

        ViewBag.ClientHistory = await _db.Invoices.AsNoTracking()
            .Include(i => i.Lines)
            .Where(i => i.ClientId == invoice.ClientId && i.Id != id && i.Status != InvoiceStatus.Draft)
            .OrderByDescending(i => i.IssueDate).Take(5).ToListAsync();
        return View(invoice);
    }

    public async Task<IActionResult> Print(int id)
    {
        var invoice = await LoadAsync(id);
        return invoice == null ? NotFound() : View(invoice);
    }

    // ─── Alta ───────────────────────────────────────────────────────────────

    public async Task<IActionResult> Create(int? clientId, int? copyOf)
    {
        var model = new Invoice { IssueDate = DateTime.Today, TaxRate = 21m, Lines = new() { new InvoiceLine() } };

        if (copyOf is > 0)
        {
            var source = await LoadAsync(copyOf.Value);
            if (source != null)
            {
                model.ClientId = source.ClientId;
                model.TaxRate = source.TaxRate;
                model.Notes = source.Notes;
                model.Lines = source.Lines.Select(l => new InvoiceLine
                {
                    Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice
                }).ToList();
                ViewBag.CopyOf = source.DisplayNumber;
            }
        }
        else if (clientId is > 0)
        {
            model.ClientId = clientId.Value;
        }

        var client = model.ClientId > 0 ? await _db.Clients.FindAsync(model.ClientId) : null;
        model.DueDate = model.IssueDate.AddDays(client?.PaymentTermsDays ?? 30);

        await LoadFormDataAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Invoice invoice, string submit)
    {
        var issue = submit == "issue";
        await ValidateInvoiceAsync(invoice, issue, null);

        if (!ModelState.IsValid)
        {
            await LoadFormDataAsync();
            return View(invoice);
        }

        invoice.Id = 0;
        invoice.CreatedAt = invoice.UpdatedAt = DateTime.UtcNow;
        invoice.Status = InvoiceStatus.Draft;
        invoice.Number = string.Empty;
        foreach (var line in invoice.Lines) line.Id = 0;

        if (issue)
        {
            invoice.Number = await _numbering.NextAsync(invoice.IssueDate.Year);
            invoice.Status = InvoiceStatusService.OpenStatusFor(invoice.DueDate);
        }

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();

        TempData["Success"] = issue ? $"Factura {invoice.Number} emitida." : "Borrador guardado.";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    // ─── Edición ────────────────────────────────────────────────────────────

    public async Task<IActionResult> Edit(int id)
    {
        var invoice = await LoadAsync(id);
        if (invoice == null) return NotFound();
        if (!invoice.IsEditable)
        {
            TempData["Error"] = "Las facturas cobradas o anuladas no se pueden modificar. Puedes duplicarla para crear una nueva.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await LoadFormDataAsync();
        return View(invoice);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Invoice form, string submit)
    {
        var invoice = await _db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return NotFound();
        if (!invoice.IsEditable)
        {
            TempData["Error"] = "Esta factura ya no se puede modificar.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var issueNow = invoice.IsDraft && submit == "issue";
        await ValidateInvoiceAsync(form, issueNow || !invoice.IsDraft, id);

        if (!ModelState.IsValid)
        {
            form.Id = id;
            form.Number = invoice.Number;
            form.Status = invoice.Status;
            await LoadFormDataAsync();
            return View(form);
        }

        invoice.ClientId = form.ClientId;
        invoice.IssueDate = form.IssueDate;
        invoice.DueDate = form.DueDate;
        invoice.TaxRate = form.TaxRate;
        invoice.Notes = form.Notes;
        invoice.UpdatedAt = DateTime.UtcNow;

        // Se sustituyen las líneas por las del formulario (entidades nuevas, sin reutilizar ids)
        _db.InvoiceLines.RemoveRange(invoice.Lines);
        invoice.Lines = form.Lines.Select(l => new InvoiceLine
        {
            Description = l.Description.Trim(), Quantity = l.Quantity, UnitPrice = l.UnitPrice
        }).ToList();

        if (issueNow)
            invoice.Number = await _numbering.NextAsync(invoice.IssueDate.Year);
        if (!invoice.IsDraft || issueNow)
            invoice.Status = InvoiceStatusService.OpenStatusFor(invoice.DueDate);

        await _db.SaveChangesAsync();
        TempData["Success"] = issueNow ? $"Factura {invoice.Number} emitida." : "Cambios guardados.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ─── Acciones de estado ─────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(int id)
    {
        var invoice = await _db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return NotFound();
        if (!invoice.IsDraft) return Back(id, "Esta factura ya está emitida.");
        if (invoice.Lines.Count == 0) return Back(id, "Añade al menos un concepto antes de emitir.");

        // Al emitir se usa la fecha de hoy para respetar el orden de la serie; se conserva el plazo de pago
        var terms = Math.Max(0, (invoice.DueDate - invoice.IssueDate).Days);
        invoice.IssueDate = DateTime.Today;
        invoice.DueDate = DateTime.Today.AddDays(terms);
        invoice.Number = await _numbering.NextAsync(invoice.IssueDate.Year);
        invoice.Status = InvoiceStatusService.OpenStatusFor(invoice.DueDate);
        invoice.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Factura {invoice.Number} emitida con fecha de hoy.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterPayment(int id, DateTime? paidDate, string? method)
    {
        var invoice = await _db.Invoices.FindAsync(id);
        if (invoice == null) return NotFound();
        if (!invoice.IsOpen) return Back(id, "Solo se pueden cobrar facturas pendientes o vencidas.");

        var date = (paidDate ?? DateTime.Today).Date;
        if (date > DateTime.Today) return Back(id, "La fecha de cobro no puede ser futura.");
        if (date < invoice.IssueDate.Date) return Back(id, "La fecha de cobro no puede ser anterior a la emisión.");

        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidDate = date;
        invoice.PaymentMethod = PaymentMethods.All.Contains(method) ? method : "Transferencia";
        invoice.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Cobro de {invoice.Number} registrado.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UndoPayment(int id)
    {
        var invoice = await _db.Invoices.FindAsync(id);
        if (invoice == null) return NotFound();
        if (invoice.Status != InvoiceStatus.Paid) return Back(id, "La factura no está cobrada.");

        invoice.Status = InvoiceStatusService.OpenStatusFor(invoice.DueDate);
        invoice.PaidDate = null;
        invoice.PaymentMethod = null;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Cobro deshecho.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        var invoice = await _db.Invoices.FindAsync(id);
        if (invoice == null) return NotFound();
        if (!invoice.IsOpen) return Back(id, "Solo se pueden anular facturas emitidas pendientes de cobro.");

        invoice.Status = InvoiceStatus.Cancelled;
        var note = $"Anulada el {Fmt.Date(DateTime.Today)}" + (string.IsNullOrWhiteSpace(reason) ? "." : $": {reason.Trim()}");
        invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes) ? note : $"{invoice.Notes}\n{note}";
        if (invoice.Notes.Length > 500) invoice.Notes = invoice.Notes[..500];
        invoice.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Factura {invoice.Number} anulada. El número se conserva en la serie.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var invoice = await _db.Invoices.FindAsync(id);
        if (invoice == null) return NotFound();
        if (!invoice.IsDraft) return Back(id, "Solo se pueden eliminar borradores. Una factura emitida se anula.");

        _db.Invoices.Remove(invoice);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Borrador eliminado.";
        return RedirectToAction(nameof(Index));
    }

    // ─── Ayudas ─────────────────────────────────────────────────────────────

    private Task<Invoice?> LoadAsync(int id) => _db.Invoices.AsNoTracking()
        .Include(i => i.Client)
        .Include(i => i.Lines)
        .FirstOrDefaultAsync(i => i.Id == id);

    private IActionResult Back(int id, string error)
    {
        TempData["Error"] = error;
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Validación común de alta y edición. Ignora las líneas vacías del formulario.</summary>
    private async Task ValidateInvoiceAsync(Invoice invoice, bool issued, int? currentId)
    {
        invoice.Lines = (invoice.Lines ?? new()).Where(l => !string.IsNullOrWhiteSpace(l.Description)).ToList();

        // Las líneas vacías descartadas dejan errores de enlace: se revalida el modelo ya limpio
        ModelState.Clear();
        TryValidateModel(invoice);

        if (invoice.Lines.Count == 0)
            ModelState.AddModelError("Lines", "Añade al menos un concepto.");
        if (invoice.ClientId > 0 && !await _db.Clients.AnyAsync(c => c.Id == invoice.ClientId))
            ModelState.AddModelError(nameof(Invoice.ClientId), "El cliente no existe.");
        if (invoice.DueDate.Date < invoice.IssueDate.Date)
            ModelState.AddModelError(nameof(Invoice.DueDate), "El vencimiento no puede ser anterior a la emisión.");
        if (invoice.TaxRate is not (0m or 4m or 10m or 21m))
            ModelState.AddModelError(nameof(Invoice.TaxRate), "Tipo de IVA no válido.");

        if (!issued) return;

        if (invoice.IssueDate.Date > DateTime.Today)
            ModelState.AddModelError(nameof(Invoice.IssueDate), "Una factura emitida no puede tener fecha futura.");

        // Serie correlativa: la fecha no puede ser anterior a la última factura emitida de la serie
        var prefix = InvoiceNumberService.Prefix(invoice.IssueDate.Year);
        var lastDate = await _db.Invoices
            .Where(i => i.Number.StartsWith(prefix) && i.Id != (currentId ?? 0))
            .OrderByDescending(i => i.IssueDate)
            .Select(i => (DateTime?)i.IssueDate)
            .FirstOrDefaultAsync();
        var current = currentId == null ? null : await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == currentId);
        var keepsNumber = current is { IsDraft: false } && current.IssueDate.Year == invoice.IssueDate.Year;
        if (!keepsNumber && lastDate != null && invoice.IssueDate.Date < lastDate.Value.Date)
            ModelState.AddModelError(nameof(Invoice.IssueDate),
                $"La fecha no puede ser anterior a la última factura de la serie ({Fmt.Date(lastDate.Value)}).");
        if (current is { IsDraft: false } && current.IssueDate.Year != invoice.IssueDate.Year)
            ModelState.AddModelError(nameof(Invoice.IssueDate), "No se puede cambiar el año de una factura ya numerada.");
    }

    private async Task LoadFormDataAsync()
    {
        ViewBag.ClientOptions = await _db.Clients.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

        // Conceptos facturados en los últimos meses con su último precio, para autocompletar
        var since = DateTime.Today.AddMonths(-12);
        var lines = await _db.InvoiceLines.AsNoTracking()
            .Where(l => l.Invoice!.IssueDate >= since)
            .Select(l => new { l.Description, l.UnitPrice, IssueDate = l.Invoice!.IssueDate })
            .ToListAsync();
        ViewBag.Concepts = lines
            .Where(l => !l.Description.StartsWith("Anticipo") && !l.Description.StartsWith("Entrega final"))
            .GroupBy(l => l.Description)
            .Select(g => new KeyValuePair<string, decimal>(g.Key, g.OrderByDescending(x => x.IssueDate).First().UnitPrice))
            .OrderBy(k => k.Key)
            .ToList();
    }
}
