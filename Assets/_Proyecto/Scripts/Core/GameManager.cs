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
    private const float IntervaloAutoguardado = 5f;
    private const double MaxSegundosOffline = 8 * 3600;   // 8 horas
    private const double MinSegundosParaAviso = 10;       // ausencias cortas no muestran cartel
    public const double MinimoParaBolsa = 1e6;            // facturación de la partida para salir a la bolsa
    private const double DivisorAcciones = 100000;

    public static GameManager Instance { get; private set; }

    [Header("Datos (en orden: Empleado → Vicepresidente)")]
    [SerializeField] private PuestoData[] puestos = new PuestoData[0];

    [Header("Departamentos (RRHH, Marketing, Finanzas)")]
    [SerializeField] private DepartamentoData[] departamentos = new DepartamentoData[0];

    [Header("Etapas de la empresa")]
    [SerializeField] private EtapasConfig configEtapas;

    [Header("Guardado")]
    [Tooltip("Destildalo para probar siempre desde cero en el editor.")]
    [SerializeField] private bool cargarAlIniciar = true;

    public EstadoJuego Estado { get; private set; } = new EstadoJuego();
    public IReadOnlyList<PuestoData> Puestos => puestos;
    public IReadOnlyList<DepartamentoData> Departamentos => departamentos;

    /// <summary>Se dispara cuando cambia cualquier dato (plata, cantidades, niveles...).</summary>
    public event Action OnEstadoCambio;

    /// <summary>Se dispara en cada toque de "Facturar", con lo que se ganó.</summary>
    public event Action<double> OnFacturo;

    /// <summary>Se dispara al contratar: qué puesto y cuántas personas.</summary>
    public event Action<PuestoData, int> OnContrato;

    /// <summary>Se dispara al subir de nivel un departamento.</summary>
    public event Action<DepartamentoData> OnDepartamentoMejorado;

    /// <summary>Se dispara al pasar a una etapa nueva (índice de la etapa).</summary>
    public event Action<int> OnCambioEtapa;

    /// <summary>Índice de la etapa actual (0 = Freelancer en un garaje).</summary>
    public int IndiceEtapa { get; private set; }

    /// <summary>Se dispara al salir a la bolsa, con las acciones ganadas.</summary>
    public event Action<int> OnSalidaABolsa;

    /// <summary>Se dispara al borrar la partida.</summary>
    public event Action OnPartidaBorrada;

    /// <summary>Ganancia offline al volver (monto, segundos ausente).</summary>
    public event Action<double, double> OnGananciaOffline;

    // Aviso offline que quedó pendiente porque la UI todavía no existía (carga en Awake)
    public bool HayAvisoOfflinePendiente { get; private set; }
    public double OfflinePendienteMonto { get; private set; }
    public double OfflinePendienteSegundos { get; private set; }

    private float proximoGuardado;
    private DateTime? momentoPausa;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (puestos == null) puestos = new PuestoData[0];
        if (departamentos == null) departamentos = new DepartamentoData[0];
        AjustarEstado();
        if (cargarAlIniciar) Cargar();
        ActualizarEtapa(false);
        proximoGuardado = Time.unscaledTime + IntervaloAutoguardado;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Producción automática: cada frame suma lo que corresponde al tiempo transcurrido
        double porSegundo = IngresoPorSegundo;
        if (porSegundo > 0)
        {
            SumarPlata(porSegundo * Time.deltaTime);
        }

        // Guardado automático
        if (Time.unscaledTime >= proximoGuardado)
        {
            proximoGuardado = Time.unscaledTime + IntervaloAutoguardado;
            Guardar();
        }
    }

    // Al minimizar la app (Android) se guarda; al volver se suma lo producido mientras tanto
    private void OnApplicationPause(bool pausada)
    {
        if (Instance != this) return;

        if (pausada)
        {
            Guardar();
            momentoPausa = DateTime.UtcNow;
        }
        else if (momentoPausa.HasValue)
        {
            double segundos = (DateTime.UtcNow - momentoPausa.Value).TotalSeconds;
            momentoPausa = null;
            AplicarGananciaOffline(segundos);
        }
    }

    private void OnApplicationQuit()
    {
        if (Instance == this) Guardar();
    }

    // ------------------------------------------------------------------
    // Guardado y carga
    // ------------------------------------------------------------------

    public void Guardar()
    {
        SistemaGuardado.Guardar(CrearDatosGuardado());
    }

    private void Cargar()
    {
        DatosGuardado datos = SistemaGuardado.Cargar();
        if (datos == null) return;

        AplicarDatosGuardado(datos);

        DateTime fechaGuardado = new DateTime(datos.fechaGuardadoTicks, DateTimeKind.Utc);
        double segundosAusente = (DateTime.UtcNow - fechaGuardado).TotalSeconds;
        AplicarGananciaOffline(segundosAusente);
    }

    private DatosGuardado CrearDatosGuardado()
    {
        DatosGuardado datos = new DatosGuardado
        {
            plata = Estado.plata,
            facturadoPartida = Estado.facturadoPartida,
            facturadoHistorico = Estado.facturadoHistorico,
            clicsTotales = Estado.clicsTotales,
            acciones = Estado.acciones
        };

        for (int i = 0; i < puestos.Length; i++)
        {
            if (puestos[i] == null) continue;
            datos.puestos.Add(new RegistroId { id = puestos[i].id, valor = Estado.cantidadesPuestos[i] });
        }

        for (int i = 0; i < departamentos.Length; i++)
        {
            if (departamentos[i] == null) continue;
            datos.departamentos.Add(new RegistroId { id = departamentos[i].id, valor = Estado.nivelesDepartamentos[i] });
        }

        return datos;
    }

    private void AplicarDatosGuardado(DatosGuardado datos)
    {
        Estado = new EstadoJuego
        {
            plata = Math.Max(0, datos.plata),
            facturadoPartida = Math.Max(0, datos.facturadoPartida),
            facturadoHistorico = Math.Max(0, datos.facturadoHistorico),
            clicsTotales = Math.Max(0, datos.clicsTotales),
            acciones = Math.Max(0, datos.acciones)
        };
        AjustarEstado();

        if (datos.puestos != null)
        {
            foreach (RegistroId r in datos.puestos)
            {
                int i = Array.FindIndex(puestos, p => p != null && p.id == r.id);
                if (i >= 0) Estado.cantidadesPuestos[i] = Math.Max(0, r.valor);
            }
        }

        if (datos.departamentos != null)
        {
            foreach (RegistroId r in datos.departamentos)
            {
                int i = Array.FindIndex(departamentos, d => d != null && d.id == r.id);
                if (i < 0) continue;
                int nivel = Math.Max(0, r.valor);
                if (departamentos[i].nivelMaximo > 0) nivel = Math.Min(nivel, departamentos[i].nivelMaximo);
                Estado.nivelesDepartamentos[i] = nivel;
            }
        }
    }

    /// <summary>Suma ingresoPorSegundo × segundos ausente (máximo 8 h) y avisa a la UI.</summary>
    private void AplicarGananciaOffline(double segundos)
    {
        if (double.IsNaN(segundos) || segundos < 1) return; // reloj atrasado o ausencia nula

        segundos = Math.Min(segundos, MaxSegundosOffline);
        double ganancia = IngresoPorSegundo * segundos;
        if (ganancia <= 0) return;

        SumarPlata(ganancia);

        if (segundos < MinSegundosParaAviso) return;

        OfflinePendienteMonto = ganancia;
        OfflinePendienteSegundos = segundos;
        HayAvisoOfflinePendiente = true;
        OnGananciaOffline?.Invoke(ganancia, segundos);
    }

    // ------------------------------------------------------------------
    // Salir a la bolsa (prestigio) y borrar partida
    // ------------------------------------------------------------------

    public bool PuedeSalirABolsa => Estado.facturadoPartida >= MinimoParaBolsa;

    /// <summary>Acciones que ganás = floor(sqrt(facturadoEnLaPartida / 100000)).</summary>
    public int AccionesAGanar => (int)Math.Floor(Math.Sqrt(Estado.facturadoPartida / DivisorAcciones));

    /// <summary>
    /// Reinicia plata, puestos y departamentos.
    /// Conserva acciones (sumando las nuevas), facturación histórica y clics totales.
    /// </summary>
    public bool SalirABolsa()
    {
        if (!PuedeSalirABolsa) return false;

        int ganadas = AccionesAGanar;

        EstadoJuego anterior = Estado;
        Estado = new EstadoJuego
        {
            acciones = anterior.acciones + ganadas,
            facturadoHistorico = anterior.facturadoHistorico,
            clicsTotales = anterior.clicsTotales
        };
        AjustarEstado();
        ActualizarEtapa(false);
        Guardar();

        OnSalidaABolsa?.Invoke(ganadas);
        AvisarCambio();
        return true;
    }

    /// <summary>Borra todo (incluidas acciones e historial) y el archivo de guardado.</summary>
    public void BorrarPartida()
    {
        SistemaGuardado.Borrar();

        Estado = new EstadoJuego();
        AjustarEstado();
        ActualizarEtapa(false);
        HayAvisoOfflinePendiente = false;
        proximoGuardado = Time.unscaledTime + IntervaloAutoguardado;

        OnPartidaBorrada?.Invoke();
        AvisarCambio();
    }

    /// <summary>La UI lo llama al cerrar el cartel de ganancias offline.</summary>
    public void MarcarAvisoOfflineVisto()
    {
        HayAvisoOfflinePendiente = false;
    }

    /// <summary>Se asegura de que el estado tenga un lugar por cada puesto y cada departamento.</summary>
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

        if (Estado.nivelesDepartamentos == null)
        {
            Estado.nivelesDepartamentos = new int[departamentos.Length];
        }
        else if (Estado.nivelesDepartamentos.Length != departamentos.Length)
        {
            Array.Resize(ref Estado.nivelesDepartamentos, departamentos.Length);
        }
    }

    // ------------------------------------------------------------------
    // Multiplicadores y fórmulas
    // ------------------------------------------------------------------

    /// <summary>multAcciones = 1 + 0.1 × acciones</summary>
    public double MultAcciones => 1.0 + 0.1 * Estado.acciones;

    /// <summary>
    /// multProducción = (1 + 0.25×RRHH) × (1 + 0.1×Marketing) × multAcciones
    /// (cada departamento aporta su bonusProduccionPorNivel).
    /// </summary>
    public double MultProduccion
    {
        get
        {
            double mult = MultAcciones;
            for (int i = 0; i < departamentos.Length; i++)
            {
                DepartamentoData d = departamentos[i];
                if (d == null) continue;
                mult *= 1.0 + d.bonusProduccionPorNivel * Estado.nivelesDepartamentos[i];
            }
            return mult;
        }
    }

    /// <summary>Descuento sobre el costo de contratar: 0.95^nivelFinanzas.</summary>
    public double MultFinanzas
    {
        get
        {
            double mult = 1.0;
            for (int i = 0; i < departamentos.Length; i++)
            {
                DepartamentoData d = departamentos[i];
                if (d == null || d.descuentoContratacionPorNivel <= 0) continue;
                mult *= Math.Pow(d.descuentoContratacionPorNivel, Estado.nivelesDepartamentos[i]);
            }
            return mult;
        }
    }

    /// <summary>
    /// Valor por clic = (1 + nivelMarketing) × multAcciones + ingresoPorSegundo × 0.01 × nivelMarketing
    /// </summary>
    public double ValorPorClic
    {
        get
        {
            double clicBase = 1.0;
            double porcentajeIngreso = 0.0;
            for (int i = 0; i < departamentos.Length; i++)
            {
                DepartamentoData d = departamentos[i];
                if (d == null) continue;
                int nivel = Estado.nivelesDepartamentos[i];
                clicBase += d.bonusClicPorNivel * nivel;
                porcentajeIngreso += d.porcentajeIngresoAlClicPorNivel * nivel;
            }
            return clicBase * MultAcciones + IngresoPorSegundo * porcentajeIngreso;
        }
    }

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
    // Departamentos: consultas
    // ------------------------------------------------------------------

    public int IndiceDepartamento(DepartamentoData d) => Array.IndexOf(departamentos, d);

    public int Nivel(DepartamentoData d)
    {
        int i = IndiceDepartamento(d);
        return i < 0 ? 0 : Estado.nivelesDepartamentos[i];
    }

    public bool EstaAlMaximo(DepartamentoData d)
    {
        return d != null && d.nivelMaximo > 0 && Nivel(d) >= d.nivelMaximo;
    }

    /// <summary>Costo del próximo nivel: costoBase × multiplicador^nivel.</summary>
    public double CostoDepartamento(DepartamentoData d)
    {
        if (d == null) return 0;
        return d.costoBase * Math.Pow(d.multiplicadorCosto, Nivel(d));
    }

    // ------------------------------------------------------------------
    // Etapas
    // ------------------------------------------------------------------

    public int CantidadEtapas => configEtapas != null ? configEtapas.etapas.Count : 0;

    /// <summary>Etapa por índice, o null si no existe.</summary>
    public EtapasConfig.Etapa Etapa(int indice)
    {
        if (indice < 0 || indice >= CantidadEtapas) return null;
        return configEtapas.etapas[indice];
    }

    public EtapasConfig.Etapa EtapaActual => Etapa(IndiceEtapa);

    /// <summary>La próxima etapa, o null si ya estás en la última.</summary>
    public EtapasConfig.Etapa EtapaSiguiente => Etapa(IndiceEtapa + 1);

    /// <summary>Progreso de 0 a 1 entre la etapa actual y la siguiente (1 en la última).</summary>
    public double ProgresoEtapa
    {
        get
        {
            EtapasConfig.Etapa actual = EtapaActual;
            EtapasConfig.Etapa siguiente = EtapaSiguiente;
            if (actual == null || siguiente == null) return 1.0;

            double tramo = siguiente.minimoFacturado - actual.minimoFacturado;
            if (tramo <= 0) return 1.0;

            double progreso = (Estado.facturadoPartida - actual.minimoFacturado) / tramo;
            return Math.Max(0.0, Math.Min(1.0, progreso));
        }
    }

    private int CalcularIndiceEtapa()
    {
        int indice = 0;
        for (int i = 0; i < CantidadEtapas; i++)
        {
            if (Estado.facturadoPartida >= configEtapas.etapas[i].minimoFacturado) indice = i;
        }
        return indice;
    }

    private void ActualizarEtapa(bool avisar)
    {
        int nuevo = CalcularIndiceEtapa();
        if (nuevo == IndiceEtapa) return;

        bool subio = nuevo > IndiceEtapa;
        IndiceEtapa = nuevo;
        if (avisar && subio) OnCambioEtapa?.Invoke(nuevo);
    }

    // ------------------------------------------------------------------
    // Acciones del jugador
    // ------------------------------------------------------------------

    /// <summary>Sube un nivel el departamento. Devuelve false si está al máximo o no alcanza la plata.</summary>
    public bool MejorarDepartamento(DepartamentoData d)
    {
        int i = IndiceDepartamento(d);
        if (i < 0 || EstaAlMaximo(d)) return false;

        double costo = CostoDepartamento(d);
        if (costo > Estado.plata) return false;

        Estado.plata -= costo;
        Estado.nivelesDepartamentos[i]++;

        OnDepartamentoMejorado?.Invoke(d);
        AvisarCambio();
        return true;
    }

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
        ActualizarEtapa(true);
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

    [ContextMenu("Prueba: simular 1 hora offline")]
    private void PruebaOffline() => AplicarGananciaOffline(3600);

    [ContextMenu("Prueba: guardar ahora")]
    private void PruebaGuardar()
    {
        Guardar();
        Debug.Log("Partida guardada en " + SistemaGuardado.Ruta);
    }

    [ContextMenu("Prueba: reiniciar partida")]
    private void PruebaReiniciar()
    {
        Estado = new EstadoJuego();
        AjustarEstado();
        ActualizarEtapa(false);
        AvisarCambio();
    }
}
