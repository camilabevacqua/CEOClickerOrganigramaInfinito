using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum EstadoEtapa { Lograda, Actual, Futura }

/// <summary>
/// Una fila de la lista de etapas (pestaña Empresa). Solo muestra lo que le pasan.
/// </summary>
public class UIEtapaItem : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Image fondo;
    [SerializeField] private Image marcador;
    [SerializeField] private TMP_Text textoNumero;
    [SerializeField] private TMP_Text textoEtapa;
    [SerializeField] private TMP_Text textoEmpresa;
    [SerializeField] private TMP_Text textoMeta;

    [Header("Colores")]
    [SerializeField] private Color fondoNormal = new Color32(0xF3, 0xF5, 0xF8, 0xFF);
    [SerializeField] private Color fondoActual = new Color32(0xFF, 0xF1, 0xDC, 0xFF);
    [SerializeField] private Color colorLograda = new Color32(0x0A, 0x74, 0x58, 0xFF);
    [SerializeField] private Color colorActual = new Color32(0x9A, 0x5B, 0x00, 0xFF);
    [SerializeField] private Color colorFutura = new Color32(0xA7, 0xB0, 0xBE, 0xFF);
    [SerializeField] private Color colorTexto = new Color32(0x13, 0x20, 0x3A, 0xFF);
    [SerializeField] private Color colorTextoSuave = new Color32(0x5A, 0x64, 0x78, 0xFF);

    private EstadoEtapa? ultimoEstado;

    public void Mostrar(int numero, string nombreEtapa, string nombreEmpresa, double minimoFacturado, EstadoEtapa estado)
    {
        Poner(textoNumero, numero.ToString());
        Poner(textoEtapa, nombreEtapa);
        Poner(textoEmpresa, nombreEmpresa);

        switch (estado)
        {
            case EstadoEtapa.Lograda:
                Poner(textoMeta, "Lograda");
                break;
            case EstadoEtapa.Actual:
                Poner(textoMeta, "Actual");
                break;
            default:
                Poner(textoMeta, FormatoNumeros.Plata(minimoFacturado));
                break;
        }

        // Los colores solo se tocan si cambió el estado
        if (ultimoEstado == estado) return;
        ultimoEstado = estado;

        Color colorEstado = estado == EstadoEtapa.Lograda ? colorLograda
                          : estado == EstadoEtapa.Actual ? colorActual
                          : colorFutura;

        fondo.color = estado == EstadoEtapa.Actual ? fondoActual : fondoNormal;
        marcador.color = colorEstado;
        textoMeta.color = colorEstado;
        textoEtapa.color = estado == EstadoEtapa.Futura ? colorTextoSuave : colorTexto;
        textoEmpresa.color = estado == EstadoEtapa.Futura ? colorFutura : colorTextoSuave;
    }

    private static void Poner(TMP_Text texto, string valor)
    {
        if (texto != null && texto.text != valor) texto.text = valor;
    }
}
