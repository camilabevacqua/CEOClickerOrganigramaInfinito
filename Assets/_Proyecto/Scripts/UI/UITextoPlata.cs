using TMPro;
using UnityEngine;

/// <summary>
/// Muestra la plata actual en un texto TextMeshPro. Solo lee del GameManager.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class UITextoPlata : MonoBehaviour
{
    private TMP_Text texto;
    private double ultimoValor = double.NaN;

    private void Awake()
    {
        texto = GetComponent<TMP_Text>();
    }

    private void LateUpdate()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        double plata = gm.Estado.plata;

        // Solo reescribimos el texto si cambió (evita generar basura cada frame)
        if (plata.Equals(ultimoValor)) return;
        ultimoValor = plata;

        texto.text = FormatoNumeros.Plata(plata);
    }
}
