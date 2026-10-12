using UnityEngine;

/// <summary>
/// Paleta de colores del juego. Cambiá los colores acá y usá
/// el menú CEO Clicker > Aplicar tema (reconstruir interfaz).
/// Los valores por defecto son la paleta pastel.
/// </summary>
[CreateAssetMenu(fileName = "Tema", menuName = "CEO Clicker/Tema de colores", order = 3)]
public class TemaColores : ScriptableObject
{
    [Header("Base")]
    public Color fondo = Hex("#FDEFF4");
    public Color panel = Hex("#FFFFFF");
    public Color tarjeta = Hex("#FFF6F9");
    public Color texto = Hex("#5B3A55");
    public Color textoSuave = Hex("#9C7A93");
    [Tooltip("Texto sobre botones y insignias de color.")]
    public Color textoBoton = Hex("#FFFFFF");

    [Header("Plata y botones")]
    public Color plata = Hex("#D65A92");
    public Color botonFacturar = Hex("#F28DB8");
    public Color botonFacturarSombra = Hex("#D06A98");
    public Color botonContratar = Hex("#5FC4A6");
    public Color botonMejorar = Hex("#A58AD8");
    [Tooltip("Pestaña o selector elegido (x1/x10/Máx).")]
    public Color seleccion = Hex("#9B7FD4");
    [Tooltip("Pestaña o selector sin elegir.")]
    public Color botonSecundario = Hex("#F6E3EC");

    [Header("Detalles")]
    public Color acento = Hex("#9B7FD4");
    public Color aviso = Hex("#E06C8C");
    public Color bloqueado = Hex("#DCCFD8");
    public Color barraFondo = Hex("#F8E1EA");
    public Color barraRelleno = Hex("#F28DB8");

    [Header("Etapas")]
    public Color etapaLograda = Hex("#4DB592");
    public Color etapaActual = Hex("#E0739F");
    public Color fondoEtapaActual = Hex("#FFE3EE");

    [Header("Puestos (Empleado → Vicepresidente)")]
    public Color[] coloresPuestos =
    {
        Hex("#8FA3E0"), Hex("#6CC3D9"), Hex("#6CCB9F"), Hex("#F5A26B"), Hex("#B18AE6")
    };

    [Header("Departamentos (RRHH, Marketing, Finanzas)")]
    public Color[] coloresDepartamentos =
    {
        Hex("#EE7FB0"), Hex("#F29A5C"), Hex("#9C84DB")
    };

    // Conversión en C# puro (los inicializadores de campos no pueden llamar a la API nativa de Unity)
    private static Color Hex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return new Color(1f, 0f, 1f);

        int valor = System.Convert.ToInt32(hex, 16);
        float r = ((valor >> 16) & 0xFF) / 255f;
        float g = ((valor >> 8) & 0xFF) / 255f;
        float b = (valor & 0xFF) / 255f;
        return new Color(r, g, b, 1f);
    }
}
