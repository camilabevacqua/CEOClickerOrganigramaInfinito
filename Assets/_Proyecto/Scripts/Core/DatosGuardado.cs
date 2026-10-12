using System;
using System.Collections.Generic;

/// <summary>
/// Lo que se escribe en el archivo de guardado (JSON).
/// Puestos y departamentos se guardan por id, así se puede reordenar o agregar
/// puestos sin romper partidas viejas.
/// </summary>
[Serializable]
public class DatosGuardado
{
    public int version = 1;

    // Momento del guardado (UTC, en ticks) para calcular las ganancias offline
    public long fechaGuardadoTicks;

    public double plata;
    public double facturadoPartida;
    public double facturadoHistorico;
    public long clicsTotales;
    public int acciones;

    public List<RegistroId> puestos = new List<RegistroId>();
    public List<RegistroId> departamentos = new List<RegistroId>();
}

/// <summary>Par id → valor (cantidad de un puesto o nivel de un departamento).</summary>
[Serializable]
public class RegistroId
{
    public string id;
    public int valor;
}
