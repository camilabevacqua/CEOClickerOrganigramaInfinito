using UnityEngine;

/// <summary>
/// Lista de la pestaña Departamentos: crea una fila por departamento.
/// </summary>
public class UIListaDepartamentos : MonoBehaviour
{
    [SerializeField] private UIDepartamentoItem prefabItem;
    [SerializeField] private RectTransform contenedor;

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || prefabItem == null || contenedor == null)
        {
            Debug.LogWarning("UIListaDepartamentos: falta el GameManager, el prefab o el contenedor.");
            return;
        }

        // Limpia filas viejas (por si quedaron en la escena)
        for (int i = contenedor.childCount - 1; i >= 0; i--)
        {
            Destroy(contenedor.GetChild(i).gameObject);
        }

        foreach (DepartamentoData departamento in gm.Departamentos)
        {
            if (departamento == null) continue;
            UIDepartamentoItem item = Instantiate(prefabItem, contenedor);
            item.name = "Item_" + departamento.sigla;
            item.Configurar(departamento);
        }
    }
}
