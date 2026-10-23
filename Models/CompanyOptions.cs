namespace InvoiceManager.Models;

/// <summary>Datos fiscales del emisor (sección "Company" de appsettings).</summary>
public class CompanyOptions
{
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Web { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string Registry { get; set; } = string.Empty;
}
