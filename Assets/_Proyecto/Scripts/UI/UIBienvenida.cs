using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cartel inicial "¡Fundá tu empresa!": el jugador escribe el nombre y elige un rubro.
/// Aparece si la empresa todavía no tiene nombre (primera vez o después de borrar la partida).
/// Va en un objeto siempre activo; el que se prende y apaga es "panel".
/// </summary>
public class UIBienvenida : MonoBehaviour
{
    private const float DuracionAparicion = 0.3f;

    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform tarjeta;
    [SerializeField] private TMP_InputField campoNombre;
    [SerializeField] private RectTransform contenedorRubros;
    [Tooltip("Botón de ejemplo (inactivo) que se copia una vez por rubro.")]
    [SerializeField] private Button plantillaRubro;
    [SerializeField] private Button botonEmpezar;
    [SerializeField] private TMP_Text textoAyuda;

    [Header("Colores")]
    [SerializeField] private Color colorTextoNormal = new Color32(0x5B, 0x3A, 0x55, 0xFF);
    [SerializeField] private Color colorTextoElegido = Color.white;

    private class BotonRubro
    {
        public Button boton;
        public TMP_Text texto;
        public RubrosConfig.Rubro rubro;
    }

    private readonly List<BotonRubro> botones = new List<BotonRubro>();
    private string rubroElegido;
    private float tiempoAnimacion = -1f;
    private bool suscripto;

    private void Start()
    {
        plantillaRubro.gameObject.SetActive(false);
        campoNombre.characterLimit = GameManager.MaxLargoNombre;
        campoNombre.onValueChanged.AddListener(_ => Validar());
        botonEmpezar.onClick.AddListener(Empezar);

        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        CrearBotones(gm);

        gm.OnPartidaBorrada += Mostrar;
        suscripto = true;

        if (gm.EmpresaFundada) panel.SetActive(false);
        else Mostrar();
    }

    private void OnDestroy()
    {
        if (suscripto && GameManager.Instance != null) GameManager.Instance.OnPartidaBorrada -= Mostrar;
    }

    private void CrearBotones(GameManager gm)
    {
        foreach (RubrosConfig.Rubro rubro in gm.Rubros)
        {
            Button boton = Instantiate(plantillaRubro, contenedorRubros);
            boton.gameObject.SetActive(true);
            boton.name = "Rubro_" + rubro.id;

            TMP_Text texto = boton.GetComponentInChildren<TMP_Text>();
            if (texto != null) texto.text = rubro.nombre;

            string id = rubro.id; // copia local para el lambda
            boton.onClick.AddListener(() => Elegir(id));

            botones.Add(new BotonRubro { boton = boton, texto = texto, rubro = rubro });
        }
    }

    private void Mostrar()
    {
        campoNombre.text = "";
        rubroElegido = null;
        Pintar();
        Validar();

        panel.SetActive(true);
        tarjeta.localScale = Vector3.one * 0.8f;
        tiempoAnimacion = 0f;
    }

    private void Elegir(string id)
    {
        rubroElegido = id;
        Pintar();
        Validar();
    }

    /// <summary>El rubro elegido se ve con su color fuerte; los demás, clarito.</summary>
    private void Pintar()
    {
        foreach (BotonRubro b in botones)
        {
            bool elegido = b.rubro.id == rubroElegido;
            if (b.boton.targetGraphic != null)
            {
                b.boton.targetGraphic.color = elegido ? b.rubro.color : Color.Lerp(b.rubro.color, Color.white, 0.65f);
            }
            if (b.texto != null) b.texto.color = elegido ? colorTextoElegido : colorTextoNormal;
            b.boton.transform.localScale = elegido ? Vector3.one * 1.05f : Vector3.one;
        }
    }

    private void Validar()
    {
        bool hayNombre = !string.IsNullOrEmpty(GameManager.LimpiarNombre(campoNombre.text));
        bool hayRubro = !string.IsNullOrEmpty(rubroElegido);

        botonEmpezar.interactable = hayNombre && hayRubro;

        if (textoAyuda != null)
        {
            textoAyuda.text = !hayNombre ? "Escribí el nombre de tu empresa"
                            : !hayRubro ? "Elegí un rubro"
                            : "¡Todo listo!";
        }
    }

    private void Empezar()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.FundarEmpresa(campoNombre.text, rubroElegido))
        {
            panel.SetActive(false);
            tiempoAnimacion = -1f;
        }
    }

    private void Update()
    {
        if (tiempoAnimacion < 0f) return;

        tiempoAnimacion += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(tiempoAnimacion / DuracionAparicion);

        // Aparece con un pequeño rebote
        float escala = k < 0.6f
            ? Mathf.Lerp(0.8f, 1.03f, Mathf.SmoothStep(0f, 1f, k / 0.6f))
            : Mathf.Lerp(1.03f, 1f, Mathf.SmoothStep(0f, 1f, (k - 0.6f) / 0.4f));
        tarjeta.localScale = new Vector3(escala, escala, 1f);

        if (k >= 1f) tiempoAnimacion = -1f;
    }
}
