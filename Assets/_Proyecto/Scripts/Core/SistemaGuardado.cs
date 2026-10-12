using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Lee y escribe la partida como JSON en Application.persistentDataPath/partida.json.
/// Escribe primero un archivo temporal y después lo reemplaza, para no dejar
/// un guardado a medias si la app se cierra justo en ese momento.
/// </summary>
public static class SistemaGuardado
{
    private const string NombreArchivo = "partida.json";

    public static string Ruta => Path.Combine(Application.persistentDataPath, NombreArchivo);
    private static string RutaTemporal => Ruta + ".tmp";

    public static bool Existe => File.Exists(Ruta) || File.Exists(RutaTemporal);

    public static void Guardar(DatosGuardado datos)
    {
        if (datos == null) return;

        try
        {
            datos.fechaGuardadoTicks = DateTime.UtcNow.Ticks;
            string json = JsonUtility.ToJson(datos, true);

            File.WriteAllText(RutaTemporal, json);
            if (File.Exists(Ruta)) File.Delete(Ruta);
            File.Move(RutaTemporal, Ruta);
        }
        catch (Exception e)
        {
            Debug.LogWarning("No se pudo guardar la partida: " + e.Message);
        }
    }

    /// <summary>Devuelve la partida guardada, o null si no hay (o está dañada).</summary>
    public static DatosGuardado Cargar()
    {
        try
        {
            // Si quedó solo el temporal (cierre justo durante el reemplazo), se usa ese
            string ruta = File.Exists(Ruta) ? Ruta : (File.Exists(RutaTemporal) ? RutaTemporal : null);
            if (ruta == null) return null;

            string json = File.ReadAllText(ruta);
            if (string.IsNullOrWhiteSpace(json)) return null;

            return JsonUtility.FromJson<DatosGuardado>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("No se pudo cargar la partida: " + e.Message);
            return null;
        }
    }

    public static void Borrar()
    {
        try
        {
            if (File.Exists(Ruta)) File.Delete(Ruta);
            if (File.Exists(RutaTemporal)) File.Delete(RutaTemporal);
        }
        catch (Exception e)
        {
            Debug.LogWarning("No se pudo borrar la partida: " + e.Message);
        }
    }
}
