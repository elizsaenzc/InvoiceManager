using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoiceManager.Data;
using InvoiceManager.Models;
using InvoiceManager.Services;

namespace InvoiceManager.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly InvoiceStatusService _status;

    public HomeController(AppDbContext db, InvoiceStatusService status)
    {
        _db = db;
        _status = status;
    }

    public async Task<IActionResult> Index()
    {
        await _status.RefreshAsync();

        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var from = monthStart.AddMonths(-11);

        // SQLite no agrega decimales: los importes se calculan en memoria
        var all = await _db.Invoices.AsNoTracking()
            .Include(i => i.Client)
            .Include(i => i.Lines)
            .ToListAsync();

        var issued = all.Where(i => i.Status is not (InvoiceStatus.Draft or InvoiceStatus.Cancelled)).ToList();
        var open = issued.Where(i => i.IsOpen).ToList();
        var paid12 = issued.Where(i => i.PaidDate >= from).ToList();
        var last12 = issued.Where(i => i.IssueDate >= from).ToList();

        var vm = new DashboardViewModel
        {
            Invoiced12 = last12.Sum(i => i.Total),
            Collected12 = paid12.Sum(i => i.Total),
            InvoicedThisMonth = issued.Where(i => i.IssueDate >= monthStart).Sum(i => i.Total),
            InvoicedLastMonth = issued.Where(i => i.IssueDate >= monthStart.AddMonths(-1) && i.IssueDate < monthStart).Sum(i => i.Total),
            PendingAmount = open.Sum(i => i.Total),
            PendingCount = open.Count,
            OverdueAmount = open.Where(i => i.Status == InvoiceStatus.Overdue).Sum(i => i.Total),
            OverdueCount = open.Count(i => i.Status == InvoiceStatus.Overdue),
            DraftCount = all.Count(i => i.Status == InvoiceStatus.Draft),
            AverageCollectionDays = paid12.Count == 0 ? 0
                : (int)Math.Round(paid12.Average(i => (i.PaidDate!.Value - i.IssueDate).TotalDays)),
            ActiveClients = last12.Select(i => i.ClientId).Distinct().Count(),

            Months = Enumerable.Range(0, 12).Select(k =>
            {
                var m = from.AddMonths(k);
                var next = m.AddMonths(1);
                return new MonthPoint(
                    m.ToString("MMM", Fmt.Es).TrimEnd('.'),
                    issued.Where(i => i.IssueDate >= m && i.IssueDate < next).Sum(i => i.Subtotal),
                    issued.Where(i => i.PaidDate >= m && i.PaidDate < next).Sum(i => i.Subtotal));
            }).ToList(),

            OverdueInvoices = open.Where(i => i.Status == InvoiceStatus.Overdue)
                .OrderBy(i => i.DueDate).ToList(),
            UpcomingInvoices = open.Where(i => i.Status == InvoiceStatus.Pending && i.DueDate <= today.AddDays(15))
                .OrderBy(i => i.DueDate).Take(5).ToList(),
            RecentInvoices = issued.OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.Number)
                .Take(6).ToList(),
            // Agrupar por id: sin seguimiento, EF crea una instancia de cliente por fila
            TopClients = last12.GroupBy(i => i.ClientId)
                .Select(g => { var c = g.First().Client!; return new ClientRanking(c.Id, c.Name, c.Initials, g.Sum(i => i.Subtotal), g.Count()); })
                .OrderByDescending(r => r.Invoiced).Take(5).ToList(),
        };

        return View(vm);
    }

    public IActionResult Error() => View("Status", 500);

    [Route("/Home/Status/{code:int}")]
    public IActionResult Status(int code) => View("Status", code);
}
