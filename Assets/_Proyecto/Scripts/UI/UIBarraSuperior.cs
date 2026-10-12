using TMPro;
using UnityEngine;

/// <summary>
/// Barra fija de arriba: plata total y ganancia por segundo.
/// (En el Paso 6 se suman la etapa, el nombre de la empresa y la barra de progreso.)
/// Solo lee del GameManager.
/// </summary>
public class UIBarraSuperior : MonoBehaviour
{
    [SerializeField] private TMP_Text textoPlata;
    [SerializeField] private TMP_Text textoPorSegundo;

    private void LateUpdate()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        Poner(textoPlata, FormatoNumeros.Plata(gm.Estado.plata));
        Poner(textoPorSegundo, "+" + FormatoNumeros.Plata(gm.IngresoPorSegundo) + "/s");
    }

    // Solo reescribe si cambió (TextMeshPro regenera la malla en cada cambio)
    private static void Poner(TMP_Text texto, string valor)
    {
        if (texto != null && texto.text != valor) texto.text = valor;
    }
}
