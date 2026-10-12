using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cartel "Mientras no estabas, tu equipo facturó $X" al volver al juego.
/// Va en un objeto siempre activo; el que se prende y apaga es "panel".
/// </summary>
public class UIAvisoOffline : MonoBehaviour
{
    private const float DuracionAparicion = 0.3f;
    private const double MaxSegundosOffline = 8 * 3600;

    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform tarjeta;
    [SerializeField] private TMP_Text textoMonto;
    [SerializeField] private TMP_Text textoDetalle;
    [SerializeField] private Button botonCerrar;

    private float tiempoAnimacion = -1f;
    private bool suscripto;

    private void Start()
    {
        panel.SetActive(false);
        botonCerrar.onClick.AddListener(Cerrar);

        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        gm.OnGananciaOffline += Mostrar;
        suscripto = true;

        // La carga ocurre en el Awake del GameManager, antes de que existiera este cartel
        if (gm.HayAvisoOfflinePendiente)
        {
            Mostrar(gm.OfflinePendienteMonto, gm.OfflinePendienteSegundos);
        }
    }

    private void OnDestroy()
    {
        if (suscripto && GameManager.Instance != null) GameManager.Instance.OnGananciaOffline -= Mostrar;
    }

    private void Mostrar(double monto, double segundos)
    {
        textoMonto.text = FormatoNumeros.Plata(monto);

        string detalle = "Estuviste afuera " + FormatearDuracion(segundos);
        if (segundos >= MaxSegundosOffline - 1) detalle += " (máximo 8 h)";
        textoDetalle.text = detalle;

        panel.SetActive(true);
        tarjeta.localScale = Vector3.one * 0.7f;
        tiempoAnimacion = 0f;
    }

    private void Update()
    {
        if (tiempoAnimacion < 0f) return;

        tiempoAnimacion += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(tiempoAnimacion / DuracionAparicion);

        // Aparece con un pequeño rebote
        float escala = k < 0.6f
            ? Mathf.Lerp(0.7f, 1.05f, Mathf.SmoothStep(0f, 1f, k / 0.6f))
            : Mathf.Lerp(1.05f, 1f, Mathf.SmoothStep(0f, 1f, (k - 0.6f) / 0.4f));
        tarjeta.localScale = new Vector3(escala, escala, 1f);

        if (k >= 1f) tiempoAnimacion = -1f;
    }

    private void Cerrar()
    {
        panel.SetActive(false);
        tiempoAnimacion = -1f;
        if (GameManager.Instance != null) GameManager.Instance.MarcarAvisoOfflineVisto();
    }

    private static string FormatearDuracion(double segundos)
    {
        int total = (int)segundos;
        if (total < 60) return $"{total} s";

        int minutos = total / 60;
        if (minutos < 60) return $"{minutos} min";

        int horas = minutos / 60;
        int resto = minutos % 60;
        return resto > 0 ? $"{horas} h {resto} min" : $"{horas} h";
    }
}
