using TMPro;
using UnityEngine;

/// <summary>
/// Fila de estadísticas debajo del botón Facturar: Personal, Tratos (clics) y Acciones.
/// Solo lee del GameManager.
/// </summary>
public class UIEstadisticas : MonoBehaviour
{
    private const float IntervaloRefresco = 0.2f;

    [SerializeField] private TMP_Text textoPersonal;
    [SerializeField] private TMP_Text textoTratos;
    [SerializeField] private TMP_Text textoAcciones;

    private float proximoRefresco;

    private void OnEnable()
    {
        proximoRefresco = 0f;
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < proximoRefresco) return;
        proximoRefresco = Time.unscaledTime + IntervaloRefresco;

        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        Poner(textoPersonal, $"Personal <b>{FormatoNumeros.Formatear(gm.PersonalTotal)}</b>");
        Poner(textoTratos, $"Tratos <b>{FormatoNumeros.Formatear(gm.Estado.clicsTotales)}</b>");
        Poner(textoAcciones, $"Acciones <b>{FormatoNumeros.Formatear(gm.Estado.acciones)}</b>");
    }

    private static void Poner(TMP_Text texto, string valor)
    {
        if (texto != null && texto.text != valor) texto.text = valor;
    }
}
