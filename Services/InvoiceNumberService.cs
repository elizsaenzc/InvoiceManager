using InvoiceManager.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceManager.Services;

/// <summary>
/// Serie correlativa por año: FAC-2026-0001. Se calcula a partir del número más alto emitido
/// (no del recuento), así que borrar o anular facturas nunca provoca números repetidos.
/// </summary>
public class InvoiceNumberService
{
    private readonly AppDbContext _db;
    public InvoiceNumberService(AppDbContext db) => _db = db;

    public static string Prefix(int year) => $"FAC-{year}-";

    public async Task<string> NextAsync(int year)
    {
        var prefix = Prefix(year);
        var numbers = await _db.Invoices
            .Where(i => i.Number.StartsWith(prefix))
            .Select(i => i.Number)
            .ToListAsync();

        var last = numbers
            .Select(n => int.TryParse(n[prefix.Length..], out var seq) ? seq : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{last + 1:D4}";
    }
}
