using System.Globalization;
using System.Text;

namespace InvoiceManager.Services;

/// <summary>CSV que Excel en español abre directamente: BOM UTF-8, separador ";" y coma decimal.</summary>
public static class Csv
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    public static byte[] Build(IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', headers.Select(Escape)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(';', row.Select(Format)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Format(object? value) => value switch
    {
        null => "",
        decimal d => d.ToString("0.00", Es),
        DateTime dt => dt.ToString("dd/MM/yyyy", Es),
        _ => Escape(value.ToString() ?? "")
    };

    private static string Escape(string s) =>
        s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
