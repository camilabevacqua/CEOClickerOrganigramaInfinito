using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ModoCompra { X1, X10, Max }

/// <summary>
/// Lista de la pestaña Personal: crea una fila por puesto y maneja el selector x1 / x10 / Máx.
/// </summary>
public class UIListaPersonal : MonoBehaviour
{
    [SerializeField] private UIPuestoItem prefabItem;
    [SerializeField] private RectTransform contenedor;

    [Header("Selector de cantidad")]
    [SerializeField] private Button botonX1;
    [SerializeField] private Button botonX10;
    [SerializeField] private Button botonMax;
    [SerializeField] private Color colorSeleccionado = new Color32(0x9A, 0x5B, 0x00, 0xFF);
    [SerializeField] private Color colorNormal = new Color32(0xE7, 0xEC, 0xF2, 0xFF);
    [SerializeField] private Color textoSeleccionado = Color.white;
    [SerializeField] private Color textoNormal = new Color32(0x13, 0x20, 0x3A, 0xFF);

    public ModoCompra Modo { get; private set; } = ModoCompra.X1;

    /// <summary>Avisa a las filas que cambió x1/x10/Máx.</summary>
    public event Action OnModoCambio;

    private void Start()
    {
        if (botonX1 != null) botonX1.onClick.AddListener(() => CambiarModo(ModoCompra.X1));
        if (botonX10 != null) botonX10.onClick.AddListener(() => CambiarModo(ModoCompra.X10));
        if (botonMax != null) botonMax.onClick.AddListener(() => CambiarModo(ModoCompra.Max));

        CrearItems();
        PintarSelector();
    }

    private void CrearItems()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || prefabItem == null || contenedor == null)
        {
            Debug.LogWarning("UIListaPersonal: falta el GameManager, el prefab o el contenedor.");
            return;
        }

        // Limpia filas viejas (por si quedaron en la escena)
        for (int i = contenedor.childCount - 1; i >= 0; i--)
        {
            Destroy(contenedor.GetChild(i).gameObject);
        }

        foreach (PuestoData puesto in gm.Puestos)
        {
            if (puesto == null) continue;
            UIPuestoItem item = Instantiate(prefabItem, contenedor);
            item.name = "Item_" + puesto.nombre;
            item.Configurar(puesto, this);
        }
    }

    public void CambiarModo(ModoCompra modo)
    {
        Modo = modo;
        PintarSelector();
        OnModoCambio?.Invoke();
    }

    /// <summary>Cuántos se intentan contratar con el modo actual (0 si no hay lugar).</summary>
    public int CantidadObjetivo(PuestoData puesto)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || puesto == null) return 0;

        int libres = gm.LugaresLibres(puesto);
        if (libres <= 0) return 0;

        switch (Modo)
        {
            case ModoCompra.X10:
                return Math.Min(10, libres);
            case ModoCompra.Max:
                // Si no alcanza para ninguno, mostramos el costo de 1
                return Math.Max(1, gm.MaxComprable(puesto));
            default:
                return 1;
        }
    }

    private void PintarSelector()
    {
        Pintar(botonX1, Modo == ModoCompra.X1);
        Pintar(botonX10, Modo == ModoCompra.X10);
        Pintar(botonMax, Modo == ModoCompra.Max);
    }

    private void Pintar(Button boton, bool seleccionado)
    {
        if (boton == null) return;

        if (boton.targetGraphic != null)
        {
            boton.targetGraphic.color = seleccionado ? colorSeleccionado : colorNormal;
        }

        TMP_Text texto = boton.GetComponentInChildren<TMP_Text>();
        if (texto != null) texto.color = seleccionado ? textoSeleccionado : textoNormal;
    }
}
