using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoiceManager.Data;
using InvoiceManager.Models;

namespace InvoiceManager.Controllers;

public class ClientsController : Controller
{
    private readonly AppDbContext _db;
    public ClientsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? search)
    {
        var query = _db.Clients.Include(c => c.Invoices).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || c.Email.Contains(search));

        ViewBag.Search = search;
        return View(await query.OrderBy(c => c.Name).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var client = await _db.Clients
            .Include(c => c.Invoices).ThenInclude(i => i.Lines)
            .FirstOrDefaultAsync(c => c.Id == id);

        return client == null ? NotFound() : View(client);
    }

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Client client)
    {
        if (!ModelState.IsValid) return View(client);
        _db.Clients.Add(client);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Cliente «{client.Name}» creado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var client = await _db.Clients.FindAsync(id);
        return client == null ? NotFound() : View(client);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Client client)
    {
        if (id != client.Id) return BadRequest();
        if (!ModelState.IsValid) return View(client);

        _db.Update(client);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Cliente actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var client = await _db.Clients.FindAsync(id);
        if (client != null)
        {
            _db.Clients.Remove(client);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Cliente eliminado.";
        }
        return RedirectToAction(nameof(Index));
    }
}
