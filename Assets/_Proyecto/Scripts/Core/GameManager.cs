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
    private const int MaxCompraPorVez = 100000;
    private const float IntervaloAutoguardado = 5f;
    private const double MaxSegundosOffline = 8 * 3600;   // 8 horas
    private const double MinSegundosParaAviso = 10;       // ausencias cortas no muestran cartel
    public const int MaxLargoNombre = 20;                 // caracteres del nombre de la empresa
    public const double MinimoParaBolsa = 1e6;            // facturación de la partida para salir a la bolsa
    private const double DivisorAcciones = 100000;
    public const int MaxAcciones = 1000000000;            // tope para que el número entero no se desborde

    public static GameManager Instance { get; private set; }

    [Header("Economía")]
    [Tooltip("Cada contratación de un puesto cuesta esto × la anterior.")]
    [SerializeField] private double crecimientoCosto = 1.2;

    [Header("Datos (en orden: Empleado → Vicepresidente)")]
    [SerializeField] private PuestoData[] puestos = new PuestoData[0];

    [Header("Departamentos (RRHH, Marketing, Finanzas)")]
    [SerializeField] private DepartamentoData[] departamentos = new DepartamentoData[0];

    [Header("Etapas de la empresa")]
    [SerializeField] private EtapasConfig configEtapas;

    [Header("Rubros para fundar la empresa")]
    [SerializeField] private RubrosConfig configRubros;

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

    /// <summary>Se dispara al contratar personal de un departamento.</summary>
    public event Action<DepartamentoData, RolDepartamento> OnPersonalDepartamentoContratado;

    // Todos los puestos internos de todos los departamentos, en orden (para el estado y el guardado)
    private struct RolRegistrado
    {
        public DepartamentoData departamento;
        public RolDepartamento rol;
    }
    private readonly List<RolRegistrado> roles = new List<RolRegistrado>();
    private readonly Dictionary<RolDepartamento, int> indiceRoles = new Dictionary<RolDepartamento, int>();

    /// <summary>Se dispara al pasar a una etapa nueva (índice de la etapa).</summary>
    public event Action<int> OnCambioEtapa;

    /// <summary>Índice de la etapa actual (0 = Freelancer en un garaje).</summary>
    public int IndiceEtapa { get; private set; }

    /// <summary>Se dispara al salir a la bolsa, con las acciones ganadas.</summary>
    public event Action<int> OnSalidaABolsa;

    /// <summary>Se dispara cuando el jugador elige nombre y rubro.</summary>
    public event Action OnEmpresaFundada;

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

        // En Android Unity limita a 30 FPS por defecto: 60 se ve mucho más fluido
        Application.targetFrameRate = 60;

        if (puestos == null) puestos = new PuestoData[0];
        if (departamentos == null) departamentos = new DepartamentoData[0];
        RegistrarRoles();
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
            acciones = Estado.acciones,
            nombreEmpresa = Estado.nombreEmpresa,
            rubroId = Estado.rubroId
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

        for (int i = 0; i < roles.Count; i++)
        {
            datos.personalRoles.Add(new RegistroId { id = IdRol(roles[i]), valor = Estado.personalRoles[i] });
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
            acciones = Math.Max(0, Math.Min(MaxAcciones, datos.acciones)),
            nombreEmpresa = datos.nombreEmpresa ?? "",
            rubroId = datos.rubroId ?? ""
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

        if (datos.personalRoles != null && datos.personalRoles.Count > 0)
        {
            foreach (RegistroId r in datos.personalRoles)
            {
                int i = roles.FindIndex(x => IdRol(x) == r.id);
                if (i < 0) continue;
                Estado.personalRoles[i] = Math.Max(0, Math.Min(r.valor, CapacidadRol(roles[i].departamento, roles[i].rol)));
            }
        }
        else if (datos.personalDepartamentos != null)
        {
            // Partida de la versión anterior: ese personal pasa al primer puesto interno del departamento
            foreach (RegistroId r in datos.personalDepartamentos)
            {
                int i = roles.FindIndex(x => x.departamento.id == r.id);
                if (i < 0) continue;
                Estado.personalRoles[i] = Math.Max(0, Math.Min(r.valor, CapacidadRol(roles[i].departamento, roles[i].rol)));
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
    // Rubro y nombre de la empresa
    // ------------------------------------------------------------------

    public IReadOnlyList<RubrosConfig.Rubro> Rubros =>
        configRubros != null ? configRubros.rubros : (IReadOnlyList<RubrosConfig.Rubro>)Array.Empty<RubrosConfig.Rubro>();

    /// <summary>True cuando el jugador ya eligió nombre y rubro.</summary>
    public bool EmpresaFundada => !string.IsNullOrWhiteSpace(Estado.nombreEmpresa);

    public RubrosConfig.Rubro RubroActual
    {
        get
        {
            if (configRubros == null || string.IsNullOrEmpty(Estado.rubroId)) return null;
            return configRubros.rubros.Find(r => r.id == Estado.rubroId);
        }
    }

    /// <summary>Guarda el nombre y el rubro elegidos. Devuelve false si el nombre queda vacío.</summary>
    public bool FundarEmpresa(string nombre, string rubroId)
    {
        string limpio = LimpiarNombre(nombre);
        if (string.IsNullOrEmpty(limpio)) return false;

        Estado.nombreEmpresa = limpio;
        Estado.rubroId = rubroId ?? "";
        Guardar();

        OnEmpresaFundada?.Invoke();
        AvisarCambio();
        return true;
    }

    /// <summary>Saca espacios de más y los signos &lt; &gt; (TextMeshPro los tomaría como etiquetas).</summary>
    public static string LimpiarNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "";

        string limpio = nombre.Replace("<", "").Replace(">", "").Trim();
        while (limpio.Contains("  ")) limpio = limpio.Replace("  ", " ");
        if (limpio.Length > MaxLargoNombre) limpio = limpio.Substring(0, MaxLargoNombre).Trim();
        return limpio;
    }

    // ------------------------------------------------------------------
    // Salir a la bolsa (prestigio) y borrar partida
    // ------------------------------------------------------------------

    public bool PuedeSalirABolsa => Estado.facturadoPartida >= MinimoParaBolsa;

    /// <summary>Acciones que ganás = floor(sqrt(facturadoEnLaPartida / 100000)).</summary>
    public int AccionesAGanar
    {
        get
        {
            // Con facturaciones gigantes la raíz no entra en un int: se topea (antes daba negativo)
            double acciones = Math.Floor(Math.Sqrt(Math.Max(0, Estado.facturadoPartida) / DivisorAcciones));
            return (int)Math.Min(MaxAcciones, acciones);
        }
    }

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
            acciones = (int)Math.Min(MaxAcciones, (long)anterior.acciones + ganadas),
            facturadoHistorico = anterior.facturadoHistorico,
            clicsTotales = anterior.clicsTotales,
            nombreEmpresa = anterior.nombreEmpresa,
            rubroId = anterior.rubroId
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

        if (Estado.personalRoles == null)
        {
            Estado.personalRoles = new int[roles.Count];
        }
        else if (Estado.personalRoles.Length != roles.Count)
        {
            Array.Resize(ref Estado.personalRoles, roles.Count);
        }
    }

    // ------------------------------------------------------------------
    // Multiplicadores y fórmulas
    // ------------------------------------------------------------------

    /// <summary>multAcciones = 1 + 0.1 × acciones</summary>
    public double MultAcciones => 1.0 + 0.1 * Estado.acciones;

    /// <summary>
    /// multProducción = multAcciones × Π (1 + bonusPorNivel × nivel + bonusPorPersona × personal)
    /// Ej: RRHH nivel 2 con 3 reclutadores = 1 + 0.15×2 + 0.02×3 = ×1.36
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
                mult *= 1.0 + d.bonusProduccionPorNivel * Estado.nivelesDepartamentos[i] + SumaRoles(d, r => r.bonusProduccion);
            }
            return mult;
        }
    }

    /// <summary>Descuento sobre el costo de contratar: 0.95^nivelFinanzas × 0.985^analistas.</summary>
    public double MultFinanzas
    {
        get
        {
            double mult = 1.0;
            for (int i = 0; i < departamentos.Length; i++)
            {
                DepartamentoData d = departamentos[i];
                if (d == null) continue;
                if (d.descuentoContratacionPorNivel > 0)
                    mult *= Math.Pow(d.descuentoContratacionPorNivel, Estado.nivelesDepartamentos[i]);

            }
            for (int k = 0; k < roles.Count; k++)
            {
                double descuento = roles[k].rol.descuentoContratacion;
                if (descuento > 0 && descuento < 1) mult *= Math.Pow(descuento, Estado.personalRoles[k]);
            }
            return mult;
        }
    }

    /// <summary>Suma de un efecto × cantidad contratada, para todos los puestos internos de un departamento.</summary>
    private double SumaRoles(DepartamentoData d, Func<RolDepartamento, double> efecto)
    {
        double suma = 0;
        for (int k = 0; k < roles.Count; k++)
        {
            if (roles[k].departamento == d) suma += efecto(roles[k].rol) * Estado.personalRoles[k];
        }
        return suma;
    }

    /// <summary>
    /// Valor por clic = (1 + Σ bonus de clic) × multAcciones + ingresoPorSegundo × Σ % al clic
    /// (Marketing: +1 y +0.4% por nivel; cada community manager: +1 y +0.1%).
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
                clicBase += d.bonusClicPorNivel * nivel + SumaRoles(d, r => r.bonusClic);
                porcentajeIngreso += d.porcentajeIngresoAlClicPorNivel * nivel + SumaRoles(d, r => r.porcentajeIngresoAlClic);
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
            foreach (int c in Estado.personalRoles) total += c;
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
        return p.costoBase * Math.Pow(crecimientoCosto, Cantidad(p)) * MultFinanzas;
    }

    /// <summary>Costo de contratar n juntos (suma geométrica).</summary>
    public double CostoContratar(PuestoData p, int n)
    {
        if (p == null || n <= 0) return 0;

        double primero = CostoUnitario(p);
        if (n == 1) return primero;
        return primero * (Math.Pow(crecimientoCosto, n) - 1) / (crecimientoCosto - 1);
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
        double n = Math.Floor(Math.Log(Estado.plata * (crecimientoCosto - 1) / primero + 1) / Math.Log(crecimientoCosto));
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

    /// <summary>
    /// Nombre de la etapa. La primera usa el texto del rubro (ej: "Horneando en un garaje").
    /// </summary>
    public string NombreEtapa(int indice)
    {
        EtapasConfig.Etapa etapa = Etapa(indice);
        if (etapa == null) return "";

        RubrosConfig.Rubro rubro = RubroActual;
        if (indice == 0 && rubro != null && !string.IsNullOrWhiteSpace(rubro.etapaInicial)) return rubro.etapaInicial;
        return etapa.nombreEtapa;
    }

    /// <summary>
    /// Nombre de la empresa en esa etapa, con el nombre del jugador en lugar de "Garaje"
    /// (o de "{nombre}", si lo usás en el asset de Etapas). Ej: "Mimi S.R.L.".
    /// </summary>
    public string NombreEmpresa(int indice)
    {
        EtapasConfig.Etapa etapa = Etapa(indice);
        if (etapa == null) return "";

        string nombre = EmpresaFundada ? Estado.nombreEmpresa : "Garaje";
        return etapa.nombreEmpresa.Contains("{nombre}")
            ? etapa.nombreEmpresa.Replace("{nombre}", nombre)
            : etapa.nombreEmpresa.Replace("Garaje", nombre);
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

    // ------------------------------------------------------------------
    // Personal de los departamentos
    // ------------------------------------------------------------------

    private void RegistrarRoles()
    {
        roles.Clear();
        indiceRoles.Clear();
        foreach (DepartamentoData d in departamentos)
        {
            if (d == null || d.roles == null) continue;
            foreach (RolDepartamento r in d.roles)
            {
                if (r == null || indiceRoles.ContainsKey(r)) continue;
                indiceRoles[r] = roles.Count;
                roles.Add(new RolRegistrado { departamento = d, rol = r });
            }
        }
    }

    private static string IdRol(RolRegistrado r) => r.departamento.id + "/" + r.rol.id;

    private int IndiceRol(RolDepartamento rol)
    {
        return rol != null && indiceRoles.TryGetValue(rol, out int i) ? i : -1;
    }

    /// <summary>Puestos internos del departamento, en orden.</summary>
    public IReadOnlyList<RolDepartamento> RolesDe(DepartamentoData d)
    {
        return d != null && d.roles != null ? d.roles : (IReadOnlyList<RolDepartamento>)Array.Empty<RolDepartamento>();
    }

    /// <summary>Total de personal del departamento (todos sus puestos internos).</summary>
    public int PersonalDepartamento(DepartamentoData d)
    {
        int total = 0;
        for (int k = 0; k < roles.Count; k++)
        {
            if (roles[k].departamento == d) total += Estado.personalRoles[k];
        }
        return total;
    }

    public int PersonalRol(RolDepartamento rol)
    {
        int i = IndiceRol(rol);
        return i < 0 ? 0 : Estado.personalRoles[i];
    }

    public bool RolDesbloqueado(DepartamentoData d, RolDepartamento rol)
    {
        return d != null && rol != null && Nivel(d) >= rol.nivelRequerido;
    }

    /// <summary>Lugares = lugares por nivel × niveles desde el requerido. Ej: requerido 3, nivel 5 → 3 niveles.</summary>
    public int CapacidadRol(DepartamentoData d, RolDepartamento rol)
    {
        if (!RolDesbloqueado(d, rol)) return 0;
        return rol.lugaresPorNivel * (Nivel(d) - rol.nivelRequerido + 1);
    }

    /// <summary>Costo de la próxima persona: base × crecimiento^personal × multFinanzas.</summary>
    public double CostoRol(RolDepartamento rol)
    {
        if (rol == null) return 0;
        return rol.costoBase * Math.Pow(rol.crecimientoCosto, PersonalRol(rol)) * MultFinanzas;
    }

    /// <summary>Contrata una persona para un puesto interno del departamento.</summary>
    public bool ContratarRol(DepartamentoData d, RolDepartamento rol)
    {
        int i = IndiceRol(rol);
        if (i < 0 || roles[i].departamento != d) return false;
        if (Estado.personalRoles[i] >= CapacidadRol(d, rol)) return false;

        double costo = CostoRol(rol);
        if (costo > Estado.plata) return false;

        Estado.plata -= costo;
        Estado.personalRoles[i]++;

        OnPersonalDepartamentoContratado?.Invoke(d, rol);
        AvisarCambio();
        return true;
    }

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
