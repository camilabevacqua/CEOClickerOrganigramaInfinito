using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lista de etapas en la pestaña Empresa: lograda / actual / futura con su meta de facturación.
/// Solo lee del GameManager.
/// </summary>
public class UIListaEtapas : MonoBehaviour
{
    private const float IntervaloRefresco = 0.25f;

    [SerializeField] private UIEtapaItem prefabItem;
    [SerializeField] private RectTransform contenedor;

    private readonly List<UIEtapaItem> filas = new List<UIEtapaItem>();
    private float proximoRefresco;

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || prefabItem == null || contenedor == null)
        {
            Debug.LogWarning("UIListaEtapas: falta el GameManager, el prefab o el contenedor.");
            return;
        }

        for (int i = contenedor.childCount - 1; i >= 0; i--)
        {
            Destroy(contenedor.GetChild(i).gameObject);
        }

        for (int i = 0; i < gm.CantidadEtapas; i++)
        {
            UIEtapaItem fila = Instantiate(prefabItem, contenedor);
            fila.name = "Etapa_" + (i + 1);
            filas.Add(fila);
        }

        Refrescar();
    }

    private void OnEnable()
    {
        proximoRefresco = 0f;
    }

    private void Update()
    {
        if (Time.unscaledTime >= proximoRefresco) Refrescar();
    }

    private void Refrescar()
    {
        proximoRefresco = Time.unscaledTime + IntervaloRefresco;

        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        for (int i = 0; i < filas.Count; i++)
        {
            EstadoEtapa estado = i < gm.IndiceEtapa ? EstadoEtapa.Lograda
                               : i == gm.IndiceEtapa ? EstadoEtapa.Actual
                               : EstadoEtapa.Futura;
            filas[i].Mostrar(i + 1, gm.Etapa(i), estado);
        }
    }
}
