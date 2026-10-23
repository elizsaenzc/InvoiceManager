namespace InvoiceManager.Models;

public class DashboardViewModel
{
    public decimal Invoiced12 { get; set; }
    public decimal Collected12 { get; set; }
    public decimal InvoicedThisMonth { get; set; }
    public decimal InvoicedLastMonth { get; set; }
    public decimal PendingAmount { get; set; }
    public int PendingCount { get; set; }
    public decimal OverdueAmount { get; set; }
    public int OverdueCount { get; set; }
    public int DraftCount { get; set; }
    public int AverageCollectionDays { get; set; }
    public int ActiveClients { get; set; }

    public List<MonthPoint> Months { get; set; } = new();
    public List<Invoice> OverdueInvoices { get; set; } = new();
    public List<Invoice> UpcomingInvoices { get; set; } = new();
    public List<Invoice> RecentInvoices { get; set; } = new();
    public List<ClientRanking> TopClients { get; set; } = new();
}

public record MonthPoint(string Label, decimal Invoiced, decimal Collected);

public record ClientRanking(int ClientId, string Name, string Initials, decimal Invoiced, int Invoices);

public class ClientListItem
{
    public Client Client { get; set; } = null!;
    public int InvoiceCount { get; set; }
    public decimal Invoiced { get; set; }
    public decimal Outstanding { get; set; }
    public int OverdueCount { get; set; }
    public DateTime? LastInvoice { get; set; }
}

public class ClientDetailsViewModel
{
    public Client Client { get; set; } = null!;
    public decimal Invoiced { get; set; }
    public decimal Collected { get; set; }
    public decimal Outstanding { get; set; }
    public decimal Overdue { get; set; }
    public int? AverageCollectionDays { get; set; }
    public List<Invoice> Invoices { get; set; } = new();
}

public class TaxReportViewModel
{
    public int Year { get; set; }
    public List<int> Years { get; set; } = new();
    public List<TaxQuarter> Quarters { get; set; } = new();
    public List<TaxRateRow> ByRate { get; set; } = new();
    public decimal TotalBase => Quarters.Sum(q => q.Base);
    public decimal TotalTax => Quarters.Sum(q => q.Tax);
    public decimal TotalCollected => Quarters.Sum(q => q.Collected);
    public int TotalInvoices => Quarters.Sum(q => q.Invoices);
}

public record TaxQuarter(int Quarter, string Period, int Invoices, decimal Base, decimal Tax, decimal Collected, bool IsCurrent);

public record TaxRateRow(decimal Rate, decimal Base, decimal Tax);
