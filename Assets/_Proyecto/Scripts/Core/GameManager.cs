using System;
using UnityEngine;

/// <summary>
/// Cerebro del juego: tiene el estado y hace TODAS las cuentas.
/// La interfaz nunca cambia el estado directamente: llama a métodos de acá.
/// </summary>
[DefaultExecutionOrder(-100)] // se inicializa antes que los scripts de UI
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public EstadoJuego Estado { get; private set; } = new EstadoJuego();

    /// <summary>Se dispara cuando cambia cualquier dato (plata, cantidades, niveles...).</summary>
    public event Action OnEstadoCambio;

    /// <summary>Se dispara en cada toque de "Facturar", con lo que se ganó.</summary>
    public event Action<double> OnFacturo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------
    // Fórmulas
    // ------------------------------------------------------------------

    /// <summary>multAcciones = 1 + 0.1 × acciones</summary>
    public double MultAcciones => 1.0 + 0.1 * Estado.acciones;

    /// <summary>
    /// Valor por clic. Por ahora = 1 × multAcciones.
    /// En el Paso 5 se suma Marketing: (1 + nivel) × multAcciones + ingresoPorSegundo × 0.01 × nivel.
    /// </summary>
    public double ValorPorClic => 1.0 * MultAcciones;

    // ------------------------------------------------------------------
    // Acciones del jugador
    // ------------------------------------------------------------------

    /// <summary>Toque en el botón Facturar. Devuelve lo que se ganó.</summary>
    public double Facturar()
    {
        double ganado = ValorPorClic;
        Estado.clicsTotales++;
        SumarPlata(ganado);
        OnFacturo?.Invoke(ganado);
        return ganado;
    }

    /// <summary>Suma plata ganada (clics, producción, offline). Cuenta como facturación.</summary>
    public void SumarPlata(double monto)
    {
        if (monto <= 0 || double.IsNaN(monto) || double.IsInfinity(monto)) return;

        Estado.plata += monto;
        Estado.facturadoPartida += monto;
        Estado.facturadoHistorico += monto;
        AvisarCambio();
    }

    /// <summary>Intenta gastar plata. Devuelve false si no alcanza.</summary>
    public bool GastarPlata(double monto)
    {
        if (monto < 0 || monto > Estado.plata) return false;

        Estado.plata -= monto;
        AvisarCambio();
        return true;
    }

    public void AvisarCambio()
    {
        OnEstadoCambio?.Invoke();
    }

    // ------------------------------------------------------------------
    // Trucos para probar (clic derecho en el título del componente, en Play)
    // ------------------------------------------------------------------
    [ContextMenu("Prueba: sumar $1.000")]
    private void PruebaMil() => SumarPlata(1000);

    [ContextMenu("Prueba: sumar $1M")]
    private void PruebaMillon() => SumarPlata(1e6);

    [ContextMenu("Prueba: sumar $1 billón (1e12)")]
    private void PruebaBillon() => SumarPlata(1e12);

    [ContextMenu("Prueba: sumar 1e30")]
    private void PruebaEnorme() => SumarPlata(1e30);
}
