using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Botones "Sonido", "Música" y "Vibración" (Sí/No) de la pestaña Empresa.
/// Solo lee y cambia los ajustes de Feedback.
/// </summary>
public class UIAjustes : MonoBehaviour
{
    [SerializeField] private Button botonSonido;
    [SerializeField] private Button botonMusica;
    [SerializeField] private Button botonVibracion;
    [SerializeField] private TMP_Text textoSonido;
    [SerializeField] private TMP_Text textoMusica;
    [SerializeField] private TMP_Text textoVibracion;

    [Header("Colores")]
    [SerializeField] private Color colorEncendido = new Color32(0x5F, 0xC4, 0xA6, 0xFF);
    [SerializeField] private Color colorApagado = new Color32(0xDC, 0xCF, 0xD8, 0xFF);

    private bool suscripto;

    private void Start()
    {
        if (botonSonido != null) botonSonido.onClick.AddListener(AlternarSonido);
        if (botonMusica != null) botonMusica.onClick.AddListener(AlternarMusica);
        if (botonVibracion != null) botonVibracion.onClick.AddListener(AlternarVibracion);

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

    private void AlternarMusica()
    {
        if (Feedback.Instance != null) Feedback.Instance.CambiarMusica(!Feedback.Instance.MusicaActivada);
    }

    private void AlternarVibracion()
    {
        if (Feedback.Instance != null) Feedback.Instance.CambiarVibracion(!Feedback.Instance.VibracionActivada);
    }

    private void Refrescar()
    {
        Feedback f = Feedback.Instance;
        Pintar(botonSonido, textoSonido, "Sonido", f == null || f.SonidoActivado);
        Pintar(botonMusica, textoMusica, "Música", f == null || f.MusicaActivada);
        Pintar(botonVibracion, textoVibracion, "Vibración", f == null || f.VibracionActivada);
    }

    private void Pintar(Button boton, TMP_Text texto, string nombre, bool encendido)
    {
        if (texto != null) texto.text = nombre + (encendido ? ": Sí" : ": No");
        if (boton != null && boton.targetGraphic != null) boton.targetGraphic.color = encendido ? colorEncendido : colorApagado;
    }
}
