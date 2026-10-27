using TMPro;
using UnityEngine;

/// <summary>
/// Barra fija de arriba: rubro, etapa y nombre de la empresa (izquierda), plata y $/seg (derecha)
/// y barra de progreso hacia la próxima etapa. Solo lee del GameManager.
/// </summary>
public class UIBarraSuperior : MonoBehaviour
{
    [Header("Plata")]
    [SerializeField] private TMP_Text textoPlata;
    [SerializeField] private TMP_Text textoPorSegundo;

    [Header("Etapa")]
    [SerializeField] private TMP_Text textoEtapa;
    [SerializeField] private TMP_Text textoEmpresa;
    [Tooltip("Relleno de la barra: su ancla derecha se mueve de 0 a 1 según el progreso.")]
    [SerializeField] private RectTransform rellenoProgreso;

    // Para no rearmar el texto de la etapa en cada frame
    private int ultimaEtapa = -1;
    private string ultimoNombre;
    private string ultimoRubro;

    private void Awake()
    {
        // "Rubro · Etapa" puede ser largo: una sola línea que achica la letra hasta que entre
        if (textoEtapa != null)
        {
            textoEtapa.textWrappingMode = TextWrappingModes.NoWrap;
            textoEtapa.enableAutoSizing = true;
            textoEtapa.fontSizeMin = 16f;
            textoEtapa.fontSizeMax = 28f;
            textoEtapa.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    private void LateUpdate()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        Poner(textoPlata, FormatoNumeros.Plata(gm.Estado.plata));
        Poner(textoPorSegundo, "+" + FormatoNumeros.Plata(gm.IngresoPorSegundo) + "/s");

        RubrosConfig.Rubro rubro = gm.RubroActual;
        string idRubro = rubro != null ? rubro.id : "";
        if (gm.IndiceEtapa != ultimaEtapa || gm.Estado.nombreEmpresa != ultimoNombre || idRubro != ultimoRubro)
        {
            ultimaEtapa = gm.IndiceEtapa;
            ultimoNombre = gm.Estado.nombreEmpresa;
            ultimoRubro = idRubro;

            // "Pastelería · Horneando en un garaje", con el rubro en su color
            string etapa = gm.NombreEtapa(gm.IndiceEtapa);
            if (rubro != null)
            {
                etapa = $"<color=#{ColorUtility.ToHtmlStringRGB(rubro.color)}>{rubro.nombre}</color> · {etapa}";
            }
            Poner(textoEtapa, etapa);
            Poner(textoEmpresa, gm.NombreEmpresa(gm.IndiceEtapa));
        }

        if (rellenoProgreso != null)
        {
            float progreso = (float)gm.ProgresoEtapa;
            Vector2 anclaMax = rellenoProgreso.anchorMax;
            if (!Mathf.Approximately(anclaMax.x, progreso))
            {
                anclaMax.x = progreso;
                rellenoProgreso.anchorMax = anclaMax;
            }
        }
    }

    // Solo reescribe si cambió (TextMeshPro regenera la malla en cada cambio)
    private static void Poner(TMP_Text texto, string valor)
    {
        if (texto != null && texto.text != valor) texto.text = valor;
    }
}
