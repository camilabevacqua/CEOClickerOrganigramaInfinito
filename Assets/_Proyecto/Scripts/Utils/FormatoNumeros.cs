using System;
using System.Globalization;

/// <summary>
/// Convierte números grandes en texto corto: 1.50K, 12.3M, 450B, 1.00T, Qa, Qi, Sx, Sp, Oc, No, Dc.
/// Menos de 1000 se muestra sin abreviar.
/// </summary>
public static class FormatoNumeros
{
    private static readonly string[] Sufijos =
    {
        "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc"
    };

    // Siempre punto decimal (1.50K), sin importar el idioma de la PC o del celular
    private static readonly CultureInfo Cultura = CultureInfo.InvariantCulture;

    /// <summary>Número abreviado, ej: 1.50K</summary>
    public static string Formatear(double valor)
    {
        if (double.IsNaN(valor)) return "0";
        if (double.IsInfinity(valor)) return "∞";
        if (valor < 0) return "-" + Formatear(-valor);

        if (valor < 1000) return FormatearChico(valor);

        int nivel = (int)Math.Floor(Math.Log10(valor) / 3.0);
        if (nivel < 1) nivel = 1;
        double escalado = valor / Math.Pow(1000, nivel);

        // Corrección por imprecisión o redondeo (ej: 999.999K tiene que mostrarse 1.00M)
        if (escalado < 1) { nivel--; escalado *= 1000; }
        if (Math.Round(escalado, 0, MidpointRounding.AwayFromZero) >= 1000) { nivel++; escalado /= 1000; }

        // Más allá de Dc: notación científica
        if (nivel >= Sufijos.Length) return valor.ToString("0.00e0", Cultura);

        return ConDecimales(escalado) + Sufijos[nivel];
    }

    /// <summary>Plata con signo, ej: $1.50K</summary>
    public static string Plata(double valor)
    {
        return "$" + Formatear(valor);
    }

    // Menos de 1000: enteros sin decimales; menos de 10 permite un decimal (útil para $/seg chicos como 0.4)
    private static string FormatearChico(double valor)
    {
        if (valor < 10)
        {
            double truncado = Math.Floor(valor * 10) / 10;
            return truncado.ToString("0.#", Cultura);
        }
        return Math.Floor(valor).ToString("0", Cultura);
    }

    // 3 cifras significativas: 1.50 / 12.3 / 450
    private static string ConDecimales(double escalado)
    {
        int decimales = escalado < 10 ? 2 : (escalado < 100 ? 1 : 0);
        double redondeado = Math.Round(escalado, decimales, MidpointRounding.AwayFromZero);

        if (decimales == 2 && redondeado >= 10) decimales = 1;
        else if (decimales == 1 && redondeado >= 100) decimales = 0;

        redondeado = Math.Round(escalado, decimales, MidpointRounding.AwayFromZero);
        string formato = decimales == 2 ? "0.00" : (decimales == 1 ? "0.0" : "0");
        return redondeado.ToString(formato, Cultura);
    }
}
