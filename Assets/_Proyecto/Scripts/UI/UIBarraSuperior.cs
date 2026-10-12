using TMPro;
using UnityEngine;

/// <summary>
/// Barra fija de arriba: etapa y nombre de la empresa (izquierda), plata y $/seg (derecha)
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

    private void LateUpdate()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        Poner(textoPlata, FormatoNumeros.Plata(gm.Estado.plata));
        Poner(textoPorSegundo, "+" + FormatoNumeros.Plata(gm.IngresoPorSegundo) + "/s");

        EtapasConfig.Etapa etapa = gm.EtapaActual;
        if (etapa != null)
        {
            Poner(textoEtapa, etapa.nombreEtapa);
            Poner(textoEmpresa, etapa.nombreEmpresa);
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
