using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Botones "Sonido: Sí/No" y "Vibración: Sí/No" de la pestaña Empresa.
/// Solo lee y cambia los ajustes de Feedback.
/// </summary>
public class UIAjustes : MonoBehaviour
{
    [SerializeField] private Button botonSonido;
    [SerializeField] private Button botonVibracion;
    [SerializeField] private TMP_Text textoSonido;
    [SerializeField] private TMP_Text textoVibracion;

    [Header("Colores")]
    [SerializeField] private Color colorEncendido = new Color32(0x5F, 0xC4, 0xA6, 0xFF);
    [SerializeField] private Color colorApagado = new Color32(0xDC, 0xCF, 0xD8, 0xFF);

    private bool suscripto;

    private void Start()
    {
        botonSonido.onClick.AddListener(AlternarSonido);
        botonVibracion.onClick.AddListener(AlternarVibracion);

        if (Feedback.Instance != null)
        {
            Feedback.Instance.OnAjustesCambiados += Refrescar;
            suscripto = true;
        }

        Refrescar();
    }

    private void OnDestroy()
    {
        if (suscripto && Feedback.Instance != null) Feedback.Instance.OnAjustesCambiados -= Refrescar;
    }

    private void AlternarSonido()
    {
        if (Feedback.Instance != null) Feedback.Instance.CambiarSonido(!Feedback.Instance.SonidoActivado);
    }

    private void AlternarVibracion()
    {
        if (Feedback.Instance != null) Feedback.Instance.CambiarVibracion(!Feedback.Instance.VibracionActivada);
    }

    private void Refrescar()
    {
        Feedback f = Feedback.Instance;
        bool sonido = f == null || f.SonidoActivado;
        bool vibracion = f == null || f.VibracionActivada;

        textoSonido.text = sonido ? "Sonido: Sí" : "Sonido: No";
        textoVibracion.text = vibracion ? "Vibración: Sí" : "Vibración: No";

        if (botonSonido.targetGraphic != null) botonSonido.targetGraphic.color = sonido ? colorEncendido : colorApagado;
        if (botonVibracion.targetGraphic != null) botonVibracion.targetGraphic.color = vibracion ? colorEncendido : colorApagado;
    }
}
