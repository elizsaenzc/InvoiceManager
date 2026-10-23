using InvoiceManager.Data;

namespace InvoiceManager.Services;

public class InvoiceNumberService
{
    private readonly AppDbContext _db;

    public InvoiceNumberService(AppDbContext db)
    {
        _db = db;
    }

    public string Generate()
    {
        var year = DateTime.Now.Year;
        var count = _db.Invoices.Count(i => i.IssueDate.Year == year) + 1;
        return $"INV-{year}-{count:D4}";
    }
}
