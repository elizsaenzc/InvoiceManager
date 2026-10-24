using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoiceManager.Data;
using InvoiceManager.Models;
using InvoiceManager.Services;

namespace InvoiceManager.Controllers;

public class ClientsController : Controller
{
    private readonly AppDbContext _db;
    private readonly InvoiceStatusService _status;

    public ClientsController(AppDbContext db, InvoiceStatusService status)
    {
        _db = db;
        _status = status;
    }

    public async Task<IActionResult> Index(string? q, string? sort)
    {
        await _status.RefreshAsync();
        var query = _db.Clients.AsNoTracking().Include(c => c.Invoices).ThenInclude(i => i.Lines).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term) || c.Email.ToLower().Contains(term) ||
                                     (c.TaxId != null && c.TaxId.ToLower().Contains(term)) ||
                                     (c.City != null && c.City.ToLower().Contains(term)) ||
                                     (c.ContactName != null && c.ContactName.ToLower().Contains(term)));
        }

        var items = (await query.ToListAsync()).Select(c =>
        {
            var issued = c.Invoices.Where(i => i.Status is not (InvoiceStatus.Draft or InvoiceStatus.Cancelled)).ToList();
            return new ClientListItem
            {
                Client = c,
                InvoiceCount = issued.Count,
                Invoiced = issued.Sum(i => i.Total),
                Outstanding = issued.Where(i => i.IsOpen).Sum(i => i.Total),
                OverdueCount = issued.Count(i => i.Status == InvoiceStatus.Overdue),
                LastInvoice = issued.Select(i => (DateTime?)i.IssueDate).Max(),
            };
        });

        items = sort switch
        {
            "facturado" => items.OrderByDescending(i => i.Invoiced),
            "pendiente" => items.OrderByDescending(i => i.Outstanding).ThenBy(i => i.Client.Name),
            _ => items.OrderBy(i => i.Client.Name),
        };

        ViewBag.Q = q;
        ViewBag.Sort = sort ?? "nombre";
        return View(items.ToList());
    }

    public async Task<IActionResult> Details(int id)
    {
        await _status.RefreshAsync();
        var client = await _db.Clients.AsNoTracking()
            .Include(c => c.Invoices).ThenInclude(i => i.Lines)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (client == null) return NotFound();

        var issued = client.Invoices.Where(i => i.Status is not (InvoiceStatus.Draft or InvoiceStatus.Cancelled)).ToList();
        var paid = issued.Where(i => i.PaidDate != null).ToList();
        var vm = new ClientDetailsViewModel
        {
            Client = client,
            Invoiced = issued.Sum(i => i.Total),
            Collected = paid.Sum(i => i.Total),
            Outstanding = issued.Where(i => i.IsOpen).Sum(i => i.Total),
            Overdue = issued.Where(i => i.Status == InvoiceStatus.Overdue).Sum(i => i.Total),
            AverageCollectionDays = paid.Count == 0 ? null
                : (int)Math.Round(paid.Average(i => (i.PaidDate!.Value - i.IssueDate).TotalDays)),
            Invoices = client.Invoices.OrderBy(i => i.IsDraft ? 0 : 1)
                .ThenByDescending(i => i.IssueDate).ThenByDescending(i => i.Number).ToList(),
        };
        return View(vm);
    }

    public IActionResult Create() => View(new Client());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Client client)
    {
        Normalize(client);
        await ValidateTaxIdAsync(client);
        if (!ModelState.IsValid) return View(client);

        client.Id = 0;
        client.CreatedAt = DateTime.UtcNow;
        _db.Clients.Add(client);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Cliente «{client.Name}» creado.";
        return RedirectToAction(nameof(Details), new { id = client.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var client = await _db.Clients.FindAsync(id);
        return client == null ? NotFound() : View(client);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Client form)
    {
        var client = await _db.Clients.FindAsync(id);
        if (client == null) return NotFound();

        form.Id = id;
        Normalize(form);
        await ValidateTaxIdAsync(form);
        if (!ModelState.IsValid) return View(form);

        client.Name = form.Name;
        client.ContactName = form.ContactName;
        client.Email = form.Email;
        client.Phone = form.Phone;
        client.Address = form.Address;
        client.City = form.City;
        client.TaxId = form.TaxId;
        client.PaymentTermsDays = form.PaymentTermsDays;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Cliente actualizado.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var client = await _db.Clients.Include(c => c.Invoices).FirstOrDefaultAsync(c => c.Id == id);
        if (client == null) return NotFound();

        if (client.Invoices.Any())
        {
            TempData["Error"] = $"«{client.Name}» tiene {client.Invoices.Count} facturas y no se puede eliminar.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.Clients.Remove(client);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Cliente eliminado.";
        return RedirectToAction(nameof(Index));
    }

    private static void Normalize(Client c)
    {
        c.Name = c.Name?.Trim() ?? string.Empty;
        c.Email = c.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        c.TaxId = string.IsNullOrWhiteSpace(c.TaxId) ? null : c.TaxId.Replace(" ", "").Replace("-", "").ToUpperInvariant();
    }

    private async Task ValidateTaxIdAsync(Client c)
    {
        if (c.TaxId == null) return;
        if (!TaxId.IsValid(c.TaxId))
            ModelState.AddModelError(nameof(Client.TaxId), "NIF/CIF no válido. Revisa la letra o el dígito de control.");
        else if (await _db.Clients.AnyAsync(x => x.TaxId == c.TaxId && x.Id != c.Id))
            ModelState.AddModelError(nameof(Client.TaxId), "Ya existe un cliente con este NIF/CIF.");
    }
}
