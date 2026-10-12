using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una fila de la lista de Personal. Solo muestra datos del GameManager
/// y, al tocar el botón, le pide que contrate.
/// </summary>
public class UIPuestoItem : MonoBehaviour
{
    private const float IntervaloRefresco = 0.1f;

    [Header("Referencias")]
    [SerializeField] private Image insignia;
    [SerializeField] private TMP_Text textoInicial;
    [SerializeField] private TMP_Text textoNombre;
    [SerializeField] private TMP_Text textoCantidad;
    [SerializeField] private TMP_Text textoDetalle;
    [SerializeField] private TMP_Text textoDescripcion;
    [SerializeField] private Button botonComprar;
    [SerializeField] private TMP_Text textoBoton;

    [Header("Colores")]
    [SerializeField] private Color colorBloqueado = new Color32(0xA7, 0xB0, 0xBE, 0xFF);
    [SerializeField] private Color colorDescripcion = new Color32(0x5A, 0x64, 0x78, 0xFF);
    [SerializeField] private Color colorAviso = new Color32(0x9A, 0x5B, 0x00, 0xFF);

    private PuestoData puesto;
    private UIListaPersonal lista;
    private float proximoRefresco;
    private bool suscripto;

    public PuestoData Puesto => puesto;

    public void Configurar(PuestoData nuevoPuesto, UIListaPersonal nuevaLista)
    {
        puesto = nuevoPuesto;
        lista = nuevaLista;

        botonComprar.onClick.RemoveAllListeners();
        botonComprar.onClick.AddListener(AlComprar);

        Suscribir();
        Refrescar();
    }

    private void OnEnable() => Suscribir();
    private void OnDisable() => Desuscribir();

    private void Suscribir()
    {
        if (suscripto || lista == null || GameManager.Instance == null) return;

        GameManager.Instance.OnContrato += AlContratar;
        lista.OnModoCambio += Refrescar;
        suscripto = true;
    }

    private void Desuscribir()
    {
        if (!suscripto) return;

        if (GameManager.Instance != null) GameManager.Instance.OnContrato -= AlContratar;
        if (lista != null) lista.OnModoCambio -= Refrescar;
        suscripto = false;
    }

    private void AlContratar(PuestoData p, int cantidad) => Refrescar();

    private void Update()
    {
        // La plata cambia todo el tiempo: refrescamos 10 veces por segundo
        if (Time.unscaledTime >= proximoRefresco) Refrescar();
    }

    private void AlComprar()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || lista == null || puesto == null) return;

        int cantidad = lista.CantidadObjetivo(puesto);
        if (cantidad > 0) gm.Contratar(puesto, cantidad);
    }

    public void Refrescar()
    {
        proximoRefresco = Time.unscaledTime + IntervaloRefresco;

        GameManager gm = GameManager.Instance;
        if (gm == null || puesto == null || lista == null) return;

        // --- Bloqueado: se ve como "???" ---
        if (!gm.EstaDesbloqueado(puesto))
        {
            insignia.color = colorBloqueado;
            Poner(textoInicial, "?");
            Poner(textoNombre, "???");
            Poner(textoCantidad, "");
            Poner(textoDetalle, "");

            PuestoData anterior = gm.Anterior(puesto);
            string pista = anterior != null
                ? $"Facturá {FormatoNumeros.Plata(puesto.costoBase * 0.5)} o contratá un {anterior.nombre} para descubrirlo."
                : "";
            Poner(textoDescripcion, pista);
            textoDescripcion.color = colorDescripcion;

            if (botonComprar.gameObject.activeSelf) botonComprar.gameObject.SetActive(false);
            return;
        }

        if (!botonComprar.gameObject.activeSelf) botonComprar.gameObject.SetActive(true);

        // --- Datos del puesto ---
        insignia.color = puesto.color;
        Poner(textoInicial, puesto.inicial);
        Poner(textoNombre, puesto.nombre);

        int cantidad = gm.Cantidad(puesto);
        Poner(textoCantidad, puesto.sinLimite ? cantidad.ToString() : $"{cantidad} / {gm.Capacidad(puesto)}");
        textoCantidad.color = puesto.color;

        string detalle = $"+{FormatoNumeros.Plata(gm.ProduccionUnitaria(puesto))}/s c/u";
        PuestoData subordinado = gm.SubordinadoDe(puesto);
        if (subordinado != null && subordinado.lugaresPorJefe > 0)
        {
            detalle += $" · +{subordinado.lugaresPorJefe} lugares p/ {subordinado.nombrePlural}";
        }
        Poner(textoDetalle, detalle);

        // --- Sin lugar ---
        if (gm.LugaresLibres(puesto) <= 0)
        {
            string aviso = puesto.jefe != null
                ? $"Contratá un {puesto.jefe.nombre} para sumar {puesto.lugaresPorJefe} lugares."
                : "No hay más lugares.";
            Poner(textoDescripcion, aviso);
            textoDescripcion.color = colorAviso;

            Poner(textoBoton, "Sin lugar");
            botonComprar.interactable = false;
            return;
        }

        // --- Se puede contratar ---
        Poner(textoDescripcion, puesto.descripcion);
        textoDescripcion.color = colorDescripcion;

        int n = lista.CantidadObjetivo(puesto);
        double costo = gm.CostoContratar(puesto, n);
        Poner(textoBoton, $"Contratar x{n}\n<size=120%>{FormatoNumeros.Plata(costo)}</size>");
        botonComprar.interactable = gm.Estado.plata >= costo;
    }

    // Solo reescribe el texto si cambió (TextMeshPro regenera la malla en cada cambio)
    private static void Poner(TMP_Text texto, string valor)
    {
        if (texto != null && texto.text != valor) texto.text = valor;
    }
}
