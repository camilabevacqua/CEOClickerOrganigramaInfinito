using UnityEngine;

/// <summary>
/// Definición de un departamento (mejora con niveles). Se edita desde el Inspector.
/// Crear nuevo: clic derecho en Project > Create > CEO Clicker > Departamento.
/// Los efectos son genéricos: cada departamento usa los que tenga distintos de 0 (o de 1 en el descuento).
/// </summary>
[CreateAssetMenu(fileName = "Departamento", menuName = "CEO Clicker/Departamento", order = 1)]
public class DepartamentoData : ScriptableObject
{
    [Header("Identidad")]
    [Tooltip("Identificador único para el guardado. No cambiarlo una vez publicado el juego.")]
    public string id = "departamento";
    public string nombre = "Departamento";
    [Tooltip("Sigla corta para la insignia y el organigrama (RRHH, MKT, FIN).")]
    public string sigla = "DEP";
    public Color color = Color.gray;
    [TextArea(2, 3)]
    public string descripcion = "";

    [Header("Costo")]
    [Tooltip("Costo del nivel 1.")]
    public double costoBase = 100;
    [Tooltip("Cada nivel cuesta esto × el anterior.")]
    public double multiplicadorCosto = 2;
    [Tooltip("Nivel máximo. 0 = sin límite.")]
    [Min(0)] public int nivelMaximo = 0;

    [Header("Efectos por nivel (dejá en 0 lo que no use)")]
    [Tooltip("Producción de todo el personal × (1 + esto × nivel). RRHH = 0.25, Marketing = 0.1")]
    public double bonusProduccionPorNivel = 0;

    [Tooltip("Valor base del clic = 1 + esto × nivel. Marketing = 1")]
    public double bonusClicPorNivel = 0;

    [Tooltip("Cada clic suma además ingresoPorSegundo × esto × nivel. Marketing = 0.01")]
    public double porcentajeIngresoAlClicPorNivel = 0;

    [Tooltip("Costo de contratar × esto ^ nivel. 1 = sin efecto. Finanzas = 0.95")]
    public double descuentoContratacionPorNivel = 1;
}
