namespace InvoiceManager.Services;

/// <summary>Validación de NIF (DNI), NIE y CIF españoles con su carácter de control.</summary>
public static class TaxId
{
    private const string DniLetters = "TRWAGMYFPDXBNJZSQVHLCKE";

    public static bool IsValid(string value)
    {
        var v = value.Trim().ToUpperInvariant();
        if (v.Length != 9) return false;

        // DNI: 8 dígitos + letra
        if (char.IsDigit(v[0]))
            return int.TryParse(v[..8], out var dni) && v[8] == DniLetters[dni % 23];

        // NIE: X/Y/Z + 7 dígitos + letra
        if (v[0] is 'X' or 'Y' or 'Z')
        {
            var digits = (v[0] - 'X') + v[1..8];
            return int.TryParse(digits, out var nie) && v[8] == DniLetters[nie % 23];
        }

        // CIF: letra de entidad + 7 dígitos + control (dígito o letra)
        if (!"ABCDEFGHJNPQRSUVW".Contains(v[0]) || !v[1..8].All(char.IsDigit)) return false;
        var sum = 0;
        for (var i = 0; i < 7; i++)
        {
            var n = v[i + 1] - '0';
            if (i % 2 == 0) { n *= 2; sum += n / 10 + n % 10; }
            else sum += n;
        }
        var control = (10 - sum % 10) % 10;
        var letter = "JABCDEFGHI"[control];
        return v[0] switch
        {
            'A' or 'B' or 'E' or 'H' => v[8] == (char)('0' + control),
            'K' or 'P' or 'Q' or 'S' or 'N' or 'W' => v[8] == letter,
            _ => v[8] == (char)('0' + control) || v[8] == letter,
        };
    }
}
