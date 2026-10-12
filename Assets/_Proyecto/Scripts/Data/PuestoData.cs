using UnityEngine;

/// <summary>
/// Definición de un puesto (Empleado, Supervisor, ...). Se edita desde el Inspector.
/// Crear nuevo: clic derecho en Project > Create > CEO Clicker > Puesto.
/// </summary>
[CreateAssetMenu(fileName = "Puesto", menuName = "CEO Clicker/Puesto", order = 0)]
public class PuestoData : ScriptableObject
{
    [Header("Identidad")]
    [Tooltip("Identificador único para el guardado. No cambiarlo una vez publicado el juego.")]
    public string id = "puesto";
    public string nombre = "Puesto";
    public string nombrePlural = "Puestos";
    [Tooltip("Letra(s) que aparecen en la insignia y en el organigrama (E, S, G, D, VP).")]
    public string inicial = "P";
    public Color color = Color.gray;
    [TextArea(2, 3)]
    public string descripcion = "";

    [Header("Economía")]
    [Tooltip("Costo de la primera contratación. Cada una siguiente cuesta ×1.15.")]
    public double costoBase = 10;
    [Tooltip("Plata por segundo que genera CADA persona en este puesto (antes de multiplicadores).")]
    public double produccionPorSegundo = 1;

    [Header("Lugares")]
    [Tooltip("Si está tildado, no hay límite de contrataciones.")]
    public bool sinLimite = false;
    [Tooltip("Lugares que existen sin ningún jefe (reportan directo al CEO).")]
    [Min(0)] public int lugaresBase = 1;
    [Tooltip("Puesto jefe: cada persona en ese puesto abre lugares para este.")]
    public PuestoData jefe;
    [Tooltip("Cuántos lugares de ESTE puesto abre cada jefe contratado.")]
    [Min(0)] public int lugaresPorJefe = 0;
}
