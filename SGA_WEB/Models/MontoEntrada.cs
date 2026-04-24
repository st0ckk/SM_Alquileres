using System.Globalization;

namespace SGA_WEB.Models;

public static class MontoEntrada
{
    private static readonly CultureInfo EsCr = CultureInfo.GetCultureInfo("es-CR");
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Formatear(decimal valor) => valor.ToString("N2", EsCr);

    public static bool TryParse(string? raw, out decimal valor, out string? mensajeError)
    {
        valor = 0m;
        mensajeError = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            mensajeError = "Indique el precio mensual.";
            return false;
        }

        var s = raw.Trim();
        while (s.Contains(' ', StringComparison.Ordinal))
        {
            s = s.Replace(" ", "", StringComparison.Ordinal);
        }

        if (decimal.TryParse(s, NumberStyles.Number, EsCr, out valor) && EsRango(valor))
        {
            return true;
        }

        if (decimal.TryParse(s, NumberStyles.Number, Inv, out valor) && EsRango(valor))
        {
            return true;
        }

        if (s.Count(c => c == ',') == 1 && !s.Contains('.'))
        {
            if (decimal.TryParse(s.Replace(",", "."), NumberStyles.Number, Inv, out valor) && EsRango(valor))
            {
                return true;
            }
        }

        mensajeError = "Precio no válido. Ejemplos: 1500, 1500,00, 1500.50 (máximo 999999,99).";
        return false;
    }

    private static bool EsRango(decimal v) => v >= 0m && v <= 999999.99m;
}
