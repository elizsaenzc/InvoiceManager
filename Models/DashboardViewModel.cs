namespace InvoiceManager.Models;

public class DashboardViewModel
{
    public int TotalInvoices { get; set; }
    public int PendingCount { get; set; }
    public int PaidCount { get; set; }
    public int OverdueCount { get; set; }

    public decimal TotalRevenue { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal OverdueAmount { get; set; }

    public List<Invoice> RecentInvoices { get; set; } = new();
    public List<Invoice> OverdueInvoices { get; set; } = new();

    public int TotalClients { get; set; }
}
