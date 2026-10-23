using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoiceManager.Data;
using InvoiceManager.Models;

namespace InvoiceManager.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        // Auto-mark overdue invoices
        var overdueInvoices = await _db.Invoices
            .Where(i => i.Status == InvoiceStatus.Pending && i.DueDate < DateTime.Today)
            .ToListAsync();

        foreach (var inv in overdueInvoices)
            inv.Status = InvoiceStatus.Overdue;

        if (overdueInvoices.Any())
            await _db.SaveChangesAsync();

        var allInvoices = await _db.Invoices
            .Include(i => i.Client)
            .Include(i => i.Lines)
            .ToListAsync();

        var vm = new DashboardViewModel
        {
            TotalInvoices = allInvoices.Count,
            PendingCount  = allInvoices.Count(i => i.Status == InvoiceStatus.Pending),
            PaidCount     = allInvoices.Count(i => i.Status == InvoiceStatus.Paid),
            OverdueCount  = allInvoices.Count(i => i.Status == InvoiceStatus.Overdue),

            TotalRevenue   = allInvoices.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.Total),
            PendingAmount  = allInvoices.Where(i => i.Status == InvoiceStatus.Pending).Sum(i => i.Total),
            OverdueAmount  = allInvoices.Where(i => i.Status == InvoiceStatus.Overdue).Sum(i => i.Total),

            TotalClients   = await _db.Clients.CountAsync(),

            RecentInvoices = allInvoices.OrderByDescending(i => i.CreatedAt).Take(5).ToList(),
            OverdueInvoices = allInvoices.Where(i => i.Status == InvoiceStatus.Overdue).ToList()
        };

        return View(vm);
    }
}
