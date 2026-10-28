using System;
using System.Collections.Generic;
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

    [Header("Puestos internos del departamento (se desbloquean subiendo de nivel)")]
    public List<RolDepartamento> roles = new List<RolDepartamento>();
}

/// <summary>
/// Un puesto de trabajo dentro de un departamento (ej: Reclutador, Capacitador...).
/// Cada nivel del departamento a partir de "nivelRequerido" abre "lugaresPorNivel" lugares.
/// </summary>
[Serializable]
public class RolDepartamento
{
    [Tooltip("Identificador único para el guardado. No cambiarlo una vez publicado el juego.")]
    public string id = "rol";
    public string nombre = "Especialista";
    public string nombrePlural = "Especialistas";
    [Tooltip("Letra(s) para la insignia. Ej: R")]
    public string inicial = "P";

    [Header("Desbloqueo y lugares")]
    [Tooltip("Nivel del departamento desde el que se puede contratar.")]
    [Min(1)] public int nivelRequerido = 1;
    [Tooltip("Lugares que abre cada nivel del departamento (desde el nivel requerido).")]
    [Min(0)] public int lugaresPorNivel = 1;

    [Header("Costo")]
    public double costoBase = 1000;
    [Tooltip("Cada contratación siguiente cuesta esto × la anterior.")]
    public double crecimientoCosto = 1.4;

    [Header("Efecto de cada persona (dejá en 0 lo que no use)")]
    [Tooltip("Producción de todo el personal: se suma al bonus del departamento. Ej: 0.02 = +2%")]
    public double bonusProduccion = 0;
    [Tooltip("Se suma al valor base del clic. Ej: 1 = +$1")]
    public double bonusClic = 0;
    [Tooltip("Se suma al % de $/s que da cada clic. Ej: 0.001 = +0.1%")]
    public double porcentajeIngresoAlClic = 0;
    [Tooltip("Costo de contratar × esto por persona. 1 = sin efecto. Ej: 0.985 = -1.5%")]
    public double descuentoContratacion = 1;
}
