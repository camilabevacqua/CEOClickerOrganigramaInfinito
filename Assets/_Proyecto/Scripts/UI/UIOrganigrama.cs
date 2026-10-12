using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dibuja el organigrama: CEO arriba, una fila por puesto con gente (VP arriba, Empleado abajo),
/// líneas en codo de cada jefe a sus subordinados, "+N" si no entran y los departamentos
/// al costado del CEO con línea punteada. Solo lee del GameManager.
/// Se reconstruye solo cuando cambian cantidades/niveles o el tamaño del panel.
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
    [SerializeField] private float grosorLinea = 3f;
    [SerializeField] private float margen = 20f;

    // Nodo reutilizable (rectángulo de color + texto)
    private class Nodo
    {
        public RectTransform rt;
        public Image imagen;
        public TextMeshProUGUI texto;
    }

    private RectTransform rt;
    private RectTransform capaLineas;
    private RectTransform capaNodos;
    private TextMeshProUGUI textoAyuda;

    private readonly List<Nodo> poolNodos = new List<Nodo>();
    private readonly List<Image> poolLineas = new List<Image>();
    private int nodosUsados;
    private int lineasUsadas;

    private readonly List<PuestoData> filasPuestos = new List<PuestoData>();
    private List<float> xsArriba = new List<float>();
    private List<float> xsFila = new List<float>();

    private readonly Dictionary<PuestoData, List<RectTransform>> nodosPorPuesto = new Dictionary<PuestoData, List<RectTransform>>();
    private static readonly List<RectTransform> ListaVacia = new List<RectTransform>();

    private bool sucio = true;
    private long ultimaFirma = long.MinValue;
    private Vector2 ultimoTamanio;

    /// <summary>Nodo del CEO (para animaciones del Paso 8).</summary>
    public RectTransform NodoCEO { get; private set; }

    /// <summary>Nodos visibles de un puesto (para animaciones del Paso 8).</summary>
    public IReadOnlyList<RectTransform> NodosDe(PuestoData puesto)
    {
        return puesto != null && nodosPorPuesto.TryGetValue(puesto, out List<RectTransform> lista) ? lista : ListaVacia;
    }

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        capaLineas = CrearCapa("Lineas");
        capaNodos = CrearCapa("Nodos");

        textoAyuda = CrearTexto("Ayuda", capaNodos);
        textoAyuda.text = "Contratá personal para armar tu organigrama";
        textoAyuda.color = colorAyuda;
        textoAyuda.fontStyle = FontStyles.Normal;
        textoAyuda.enableAutoSizing = true;
        textoAyuda.fontSizeMin = 18f;
        textoAyuda.fontSizeMax = 32f;
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

        if (!sucio && firma == ultimaFirma && tamanio == ultimoTamanio) return;

        sucio = false;
        ultimaFirma = firma;
        ultimoTamanio = tamanio;
        Reconstruir(gm);
    }

    /// <summary>Número que cambia si cambia cualquier cantidad o nivel.</summary>
    private static long Firma(GameManager gm)
    {
        unchecked
        {
            long firma = 17;
            foreach (PuestoData p in gm.Puestos) firma = firma * 31 + gm.Cantidad(p);
            foreach (DepartamentoData d in gm.Departamentos) firma = firma * 31 + gm.Nivel(d);
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

        float ancho = rt.rect.width;
        float alto = rt.rect.height;
        if (ancho <= 0f || alto <= 0f) return;

        // Filas: de arriba (VP) hacia abajo (Empleado), solo puestos con gente
        filasPuestos.Clear();
        for (int i = gm.Puestos.Count - 1; i >= 0; i--)
        {
            PuestoData p = gm.Puestos[i];
            if (p != null && gm.Cantidad(p) > 0) filasPuestos.Add(p);
        }

        int filas = 1 + filasPuestos.Count;
        float anchoUtil = ancho - 2f * margen;
        float altoUtil = alto - 2f * margen;
        float altoFila = altoUtil / filas;

        float altoNodo = Mathf.Clamp(altoFila * 0.55f, 20f, 80f);
        float anchoNodo = altoNodo * 1.2f;
        float separacion = Mathf.Max(6f, anchoNodo * 0.25f);
        float altoCEO = Mathf.Min(altoNodo * 1.15f, altoFila * 0.8f);
        float anchoCEO = altoCEO * 1.5f;

        // --- CEO ---
        float xCentro = ancho * 0.5f;
        float yCEO = margen + altoFila * 0.5f;
        NodoCEO = UsarNodo(xCentro, yCEO, anchoCEO, altoCEO, colorCEO, "CEO").rt;

        DibujarDepartamentos(gm, xCentro, yCEO, anchoCEO, altoCEO, anchoNodo, ancho);

        xsArriba.Clear();
        xsArriba.Add(xCentro);
        float yArriba = yCEO;
        float altoArriba = altoCEO;

        // --- Filas de puestos ---
        int maxNodos = Mathf.Max(1, Mathf.FloorToInt((anchoUtil + separacion) / (anchoNodo + separacion)));

        for (int k = 0; k < filasPuestos.Count; k++)
        {
            PuestoData puesto = filasPuestos[k];
            int cantidad = gm.Cantidad(puesto);
            float y = margen + altoFila * (k + 1.5f);

            int visibles = Mathf.Min(cantidad, maxNodos);
            bool hayMas = cantidad > maxNodos;

            float total = visibles * anchoNodo + (visibles - 1) * separacion;
            float x0 = margen + (anchoUtil - total) * 0.5f + anchoNodo * 0.5f;

            List<RectTransform> nodosPuesto = ObtenerLista(puesto);
            xsFila.Clear();

            for (int j = 0; j < visibles; j++)
            {
                float x = x0 + j * (anchoNodo + separacion);
                xsFila.Add(x);

                bool esMas = hayMas && j == visibles - 1;
                string texto = esMas ? "+" + FormatoNumeros.Formatear(cantidad - (visibles - 1)) : puesto.inicial;
                Nodo nodo = UsarNodo(x, y, anchoNodo, altoNodo, esMas ? colorMas : puesto.color, texto);
                nodosPuesto.Add(nodo.rt);
            }

            ConectarFilas(xsArriba, yArriba + altoArriba * 0.5f, xsFila, y - altoNodo * 0.5f);

            // La fila actual pasa a ser "la de arriba"
            List<float> aux = xsArriba;
            xsArriba = xsFila;
            xsFila = aux;
            yArriba = y;
            altoArriba = altoNodo;
        }

        // --- Texto de ayuda si todavía no hay personal ---
        bool sinPersonal = filasPuestos.Count == 0;
        textoAyuda.gameObject.SetActive(sinPersonal);
        if (sinPersonal)
        {
            RectTransform rtAyuda = textoAyuda.rectTransform;
            rtAyuda.anchoredPosition = new Vector2(xCentro, -(yCEO + altoCEO * 0.5f + 50f));
            rtAyuda.sizeDelta = new Vector2(anchoUtil, 60f);
            textoAyuda.transform.SetAsLastSibling();
        }

        // Apaga lo que sobró del pool
        for (int i = nodosUsados; i < poolNodos.Count; i++) poolNodos[i].rt.gameObject.SetActive(false);
        for (int i = lineasUsadas; i < poolLineas.Count; i++) poolLineas[i].gameObject.SetActive(false);
    }

    /// <summary>Cajitas RRHH / MKT / FIN alternando a los costados del CEO, con línea punteada.</summary>
    private void DibujarDepartamentos(GameManager gm, float xCentro, float yCEO, float anchoCEO, float altoCEO,
        float anchoNodo, float anchoPanel)
    {
        IReadOnlyList<DepartamentoData> departamentos = gm.Departamentos;
        if (departamentos.Count == 0) return;

        float altoCaja = altoCEO * 0.8f;
        float anchoCaja = Mathf.Max(anchoNodo * 1.5f, altoCaja * 1.4f);
        float separacion = Mathf.Max(12f, anchoCaja * 0.2f);

        int slotIzquierda = 0;
        int slotDerecha = 0;
        float extremoIzquierdo = xCentro;
        float extremoDerecho = xCentro;

        for (int i = 0; i < departamentos.Count; i++)
        {
            DepartamentoData d = departamentos[i];
            if (d == null) continue;

            int lado = i % 2 == 0 ? -1 : 1; // RRHH izquierda, MKT derecha, FIN izquierda...
            int slot = lado < 0 ? slotIzquierda++ : slotDerecha++;
            float distancia = anchoCEO * 0.5f + separacion + anchoCaja * 0.5f + slot * (anchoCaja + separacion);
            float x = xCentro + lado * distancia;

            if (x - anchoCaja * 0.5f < 0f || x + anchoCaja * 0.5f > anchoPanel) continue; // no entra

            int nivel = gm.Nivel(d);
            Color color = d.color;
            if (nivel == 0) color.a = 0.35f; // apagada

            string texto = nivel > 0 ? $"{d.sigla}\n<size=70%>Nv {nivel}</size>" : d.sigla;
            Nodo caja = UsarNodo(x, yCEO, anchoCaja, altoCaja, color, texto);
            if (nivel == 0)
            {
                Color apagado = colorTextoNodo;
                apagado.a = 0.75f;
                caja.texto.color = apagado;
            }

            if (lado < 0) extremoIzquierdo = Mathf.Min(extremoIzquierdo, x);
            else extremoDerecho = Mathf.Max(extremoDerecho, x);
        }

        if (extremoIzquierdo < xCentro) LineaPunteada(extremoIzquierdo, xCentro - anchoCEO * 0.5f, yCEO);
        if (extremoDerecho > xCentro) LineaPunteada(xCentro + anchoCEO * 0.5f, extremoDerecho, yCEO);
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
                int asignado = Mathf.Min(k - 1, j * k / m); // reparte los hijos entre los jefes
                if (asignado != pi) continue;

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

    // ------------------------------------------------------------------
    // Piezas
    // ------------------------------------------------------------------
    private void LineaVertical(float x, float y1, float y2)
    {
        Image linea = UsarLinea();
        RectTransform r = linea.rectTransform;
        r.anchoredPosition = new Vector2(x, -(y1 + y2) * 0.5f);
        r.sizeDelta = new Vector2(grosorLinea, Mathf.Abs(y2 - y1) + grosorLinea);
    }

    private void LineaHorizontal(float x1, float x2, float y)
    {
        Image linea = UsarLinea();
        RectTransform r = linea.rectTransform;
        r.anchoredPosition = new Vector2((x1 + x2) * 0.5f, -y);
        r.sizeDelta = new Vector2(Mathf.Abs(x2 - x1) + grosorLinea, grosorLinea);
    }

    private void LineaPunteada(float x1, float x2, float y)
    {
        const float largoGuion = 8f;
        const float paso = 15f;

        float desde = Mathf.Min(x1, x2);
        float hasta = Mathf.Max(x1, x2);
        for (float x = desde; x < hasta; x += paso)
        {
            float fin = Mathf.Min(x + largoGuion, hasta);
            Image guion = UsarLinea();
            RectTransform r = guion.rectTransform;
            r.anchoredPosition = new Vector2((x + fin) * 0.5f, -y);
            r.sizeDelta = new Vector2(fin - x, grosorLinea);
        }
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
        nodo.texto.fontSizeMax = Mathf.Max(10f, alto * 0.5f);
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
        texto.margin = new Vector4(2f, 0f, 2f, 0f);

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
        t.fontSizeMin = 8f;
        t.fontSizeMax = 40f;
        t.raycastTarget = false;
        return t;
    }

    private RectTransform CrearCapa(string nombre)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = gameObject.layer;
        RectTransform r = go.GetComponent<RectTransform>();
        r.SetParent(transform, false);
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        return r;
    }

    private List<RectTransform> ObtenerLista(PuestoData puesto)
    {
        if (!nodosPorPuesto.TryGetValue(puesto, out List<RectTransform> lista))
        {
            lista = new List<RectTransform>();
            nodosPorPuesto[puesto] = lista;
        }
        return lista;
    }
}
