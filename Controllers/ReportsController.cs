using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoiceManager.Data;
using InvoiceManager.Models;
using InvoiceManager.Services;

namespace InvoiceManager.Controllers;

/// <summary>Resumen de IVA repercutido por trimestre (apoyo para el modelo 303).</summary>
public class ReportsController : Controller
{
    private readonly AppDbContext _db;
    public ReportsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(int? year)
    {
        var vm = await BuildAsync(year);
        return View(vm);
    }

    public async Task<IActionResult> Export(int? year)
    {
        var vm = await BuildAsync(year);
        var invoices = await IssuedAsync(vm.Year);
        var csv = Csv.Build(
            new[] { "Trimestre", "Número", "Fecha", "Cliente", "NIF", "Base imponible", "% IVA", "Cuota IVA", "Total", "Estado" },
            invoices.Select(i => new object?[]
            {
                $"{(i.IssueDate.Month - 1) / 3 + 1}T", i.Number, i.IssueDate, i.Client?.Name, i.Client?.TaxId,
                i.Subtotal, i.TaxRate, i.TaxAmount, i.Total, Fmt.StatusLabel(i.Status)
            }));
        return File(csv, "text/csv; charset=utf-8", $"libro_facturas_emitidas_{vm.Year}.csv");
    }

    private async Task<TaxReportViewModel> BuildAsync(int? year)
    {
        var years = (await _db.Invoices.Where(i => i.Number != "").Select(i => i.IssueDate.Year).Distinct().ToListAsync())
            .OrderByDescending(y => y).ToList();
        var y = year is > 0 && years.Contains(year.Value) ? year.Value : years.DefaultIfEmpty(DateTime.Today.Year).First();
        var invoices = await IssuedAsync(y);
        var today = DateTime.Today;

        var quarters = Enumerable.Range(1, 4).Select(q =>
        {
            var inQ = invoices.Where(i => (i.IssueDate.Month - 1) / 3 + 1 == q).ToList();
            var start = new DateTime(y, (q - 1) * 3 + 1, 1);
            var period = $"{start.ToString("MMM", Fmt.Es).TrimEnd('.')} – {start.AddMonths(2).ToString("MMM", Fmt.Es).TrimEnd('.')}";
            return new TaxQuarter(q, period, inQ.Count, inQ.Sum(i => i.Subtotal), inQ.Sum(i => i.TaxAmount),
                inQ.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.Total),
                today.Year == y && (today.Month - 1) / 3 + 1 == q);
        }).ToList();

        return new TaxReportViewModel
        {
            Year = y,
            Years = years,
            Quarters = quarters,
            ByRate = invoices.GroupBy(i => i.TaxRate).OrderByDescending(g => g.Key)
                .Select(g => new TaxRateRow(g.Key, g.Sum(i => i.Subtotal), g.Sum(i => i.TaxAmount))).ToList(),
        };
    }

    /// <summary>Facturas emitidas del año (las anuladas no computan).</summary>
    private async Task<List<Invoice>> IssuedAsync(int year) =>
        (await _db.Invoices.AsNoTracking().Include(i => i.Client).Include(i => i.Lines)
            .Where(i => i.IssueDate.Year == year && i.Number != "" && i.Status != InvoiceStatus.Cancelled)
            .ToListAsync())
        .OrderBy(i => i.Number).ToList();
}
