using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Organigrama en árbol, con cuadraditos de tamaño fijo y deslizable:
///
///                                [ CEO ]                         ← arriba de todo, solo
///        ┌─────────────────┬────────┴───────┬──────────────┐
///  [ Operaciones ]      [ RRHH ]         [ MKT ]        [ FIN ]  ← áreas: todas dependen del CEO
///         │                │
///    VP VP VP VP           ├─ R R R +3                           ← personal del área, un renglón por trabajo
///      │                   └─ Ca Ca
///    D   D
///    │
///   ...  (G, S, E con líneas en codo al jefe más cercano)
///
/// Si no entra en el panel, se desliza arriba/abajo y a los costados.
/// Zoom: pellizcando con dos dedos, con la ruedita del mouse o con los botones + y -.
/// Solo lee del GameManager. Se reconstruye cuando cambian cantidades o niveles.
/// Animaciones: "pop" al contratar o mejorar, latido del CEO al facturar y signos "$" que suben.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIOrganigrama : MonoBehaviour
{
    [Header("Aspecto")]
    [SerializeField] private Sprite spriteNodo;
    [SerializeField] private Color colorCEO = new Color32(0xF2, 0x8D, 0xB8, 0xFF);
    [SerializeField] private Color colorTextoNodo = Color.white;
    [SerializeField] private Color colorLinea = new Color32(0x9C, 0x7A, 0x93, 0x99);
    [SerializeField] private Color colorMas = new Color32(0xDC, 0xCF, 0xD8, 0xFF);
    [SerializeField] private Color colorAyuda = new Color32(0x9C, 0x7A, 0x93, 0xFF);
    [Tooltip("Color de la caja Operaciones (la cadena de puestos).")]
    [SerializeField] private Color colorOperaciones = new Color32(0x9B, 0x7F, 0xD4, 0xFF);
    [SerializeField] private float grosorLinea = 3f;
    [SerializeField] private float margen = 30f;

    [Header("Medidas (píxeles de la referencia 1080×1920)")]
    [SerializeField] private float anchoCEO = 260f;
    [SerializeField] private float altoCEO = 96f;
    [SerializeField] private float anchoArea = 230f;
    [SerializeField] private float altoArea = 84f;
    [SerializeField] private float anchoNodo = 74f;
    [SerializeField] private float altoNodo = 62f;
    [SerializeField] private float separacionNodos = 14f;
    [SerializeField] private float anchoNodoArea = 62f;
    [SerializeField] private float altoNodoArea = 54f;
    [SerializeField] private float separacionNodosArea = 10f;
    [SerializeField] private float separacionColumnas = 60f;
    [SerializeField] private float separacionNiveles = 64f;
    [Tooltip("Máximo de cuadraditos por fila en Operaciones (después, \"+N\").")]
    [SerializeField] private int maxPorFila = 10;
    [Tooltip("Máximo de cuadraditos por renglón en un departamento (después, \"+N\").")]
    [SerializeField] private int maxPorFilaArea = 5;

    [Header("Zoom")]
    [SerializeField] private float zoomMinimo = 0.4f;
    [SerializeField] private float zoomMaximo = 2.5f;
    [Tooltip("Cuánto acerca o aleja cada toque de los botones + / - (o cada paso de la ruedita).")]
    [SerializeField] private float pasoZoom = 1.25f;

    [Header("Animaciones")]
    [SerializeField] private Color colorSigno = new Color32(0xD6, 0x5A, 0x92, 0xFF);
    [Tooltip("Máximo de signos \"$\" por segundo.")]
    [SerializeField] private float maxSignosPorSegundo = 5f;
    [SerializeField] private float duracionPop = 0.35f;
    [SerializeField] private float duracionLatido = 0.18f;
    [SerializeField] private float duracionSigno = 1.1f;

    private enum TipoAnimacion { Pop, Latido }

    private struct Animacion
    {
        public RectTransform rt;
        public TipoAnimacion tipo;
        public float tiempo;
        public float duracion;
    }

    private struct Signo
    {
        public TextMeshProUGUI texto;
        public Vector2 inicio;
        public float tiempo;
    }

    // Nodo reutilizable (rectángulo de color + texto)
    private class Nodo
    {
        public RectTransform rt;
        public Image imagen;
        public TextMeshProUGUI texto;
    }

    private RectTransform rt;
    private ScrollRect scroll;
    private RectTransform contenido;
    private RectTransform capaLineas;
    private RectTransform capaNodos;
    private RectTransform capaParticulas;
    private TextMeshProUGUI textoAyuda;
    private bool primeraVez = true;

    // Zoom
    private float zoom = 1f;
    private bool pellizcando;
    private float distanciaInicial;
    private float zoomInicial;

    private readonly List<Nodo> poolNodos = new List<Nodo>();
    private readonly List<Image> poolLineas = new List<Image>();
    private int nodosUsados;
    private int lineasUsadas;

    private readonly List<PuestoData> filasPuestos = new List<PuestoData>();
    private readonly List<DepartamentoData> departamentos = new List<DepartamentoData>();
    private readonly List<float> anchosColumnas = new List<float>();
    private List<float> xsArriba = new List<float>();
    private List<float> xsFila = new List<float>();

    private readonly Dictionary<PuestoData, List<RectTransform>> nodosPorPuesto = new Dictionary<PuestoData, List<RectTransform>>();
    private readonly Dictionary<DepartamentoData, RectTransform> nodosPorDepartamento = new Dictionary<DepartamentoData, RectTransform>();
    private readonly Dictionary<RolDepartamento, List<RectTransform>> nodosPorRol = new Dictionary<RolDepartamento, List<RectTransform>>();
    private static readonly List<RectTransform> ListaVacia = new List<RectTransform>();

    // Nodos de los que salen los signos "$" (los puestos que producen)
    private readonly List<RectTransform> nodosPersonal = new List<RectTransform>();

    private readonly List<Animacion> animaciones = new List<Animacion>();
    private readonly List<Signo> signos = new List<Signo>();
    private readonly List<TextMeshProUGUI> poolSignos = new List<TextMeshProUGUI>();
    private float acumuladorSignos;

    private readonly List<PuestoData> popsPuestos = new List<PuestoData>();
    private readonly List<DepartamentoData> popsDepartamentos = new List<DepartamentoData>();
    private readonly List<RolDepartamento> popsRoles = new List<RolDepartamento>();
    private bool suscripto;

    private bool sucio = true;
    private long ultimaFirma = long.MinValue;
    private Vector2 ultimoTamanio;

    /// <summary>Nodo del CEO (para animaciones).</summary>
    public RectTransform NodoCEO { get; private set; }

    /// <summary>Nodos visibles de un puesto (para animaciones).</summary>
    public IReadOnlyList<RectTransform> NodosDe(PuestoData puesto)
    {
        return puesto != null && nodosPorPuesto.TryGetValue(puesto, out List<RectTransform> lista) ? lista : ListaVacia;
    }

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        PrepararDeslizamiento();

        capaLineas = CrearCapa("Lineas");
        capaNodos = CrearCapa("Nodos");
        capaParticulas = CrearCapa("Particulas");

        textoAyuda = CrearTexto("Ayuda", capaNodos);
        textoAyuda.text = "Contratá personal para armar tu organigrama";
        textoAyuda.color = colorAyuda;
        textoAyuda.fontStyle = FontStyles.Normal;
        textoAyuda.fontSizeMin = 18f;
        textoAyuda.fontSizeMax = 30f;
    }

    /// <summary>
    /// El panel es la "ventana" y adentro va un contenido del tamaño del organigrama completo,
    /// que se desliza con el dedo.
    /// </summary>
    private void PrepararDeslizamiento()
    {
        Image fondo = GetComponent<Image>();
        if (fondo != null) fondo.raycastTarget = true; // para recibir el arrastre
        if (GetComponent<RectMask2D>() == null) gameObject.AddComponent<RectMask2D>();

        GameObject go = new GameObject("Contenido", typeof(RectTransform));
        go.layer = gameObject.layer;
        contenido = go.GetComponent<RectTransform>();
        contenido.SetParent(transform, false);
        contenido.anchorMin = contenido.anchorMax = new Vector2(0f, 1f);
        contenido.pivot = new Vector2(0f, 1f);
        contenido.anchoredPosition = Vector2.zero;

        scroll = GetComponent<ScrollRect>();
        if (scroll == null) scroll = gameObject.AddComponent<ScrollRect>();
        scroll.content = contenido;
        scroll.viewport = rt;
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.inertia = true;
        scroll.scrollSensitivity = 0f; // la ruedita del mouse se usa para el zoom

        CrearBotonesZoom();
    }

    // ------------------------------------------------------------------
    // Zoom
    // ------------------------------------------------------------------

    /// <summary>Botones + y - en la esquina de arriba a la derecha del panel (no se deslizan).</summary>
    private void CrearBotonesZoom()
    {
        CrearBotonZoom("BotonAcercar", "+", new Vector2(-24f, -24f), () => CambiarZoom(zoom * pasoZoom, CentroPanel()));
        CrearBotonZoom("BotonAlejar", "-", new Vector2(-24f, -108f), () => CambiarZoom(zoom / pasoZoom, CentroPanel()));
    }

    private void CrearBotonZoom(string nombre, string simbolo, Vector2 posicion, UnityEngine.Events.UnityAction accion)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = gameObject.layer;
        RectTransform r = go.GetComponent<RectTransform>();
        r.SetParent(transform, false);
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(1f, 1f);
        r.sizeDelta = new Vector2(72f, 72f);
        r.anchoredPosition = posicion;

        Image imagen = go.AddComponent<Image>();
        imagen.sprite = spriteNodo;
        imagen.type = spriteNodo != null ? Image.Type.Sliced : Image.Type.Simple;
        imagen.pixelsPerUnitMultiplier = 0.7f;
        Color fondo = colorOperaciones;
        fondo.a = 0.9f;
        imagen.color = fondo;

        Button boton = go.AddComponent<Button>();
        boton.targetGraphic = imagen;
        boton.onClick.AddListener(accion);

        TextMeshProUGUI texto = CrearTexto("Texto", r);
        RectTransform rTexto = texto.rectTransform;
        rTexto.anchorMin = Vector2.zero;
        rTexto.anchorMax = Vector2.one;
        rTexto.offsetMin = Vector2.zero;
        rTexto.offsetMax = Vector2.zero;
        texto.enableAutoSizing = false;
        texto.fontSize = 48f;
        texto.color = colorTextoNodo;
        texto.text = simbolo;
    }

    private Vector2 CentroPanel()
    {
        return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
    }

    /// <summary>Cambia el zoom dejando quieto el punto de la pantalla que se está mirando (dedos o mouse).</summary>
    private void CambiarZoom(float nuevoZoom, Vector2 puntoPantalla)
    {
        nuevoZoom = Mathf.Clamp(nuevoZoom, zoomMinimo, zoomMaximo);
        if (Mathf.Approximately(nuevoZoom, zoom)) return;

        // Punto del organigrama que está debajo del dedo / mouse, antes de cambiar el zoom
        RectTransformUtility.ScreenPointToLocalPointInRectangle(contenido, puntoPantalla, null, out Vector2 puntoLocal);

        zoom = nuevoZoom;
        contenido.localScale = new Vector3(zoom, zoom, 1f);

        // Después del zoom, se corre el contenido para que ese punto siga debajo del dedo
        Vector2 nuevaPantalla = RectTransformUtility.WorldToScreenPoint(null, contenido.TransformPoint(puntoLocal));
        float escalaPanel = rt.lossyScale.x > 0f ? rt.lossyScale.x : 1f;
        contenido.anchoredPosition += (puntoPantalla - nuevaPantalla) / escalaPanel;
        scroll.StopMovement();

        // Al terminar de hacer zoom se recalcula el tamaño del área deslizable
        if (!pellizcando) sucio = true;
    }

    /// <summary>Pellizco con dos dedos (celular) y ruedita del mouse (editor/PC).</summary>
    private void ActualizarZoomConGestos()
    {
        // --- Dos dedos ---
        Touchscreen pantalla = Touchscreen.current;
        int dedos = 0;
        Vector2 dedo1 = Vector2.zero, dedo2 = Vector2.zero;
        if (pantalla != null)
        {
            foreach (var toque in pantalla.touches)
            {
                if (!toque.press.isPressed) continue;
                if (dedos == 0) dedo1 = toque.position.ReadValue();
                else if (dedos == 1) dedo2 = toque.position.ReadValue();
                dedos++;
            }
        }

        if (dedos >= 2)
        {
            Vector2 medio = (dedo1 + dedo2) * 0.5f;
            float distancia = Vector2.Distance(dedo1, dedo2);

            if (!pellizcando)
            {
                // Solo empieza si el pellizco es sobre el organigrama
                if (!RectTransformUtility.RectangleContainsScreenPoint(rt, medio, null) || distancia < 1f) return;
                pellizcando = true;
                distanciaInicial = distancia;
                zoomInicial = zoom;
                scroll.enabled = false; // mientras se pellizca, no se desliza
            }

            CambiarZoom(zoomInicial * distancia / distanciaInicial, medio);
            return;
        }

        if (pellizcando)
        {
            pellizcando = false;
            scroll.enabled = true;
            scroll.StopMovement();
            sucio = true;
        }

        // --- Ruedita del mouse ---
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        float ruedita = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(ruedita) < 0.01f) return;

        Vector2 posicion = mouse.position.ReadValue();
        if (!RectTransformUtility.RectangleContainsScreenPoint(rt, posicion, null)) return;

        CambiarZoom(ruedita > 0f ? zoom * pasoZoom : zoom / pasoZoom, posicion);
    }

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        gm.OnContrato += AlContratar;
        gm.OnDepartamentoMejorado += AlMejorar;
        gm.OnPersonalDepartamentoContratado += AlContratarPersonalDepartamento;
        gm.OnFacturo += AlFacturar;
        suscripto = true;
    }

    private void OnDestroy()
    {
        if (!suscripto || GameManager.Instance == null) return;

        GameManager.Instance.OnContrato -= AlContratar;
        GameManager.Instance.OnDepartamentoMejorado -= AlMejorar;
        GameManager.Instance.OnPersonalDepartamentoContratado -= AlContratarPersonalDepartamento;
        GameManager.Instance.OnFacturo -= AlFacturar;
    }

    // El "pop" se aplica después de reconstruir (en LateUpdate), cuando los nodos nuevos ya existen
    private void AlContratar(PuestoData puesto, int cantidad)
    {
        if (!popsPuestos.Contains(puesto)) popsPuestos.Add(puesto);
    }

    private void AlMejorar(DepartamentoData departamento)
    {
        if (!popsDepartamentos.Contains(departamento)) popsDepartamentos.Add(departamento);
    }

    private void AlContratarPersonalDepartamento(DepartamentoData departamento, RolDepartamento rol)
    {
        if (!popsRoles.Contains(rol)) popsRoles.Add(rol);
    }

    private void AlFacturar(double valor)
    {
        if (NodoCEO != null) Animar(NodoCEO, TipoAnimacion.Latido, duracionLatido);
    }

    private void OnRectTransformDimensionsChange()
    {
        sucio = true;
    }

    private void LateUpdate()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        long firma = Firma(gm);
        Vector2 tamanio = rt.rect.size;

        if (sucio || firma != ultimaFirma || tamanio != ultimoTamanio)
        {
            sucio = false;
            ultimaFirma = firma;
            ultimoTamanio = tamanio;
            Reconstruir(gm);
        }

        AplicarPopsPendientes();
    }

    private void AplicarPopsPendientes()
    {
        foreach (PuestoData puesto in popsPuestos)
        {
            foreach (RectTransform nodo in NodosDe(puesto)) Animar(nodo, TipoAnimacion.Pop, duracionPop);
        }
        popsPuestos.Clear();

        foreach (DepartamentoData departamento in popsDepartamentos)
        {
            if (nodosPorDepartamento.TryGetValue(departamento, out RectTransform caja))
            {
                Animar(caja, TipoAnimacion.Pop, duracionPop);
            }
        }
        popsDepartamentos.Clear();

        foreach (RolDepartamento rol in popsRoles)
        {
            if (rol != null && nodosPorRol.TryGetValue(rol, out List<RectTransform> nodos))
            {
                foreach (RectTransform nodo in nodos) Animar(nodo, TipoAnimacion.Pop, duracionPop);
            }
        }
        popsRoles.Clear();
    }

    // ------------------------------------------------------------------
    // Animaciones (sin librerías: se calculan a mano cada frame)
    // ------------------------------------------------------------------
    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        ActualizarZoomConGestos();
        ActualizarAnimaciones(dt);
        ActualizarSignos(dt);
    }

    private void Animar(RectTransform objetivo, TipoAnimacion tipo, float duracion)
    {
        if (objetivo == null) return;

        // Si ya se estaba animando, se reinicia
        for (int i = animaciones.Count - 1; i >= 0; i--)
        {
            if (animaciones[i].rt == objetivo) animaciones.RemoveAt(i);
        }

        animaciones.Add(new Animacion { rt = objetivo, tipo = tipo, tiempo = 0f, duracion = duracion });
    }

    private void ActualizarAnimaciones(float dt)
    {
        for (int i = animaciones.Count - 1; i >= 0; i--)
        {
            Animacion a = animaciones[i];
            if (a.rt == null)
            {
                animaciones.RemoveAt(i);
                continue;
            }

            a.tiempo += dt;
            float k = Mathf.Clamp01(a.tiempo / a.duracion);

            float escala;
            if (a.tipo == TipoAnimacion.Pop)
            {
                // Aparece chico, se pasa de tamaño y vuelve a 1
                escala = k < 0.5f
                    ? Mathf.Lerp(0.6f, 1.2f, Mathf.SmoothStep(0f, 1f, k / 0.5f))
                    : Mathf.Lerp(1.2f, 1f, Mathf.SmoothStep(0f, 1f, (k - 0.5f) / 0.5f));
            }
            else
            {
                // Latido: crece un 12% y vuelve
                escala = 1f + 0.12f * Mathf.Sin(k * Mathf.PI);
            }

            a.rt.localScale = new Vector3(escala, escala, 1f);

            if (k >= 1f)
            {
                a.rt.localScale = Vector3.one;
                animaciones.RemoveAt(i);
            }
            else
            {
                animaciones[i] = a;
            }
        }
    }

    /// <summary>Signos "$" que salen de los nodos de Operaciones, más seguido cuanto más se factura.</summary>
    private void ActualizarSignos(float dt)
    {
        GameManager gm = GameManager.Instance;

        if (gm != null && nodosPersonal.Count > 0)
        {
            double porSegundo = gm.IngresoPorSegundo;
            float tasa = Mathf.Min(maxSignosPorSegundo, (float)System.Math.Log10(porSegundo + 1.0) * 1.2f);
            acumuladorSignos += tasa * dt;

            while (acumuladorSignos >= 1f)
            {
                acumuladorSignos -= 1f;
                LanzarSigno(nodosPersonal[Random.Range(0, nodosPersonal.Count)]);
            }
        }

        for (int i = signos.Count - 1; i >= 0; i--)
        {
            Signo s = signos[i];
            s.tiempo += dt;
            float k = s.tiempo / duracionSigno;

            if (k >= 1f)
            {
                s.texto.gameObject.SetActive(false);
                poolSignos.Add(s.texto);
                signos.RemoveAt(i);
                continue;
            }

            float subida = altoNodo * 1.1f;
            s.texto.rectTransform.anchoredPosition = s.inicio + new Vector2(0f, subida * Mathf.SmoothStep(0f, 1f, k));
            Color c = colorSigno;
            c.a = 1f - k * k;
            s.texto.color = c;
            signos[i] = s;
        }
    }

    private void LanzarSigno(RectTransform nodo)
    {
        if (nodo == null || !nodo.gameObject.activeInHierarchy) return;

        TextMeshProUGUI texto;
        if (poolSignos.Count > 0)
        {
            texto = poolSignos[poolSignos.Count - 1];
            poolSignos.RemoveAt(poolSignos.Count - 1);
        }
        else
        {
            texto = CrearTexto("Signo", capaParticulas);
            texto.text = "$";
            texto.enableAutoSizing = false;
            texto.fontSize = 32f;
        }

        texto.gameObject.SetActive(true);
        texto.rectTransform.sizeDelta = new Vector2(40f, 40f);

        Vector2 inicio = nodo.anchoredPosition +
                         new Vector2(Random.Range(-0.3f, 0.3f) * nodo.sizeDelta.x, nodo.sizeDelta.y * 0.5f);
        texto.rectTransform.anchoredPosition = inicio;
        texto.color = colorSigno;

        signos.Add(new Signo { texto = texto, inicio = inicio, tiempo = 0f });
    }

    /// <summary>Número que cambia si cambia cualquier cantidad o nivel.</summary>
    private static long Firma(GameManager gm)
    {
        unchecked
        {
            long firma = 17;
            foreach (PuestoData p in gm.Puestos) firma = firma * 31 + gm.Cantidad(p);
            foreach (DepartamentoData d in gm.Departamentos)
            {
                firma = firma * 31 + gm.Nivel(d);
                foreach (RolDepartamento r in gm.RolesDe(d)) firma = firma * 31 + gm.PersonalRol(r);
            }
            return firma;
        }
    }

    // ------------------------------------------------------------------
    // Armado
    // ------------------------------------------------------------------
    private void Reconstruir(GameManager gm)
    {
        nodosUsados = 0;
        lineasUsadas = 0;
        foreach (List<RectTransform> lista in nodosPorPuesto.Values) lista.Clear();
        foreach (List<RectTransform> lista in nodosPorRol.Values) lista.Clear();
        nodosPorDepartamento.Clear();
        nodosPersonal.Clear();

        float anchoVentana = rt.rect.width;
        float altoVentana = rt.rect.height;
        if (anchoVentana <= 0f || altoVentana <= 0f) return;

        // --- Qué hay para dibujar ---
        filasPuestos.Clear();
        for (int i = gm.Puestos.Count - 1; i >= 0; i--)
        {
            PuestoData p = gm.Puestos[i];
            if (p != null && gm.Cantidad(p) > 0) filasPuestos.Add(p);
        }

        departamentos.Clear();
        foreach (DepartamentoData d in gm.Departamentos) if (d != null) departamentos.Add(d);

        // --- Ancho de cada columna (Operaciones + un departamento por columna) ---
        anchosColumnas.Clear();
        float anchoOperaciones = anchoArea;
        foreach (PuestoData p in filasPuestos)
        {
            anchoOperaciones = Mathf.Max(anchoOperaciones, AnchoFila(Mathf.Min(gm.Cantidad(p), maxPorFila), anchoNodo, separacionNodos));
        }
        anchosColumnas.Add(anchoOperaciones);

        float sangria = 34f; // espacio para la línea del costado en los departamentos
        int maxRenglones = 0;
        foreach (DepartamentoData d in departamentos)
        {
            float anchoColumna = anchoArea;
            int renglones = 0;
            foreach (RolDepartamento rol in gm.RolesDe(d))
            {
                int n = gm.PersonalRol(rol);
                if (n <= 0) continue;
                renglones++;
                anchoColumna = Mathf.Max(anchoColumna, sangria + AnchoFila(Mathf.Min(n, maxPorFilaArea), anchoNodoArea, separacionNodosArea));
            }
            maxRenglones = Mathf.Max(maxRenglones, renglones);
            anchosColumnas.Add(anchoColumna);
        }

        // --- Tamaño total ---
        float anchoTotal = 2f * margen;
        for (int i = 0; i < anchosColumnas.Count; i++) anchoTotal += anchosColumnas[i] + (i > 0 ? separacionColumnas : 0f);

        float yCEO = margen + altoCEO * 0.5f;
        float yBarra = yCEO + altoCEO * 0.5f + separacionNiveles * 0.5f;
        float yArea = yCEO + altoCEO * 0.5f + separacionNiveles + altoArea * 0.5f;
        float yInicioFilas = yArea + altoArea * 0.5f + separacionNiveles;
        float pasoFila = altoNodo + separacionNiveles;
        float pasoRenglon = altoNodoArea + 20f;

        float altoOperaciones = yInicioFilas + Mathf.Max(1, filasPuestos.Count) * pasoFila;
        float altoDepartamentos = yArea + altoArea * 0.5f + 30f + maxRenglones * pasoRenglon;
        float altoTotal = Mathf.Max(altoOperaciones, altoDepartamentos) + margen;

        // La primera vez, si no entra a lo ancho, se aleja lo justo para ver todas las áreas
        if (primeraVez && anchoTotal > anchoVentana)
        {
            zoom = Mathf.Clamp(anchoVentana / anchoTotal, zoomMinimo, 1f);
            contenido.localScale = new Vector3(zoom, zoom, 1f);
        }

        float anchoContenido = Mathf.Max(anchoTotal, anchoVentana / zoom);
        float altoContenido = Mathf.Max(altoTotal, altoVentana / zoom);
        contenido.sizeDelta = new Vector2(anchoContenido, altoContenido);
        float desplazamiento = (anchoContenido - anchoTotal) * 0.5f; // centra si sobra lugar

        // --- Centro de cada columna ---
        float x = desplazamiento + margen;
        float xPrimera = 0f, xUltima = 0f;
        float[] centros = new float[anchosColumnas.Count];
        for (int i = 0; i < anchosColumnas.Count; i++)
        {
            centros[i] = x + anchosColumnas[i] * 0.5f;
            x += anchosColumnas[i] + separacionColumnas;
        }
        xPrimera = centros[0];
        xUltima = centros[centros.Length - 1];

        // --- CEO arriba de todo, centrado sobre las áreas ---
        float xCEO = (xPrimera + xUltima) * 0.5f;
        string empresa = gm.EmpresaFundada ? gm.Estado.nombreEmpresa : "Tu empresa";
        NodoCEO = UsarNodo(xCEO, yCEO, anchoCEO, altoCEO, colorCEO, $"CEO\n<size=60%>{empresa}</size>").rt;

        // --- Barra que baja del CEO a cada área ---
        LineaVertical(xCEO, yCEO + altoCEO * 0.5f, yBarra);
        if (centros.Length > 1) LineaHorizontal(xPrimera, xUltima, yBarra);

        // --- Columna Operaciones ---
        int totalOperaciones = 0;
        foreach (PuestoData p in gm.Puestos) totalOperaciones += gm.Cantidad(p);

        LineaVertical(centros[0], yBarra, yArea - altoArea * 0.5f);
        UsarNodo(centros[0], yArea, anchoArea, altoArea, colorOperaciones,
            $"Operaciones\n<size=65%>{Personas(totalOperaciones)}</size>");
        DibujarOperaciones(gm, centros[0], yArea + altoArea * 0.5f, yInicioFilas, pasoFila);

        // --- Columnas de departamentos ---
        for (int i = 0; i < departamentos.Count; i++)
        {
            DibujarDepartamento(gm, departamentos[i], centros[i + 1], anchosColumnas[i + 1], yBarra, yArea, pasoRenglon, sangria);
        }

        // --- Texto de ayuda si todavía no hay nadie ---
        bool hayAlguien = filasPuestos.Count > 0;
        foreach (DepartamentoData d in departamentos) if (gm.PersonalDepartamento(d) > 0) hayAlguien = true;

        textoAyuda.gameObject.SetActive(!hayAlguien);
        if (!hayAlguien)
        {
            RectTransform rtAyuda = textoAyuda.rectTransform;
            rtAyuda.anchoredPosition = new Vector2(xCEO, -(yInicioFilas + 10f));
            rtAyuda.sizeDelta = new Vector2(Mathf.Min(anchoVentana, anchoTotal) - 2f * margen, 60f);
            textoAyuda.transform.SetAsLastSibling();
        }

        // Apaga lo que sobró del pool
        for (int i = nodosUsados; i < poolNodos.Count; i++) poolNodos[i].rt.gameObject.SetActive(false);
        for (int i = lineasUsadas; i < poolLineas.Count; i++) poolLineas[i].gameObject.SetActive(false);

        // La primera vez, se arranca mirando arriba con el CEO en el centro
        if (primeraVez)
        {
            primeraVez = false;
            scroll.StopMovement();
            float sobrante = anchoContenido * zoom - anchoVentana;
            scroll.horizontalNormalizedPosition = sobrante > 0f ? Mathf.Clamp01((xCEO * zoom - anchoVentana * 0.5f) / sobrante) : 0.5f;
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    /// <summary>La cadena de mando (VP → Empleados) en filas, con líneas en codo al jefe más cercano.</summary>
    private void DibujarOperaciones(GameManager gm, float xColumna, float yInferiorArea, float yInicioFilas, float pasoFila)
    {
        xsArriba.Clear();
        xsArriba.Add(xColumna);
        float yInferiorArriba = yInferiorArea;

        for (int k = 0; k < filasPuestos.Count; k++)
        {
            PuestoData puesto = filasPuestos[k];
            int cantidad = gm.Cantidad(puesto);
            float y = yInicioFilas + k * pasoFila + altoNodo * 0.5f;

            int visibles = Mathf.Min(cantidad, maxPorFila);
            bool hayMas = cantidad > maxPorFila;

            float total = AnchoFila(visibles, anchoNodo, separacionNodos);
            float x0 = xColumna - total * 0.5f + anchoNodo * 0.5f;

            List<RectTransform> nodosPuesto = ObtenerLista(nodosPorPuesto, puesto);
            xsFila.Clear();

            for (int j = 0; j < visibles; j++)
            {
                float x = x0 + j * (anchoNodo + separacionNodos);
                xsFila.Add(x);

                bool esMas = hayMas && j == visibles - 1;
                string texto = esMas ? "+" + FormatoNumeros.Formatear(cantidad - (visibles - 1)) : puesto.inicial;
                Nodo nodo = UsarNodo(x, y, anchoNodo, altoNodo, esMas ? colorMas : puesto.color, texto);
                nodosPuesto.Add(nodo.rt);
                nodosPersonal.Add(nodo.rt);
            }

            ConectarFilas(xsArriba, yInferiorArriba, xsFila, y - altoNodo * 0.5f);

            // La fila actual pasa a ser "la de arriba"
            List<float> aux = xsArriba;
            xsArriba = xsFila;
            xsFila = aux;
            yInferiorArriba = y + altoNodo * 0.5f;
        }
    }

    /// <summary>
    /// Un departamento: su caja (con nivel) colgando del CEO y, debajo, un renglón por cada trabajo
    /// con gente, unidos por una línea al costado izquierdo.
    /// </summary>
    private void DibujarDepartamento(GameManager gm, DepartamentoData d, float xColumna, float anchoColumna,
        float yBarra, float yArea, float pasoRenglon, float sangria)
    {
        int nivel = gm.Nivel(d);
        int personal = gm.PersonalDepartamento(d);

        Color color = d.color;
        string subtitulo;
        if (nivel == 0)
        {
            color.a = 0.35f; // todavía no se abrió
            subtitulo = "Sin abrir";
        }
        else
        {
            subtitulo = $"Nv {nivel} · {Personas(personal)}";
        }

        LineaVertical(xColumna, yBarra, yArea - altoArea * 0.5f);
        Nodo caja = UsarNodo(xColumna, yArea, anchoArea, altoArea, color, $"{d.sigla}\n<size=65%>{subtitulo}</size>");
        nodosPorDepartamento[d] = caja.rt;
        if (nivel == 0)
        {
            Color apagado = colorTextoNodo;
            apagado.a = 0.8f;
            caja.texto.color = apagado;
        }

        if (personal <= 0) return;

        // Renglones: un trabajo por renglón (los más altos arriba)
        float izquierda = xColumna - anchoColumna * 0.5f;
        float xLinea = izquierda + sangria * 0.35f;
        float yRenglon = yArea + altoArea * 0.5f + 30f + altoNodoArea * 0.5f;
        float yUltimo = yRenglon;

        IReadOnlyList<RolDepartamento> roles = gm.RolesDe(d);
        for (int r = roles.Count - 1; r >= 0; r--)
        {
            RolDepartamento rol = roles[r];
            int cantidad = gm.PersonalRol(rol);
            if (cantidad <= 0) continue;

            int visibles = Mathf.Min(cantidad, maxPorFilaArea);
            bool hayMas = cantidad > maxPorFilaArea;
            float x0 = izquierda + sangria + anchoNodoArea * 0.5f;
            List<RectTransform> nodosRol = ObtenerLista(nodosPorRol, rol);

            LineaHorizontal(xLinea, izquierda + sangria, yRenglon);

            for (int j = 0; j < visibles; j++)
            {
                float x = x0 + j * (anchoNodoArea + separacionNodosArea);
                bool esMas = hayMas && j == visibles - 1;
                string texto = esMas ? "+" + FormatoNumeros.Formatear(cantidad - (visibles - 1)) : rol.inicial;
                Nodo nodo = UsarNodo(x, yRenglon, anchoNodoArea, altoNodoArea, esMas ? colorMas : d.color, texto);
                nodosRol.Add(nodo.rt);
            }

            yUltimo = yRenglon;
            yRenglon += pasoRenglon;
        }

        // Línea del costado: de la caja del departamento hasta el último renglón
        LineaHorizontal(xColumna, xLinea, yArea + altoArea * 0.5f + 14f);
        LineaVertical(xColumna, yArea + altoArea * 0.5f, yArea + altoArea * 0.5f + 14f);
        LineaVertical(xLinea, yArea + altoArea * 0.5f + 14f, yUltimo);
    }

    private static float AnchoFila(int cantidad, float ancho, float separacion)
    {
        return cantidad <= 0 ? 0f : cantidad * ancho + (cantidad - 1) * separacion;
    }

    private static string Personas(int cantidad)
    {
        return cantidad == 1 ? "1 persona" : FormatoNumeros.Formatear(cantidad) + " personas";
    }

    /// <summary>Líneas en codo: cada jefe baja, cruza en horizontal y baja a cada subordinado.</summary>
    private void ConectarFilas(List<float> padres, float yInferiorPadre, List<float> hijos, float ySuperiorHijo)
    {
        int k = padres.Count;
        int m = hijos.Count;
        if (k == 0 || m == 0) return;

        float yMedio = (yInferiorPadre + ySuperiorHijo) * 0.5f;

        for (int pi = 0; pi < k; pi++)
        {
            float minX = padres[pi];
            float maxX = padres[pi];
            bool tieneHijos = false;

            for (int j = 0; j < m; j++)
            {
                if (JefeMasCercano(padres, hijos[j]) != pi) continue; // cada uno cuelga del jefe más cercano

                tieneHijos = true;
                minX = Mathf.Min(minX, hijos[j]);
                maxX = Mathf.Max(maxX, hijos[j]);
                LineaVertical(hijos[j], yMedio, ySuperiorHijo);
            }

            if (!tieneHijos) continue;

            LineaVertical(padres[pi], yInferiorPadre, yMedio);
            if (maxX > minX) LineaHorizontal(minX, maxX, yMedio);
        }
    }

    /// <summary>Índice del jefe (fila de arriba) más cercano en horizontal a esta x.</summary>
    private static int JefeMasCercano(List<float> padres, float x)
    {
        int mejor = 0;
        float menorDistancia = float.MaxValue;
        for (int i = 0; i < padres.Count; i++)
        {
            float distancia = Mathf.Abs(padres[i] - x);
            if (distancia < menorDistancia)
            {
                menorDistancia = distancia;
                mejor = i;
            }
        }
        return mejor;
    }

    // ------------------------------------------------------------------
    // Piezas
    // ------------------------------------------------------------------
    private void LineaVertical(float x, float y1, float y2)
    {
        if (Mathf.Abs(y2 - y1) < 0.5f) return;
        Image linea = UsarLinea();
        RectTransform r = linea.rectTransform;
        r.anchoredPosition = new Vector2(x, -(y1 + y2) * 0.5f);
        r.sizeDelta = new Vector2(grosorLinea, Mathf.Abs(y2 - y1) + grosorLinea);
    }

    private void LineaHorizontal(float x1, float x2, float y)
    {
        if (Mathf.Abs(x2 - x1) < 0.5f) return;
        Image linea = UsarLinea();
        RectTransform r = linea.rectTransform;
        r.anchoredPosition = new Vector2((x1 + x2) * 0.5f, -y);
        r.sizeDelta = new Vector2(Mathf.Abs(x2 - x1) + grosorLinea, grosorLinea);
    }

    private Nodo UsarNodo(float x, float y, float ancho, float alto, Color color, string texto)
    {
        Nodo nodo;
        if (nodosUsados < poolNodos.Count)
        {
            nodo = poolNodos[nodosUsados];
        }
        else
        {
            nodo = CrearNodo();
            poolNodos.Add(nodo);
        }
        nodosUsados++;

        nodo.rt.gameObject.SetActive(true);
        nodo.rt.anchoredPosition = new Vector2(x, -y);
        nodo.rt.sizeDelta = new Vector2(ancho, alto);
        nodo.rt.localScale = Vector3.one;

        nodo.imagen.color = color;
        // Esquinas proporcionales al tamaño (más chico = menos redondeo)
        nodo.imagen.pixelsPerUnitMultiplier = Mathf.Max(0.3f, 50f / alto);

        nodo.texto.text = texto;
        nodo.texto.color = colorTextoNodo;
        nodo.texto.fontSizeMax = Mathf.Max(12f, alto * 0.42f);
        return nodo;
    }

    private Image UsarLinea()
    {
        Image linea;
        if (lineasUsadas < poolLineas.Count)
        {
            linea = poolLineas[lineasUsadas];
        }
        else
        {
            GameObject go = new GameObject("Linea", typeof(RectTransform));
            go.layer = gameObject.layer;
            RectTransform r = go.GetComponent<RectTransform>();
            r.SetParent(capaLineas, false);
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            linea = go.AddComponent<Image>();
            linea.raycastTarget = false;
            poolLineas.Add(linea);
        }
        lineasUsadas++;

        linea.gameObject.SetActive(true);
        linea.color = colorLinea;
        return linea;
    }

    private Nodo CrearNodo()
    {
        GameObject go = new GameObject("Nodo", typeof(RectTransform));
        go.layer = gameObject.layer;
        RectTransform r = go.GetComponent<RectTransform>();
        r.SetParent(capaNodos, false);
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);

        Image imagen = go.AddComponent<Image>();
        imagen.sprite = spriteNodo;
        imagen.type = spriteNodo != null ? Image.Type.Sliced : Image.Type.Simple;
        imagen.raycastTarget = false;

        TextMeshProUGUI texto = CrearTexto("Texto", r);
        RectTransform rTexto = texto.rectTransform;
        rTexto.anchorMin = Vector2.zero;
        rTexto.anchorMax = Vector2.one;
        rTexto.offsetMin = Vector2.zero;
        rTexto.offsetMax = Vector2.zero;
        texto.margin = new Vector4(4f, 2f, 4f, 2f);

        return new Nodo { rt = r, imagen = imagen, texto = texto };
    }

    private TextMeshProUGUI CrearTexto(string nombre, Transform padre)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = gameObject.layer;
        RectTransform r = go.GetComponent<RectTransform>();
        r.SetParent(padre, false);
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.enableAutoSizing = true;
        t.fontSizeMin = 10f;
        t.fontSizeMax = 40f;
        t.raycastTarget = false;
        return t;
    }

    private RectTransform CrearCapa(string nombre)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = gameObject.layer;
        RectTransform r = go.GetComponent<RectTransform>();
        r.SetParent(contenido, false);
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        return r;
    }

    private static List<RectTransform> ObtenerLista<T>(Dictionary<T, List<RectTransform>> diccionario, T clave)
    {
        if (!diccionario.TryGetValue(clave, out List<RectTransform> lista))
        {
            lista = new List<RectTransform>();
            diccionario[clave] = lista;
        }
        return lista;
    }
}
