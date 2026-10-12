using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Muestra "+$X" donde se tocó el botón Facturar: sube, crece un poquito y se desvanece.
/// Va en una capa que cubre toda el área segura, por encima del resto de la interfaz.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UITextosFlotantes : MonoBehaviour
{
    [SerializeField] private UIBotonFacturar boton;
    [SerializeField] private Color color = new Color32(0xD6, 0x5A, 0x92, 0xFF);
    [SerializeField] private float tamanioLetra = 64f;
    [SerializeField] private float duracion = 0.9f;
    [SerializeField] private float subida = 200f;
    [Tooltip("Desvío horizontal al azar, para que no se encimen.")]
    [SerializeField] private float desvioHorizontal = 50f;

    private struct Flotante
    {
        public TextMeshProUGUI texto;
        public Vector2 inicio;
        public float tiempo;
    }

    private RectTransform capa;
    private readonly List<Flotante> activos = new List<Flotante>();
    private readonly List<TextMeshProUGUI> pool = new List<TextMeshProUGUI>();
    private bool suscripto;

    private void Awake()
    {
        capa = GetComponent<RectTransform>();
    }

    private void Start()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnFacturo += AlFacturar;
        suscripto = true;
    }

    private void OnDestroy()
    {
        if (suscripto && GameManager.Instance != null) GameManager.Instance.OnFacturo -= AlFacturar;
    }

    private void AlFacturar(double valor)
    {
        // Posición del toque; si no hubo (ej: teclado), el centro del botón
        Vector2 pantalla = boton != null && boton.HuboToque
            ? boton.UltimaPosicionToque
            : RectTransformUtility.WorldToScreenPoint(null, boton != null ? boton.transform.position : capa.position);

        // Canvas en Screen Space - Overlay: la cámara es null
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(capa, pantalla, null, out Vector2 local)) return;

        local.x += Random.Range(-desvioHorizontal, desvioHorizontal);

        TextMeshProUGUI texto = Obtener();
        texto.text = "+" + FormatoNumeros.Plata(valor);
        texto.rectTransform.anchoredPosition = local;
        texto.rectTransform.localScale = Vector3.one * 0.8f;
        texto.color = color;
        texto.transform.SetAsLastSibling();

        activos.Add(new Flotante { texto = texto, inicio = local, tiempo = 0f });
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        for (int i = activos.Count - 1; i >= 0; i--)
        {
            Flotante f = activos[i];
            f.tiempo += dt;
            float k = f.tiempo / duracion;

            if (k >= 1f)
            {
                f.texto.gameObject.SetActive(false);
                pool.Add(f.texto);
                activos.RemoveAt(i);
                continue;
            }

            // Sube rápido al principio y frena (ease-out)
            float avance = 1f - (1f - k) * (1f - k);
            f.texto.rectTransform.anchoredPosition = f.inicio + new Vector2(0f, subida * avance);

            // Crece un poquito al aparecer
            float escala = Mathf.Lerp(0.8f, 1.1f, Mathf.Clamp01(k * 4f));
            f.texto.rectTransform.localScale = new Vector3(escala, escala, 1f);

            // Se desvanece en la segunda mitad
            Color c = color;
            c.a = k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f;
            f.texto.color = c;

            activos[i] = f;
        }
    }

    private TextMeshProUGUI Obtener()
    {
        TextMeshProUGUI texto;
        if (pool.Count > 0)
        {
            texto = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
        }
        else
        {
            GameObject go = new GameObject("TextoFlotante", typeof(RectTransform));
            go.layer = gameObject.layer;
            RectTransform r = go.GetComponent<RectTransform>();
            r.SetParent(capa, false);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); // coincide con el origen de ScreenPointToLocalPointInRectangle
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(500f, 100f);

            texto = go.AddComponent<TextMeshProUGUI>();
            texto.alignment = TextAlignmentOptions.Center;
            texto.fontStyle = FontStyles.Bold;
            texto.fontSize = tamanioLetra;
            texto.raycastTarget = false;
        }

        texto.gameObject.SetActive(true);
        return texto;
    }
}
