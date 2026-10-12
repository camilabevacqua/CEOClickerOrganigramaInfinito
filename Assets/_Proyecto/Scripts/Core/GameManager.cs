using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cerebro del juego: tiene el estado y hace TODAS las cuentas.
/// La interfaz nunca cambia el estado directamente: llama a métodos de acá.
/// </summary>
[DefaultExecutionOrder(-100)] // se inicializa antes que los scripts de UI
public class GameManager : MonoBehaviour
{
    private const double CrecimientoCosto = 1.15;
    private const int MaxCompraPorVez = 100000;

    public static GameManager Instance { get; private set; }

    [Header("Datos (en orden: Empleado → Vicepresidente)")]
    [SerializeField] private PuestoData[] puestos = new PuestoData[0];

    public EstadoJuego Estado { get; private set; } = new EstadoJuego();
    public IReadOnlyList<PuestoData> Puestos => puestos;

    /// <summary>Se dispara cuando cambia cualquier dato (plata, cantidades, niveles...).</summary>
    public event Action OnEstadoCambio;

    /// <summary>Se dispara en cada toque de "Facturar", con lo que se ganó.</summary>
    public event Action<double> OnFacturo;

    /// <summary>Se dispara al contratar: qué puesto y cuántas personas.</summary>
    public event Action<PuestoData, int> OnContrato;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (puestos == null) puestos = new PuestoData[0];
        AjustarEstado();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Se asegura de que el estado tenga un lugar por cada puesto.</summary>
    private void AjustarEstado()
    {
        if (Estado.cantidadesPuestos == null)
        {
            Estado.cantidadesPuestos = new int[puestos.Length];
        }
        else if (Estado.cantidadesPuestos.Length != puestos.Length)
        {
            Array.Resize(ref Estado.cantidadesPuestos, puestos.Length);
        }
    }

    // ------------------------------------------------------------------
    // Multiplicadores y fórmulas
    // ------------------------------------------------------------------

    /// <summary>multAcciones = 1 + 0.1 × acciones</summary>
    public double MultAcciones => 1.0 + 0.1 * Estado.acciones;

    /// <summary>
    /// multProducción. Por ahora = multAcciones.
    /// En el Paso 5: (1 + 0.25×RRHH) × (1 + 0.1×Marketing) × multAcciones.
    /// </summary>
    public double MultProduccion => MultAcciones;

    /// <summary>Descuento de Finanzas sobre el costo de contratar. Paso 5: 0.95^nivel.</summary>
    public double MultFinanzas => 1.0;

    /// <summary>
    /// Valor por clic. Por ahora = 1 × multAcciones.
    /// En el Paso 5 se suma Marketing: (1 + nivel) × multAcciones + ingresoPorSegundo × 0.01 × nivel.
    /// </summary>
    public double ValorPorClic => 1.0 * MultAcciones;

    /// <summary>Plata por segundo que genera UNA persona de ese puesto, con multiplicadores.</summary>
    public double ProduccionUnitaria(PuestoData p)
    {
        return p == null ? 0 : p.produccionPorSegundo * MultProduccion;
    }

    /// <summary>ingresoPorSegundo = Σ(cantidad × producción del puesto) × multProducción</summary>
    public double IngresoPorSegundo
    {
        get
        {
            double suma = 0;
            for (int i = 0; i < puestos.Length; i++)
            {
                if (puestos[i] == null) continue;
                suma += Estado.cantidadesPuestos[i] * puestos[i].produccionPorSegundo;
            }
            return suma * MultProduccion;
        }
    }

    // ------------------------------------------------------------------
    // Puestos: consultas
    // ------------------------------------------------------------------

    public int Indice(PuestoData p) => Array.IndexOf(puestos, p);

    public int Cantidad(PuestoData p)
    {
        int i = Indice(p);
        return i < 0 ? 0 : Estado.cantidadesPuestos[i];
    }

    /// <summary>Total de personas contratadas (todos los puestos).</summary>
    public int PersonalTotal
    {
        get
        {
            int total = 0;
            foreach (int c in Estado.cantidadesPuestos) total += c;
            return total;
        }
    }

    /// <summary>Puesto anterior en la lista (el de abajo en la jerarquía). Null para el primero.</summary>
    public PuestoData Anterior(PuestoData p)
    {
        int i = Indice(p);
        return i > 0 ? puestos[i - 1] : null;
    }

    /// <summary>El puesto al que este jefe le abre lugares (ej: Supervisor → Empleado).</summary>
    public PuestoData SubordinadoDe(PuestoData jefe)
    {
        foreach (PuestoData p in puestos)
        {
            if (p != null && p.jefe == jefe) return p;
        }
        return null;
    }

    /// <summary>Capacidad = lugares base + (lugares que abre su jefe × cantidad de jefes).</summary>
    public long Capacidad(PuestoData p)
    {
        if (p == null) return 0;
        if (p.sinLimite) return long.MaxValue;

        long capacidad = p.lugaresBase;
        if (p.jefe != null) capacidad += (long)p.lugaresPorJefe * Cantidad(p.jefe);
        return capacidad;
    }

    public int LugaresLibres(PuestoData p)
    {
        if (p == null) return 0;
        if (p.sinLimite) return int.MaxValue;

        long libres = Capacidad(p) - Cantidad(p);
        return (int)Math.Max(0, Math.Min(int.MaxValue, libres));
    }

    /// <summary>Visible si: es el primero, ya tenés alguno, tenés uno del anterior o facturaste la mitad de su costo.</summary>
    public bool EstaDesbloqueado(PuestoData p)
    {
        int i = Indice(p);
        if (i <= 0) return i == 0;
        if (Estado.cantidadesPuestos[i] > 0) return true;
        if (Estado.cantidadesPuestos[i - 1] > 0) return true;
        return Estado.facturadoPartida >= p.costoBase * 0.5;
    }

    /// <summary>Costo de la próxima contratación: costoBase × 1.15^cantidad × multFinanzas.</summary>
    public double CostoUnitario(PuestoData p)
    {
        return p.costoBase * Math.Pow(CrecimientoCosto, Cantidad(p)) * MultFinanzas;
    }

    /// <summary>Costo de contratar n juntos (suma geométrica).</summary>
    public double CostoContratar(PuestoData p, int n)
    {
        if (p == null || n <= 0) return 0;

        double primero = CostoUnitario(p);
        if (n == 1) return primero;
        return primero * (Math.Pow(CrecimientoCosto, n) - 1) / (CrecimientoCosto - 1);
    }

    /// <summary>Cuántos se pueden contratar con la plata actual y los lugares libres.</summary>
    public int MaxComprable(PuestoData p)
    {
        if (p == null || !EstaDesbloqueado(p)) return 0;

        int libres = LugaresLibres(p);
        if (libres <= 0) return 0;

        double primero = CostoUnitario(p);
        if (Estado.plata < primero) return 0;

        // Despejamos n de: primero × (1.15^n − 1) / 0.15 ≤ plata
        double n = Math.Floor(Math.Log(Estado.plata * (CrecimientoCosto - 1) / primero + 1) / Math.Log(CrecimientoCosto));
        int cantidad = (int)Math.Min(Math.Min(n, libres), MaxCompraPorVez);

        // Ajuste por redondeo
        while (cantidad > 0 && CostoContratar(p, cantidad) > Estado.plata) cantidad--;
        return Math.Max(cantidad, 0);
    }

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

    /// <summary>Contrata n personas del puesto. Devuelve false si no hay lugar o no alcanza la plata.</summary>
    public bool Contratar(PuestoData p, int n)
    {
        int i = Indice(p);
        if (i < 0 || n <= 0) return false;
        if (!EstaDesbloqueado(p)) return false;
        if (n > LugaresLibres(p)) return false;

        double costo = CostoContratar(p, n);
        if (costo > Estado.plata) return false;

        Estado.plata -= costo;
        Estado.cantidadesPuestos[i] += n;

        OnContrato?.Invoke(p, n);
        AvisarCambio();
        return true;
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

    [ContextMenu("Prueba: reiniciar partida")]
    private void PruebaReiniciar()
    {
        Estado = new EstadoJuego();
        AjustarEstado();
        AvisarCambio();
    }
}
