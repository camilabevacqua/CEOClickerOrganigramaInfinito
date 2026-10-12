using TMPro;
using UnityEngine;

/// <summary>
/// Pestaña Empresa: tarjeta de "Salir a la bolsa" y de "Borrar partida".
/// (La lista de etapas la maneja UIListaEtapas.) Solo lee del GameManager
/// y le pide las acciones cuando se confirma con doble toque.
/// </summary>
public class UIPanelEmpresa : MonoBehaviour
{
    private const float IntervaloRefresco = 0.25f;
    private const float DuracionMensaje = 4f;

    [Header("Salir a la bolsa")]
    [SerializeField] private TMP_Text textoBolsaDetalle;
    [SerializeField] private TMP_Text textoAcciones;
    [SerializeField] private BotonDobleToque botonBolsa;

    [Header("Borrar partida")]
    [SerializeField] private BotonDobleToque botonBorrar;

    private float proximoRefresco;
    private string mensaje;
    private float mensajeHasta;

    private void Start()
    {
        if (botonBolsa != null) botonBolsa.alConfirmar.AddListener(SalirABolsa);
        if (botonBorrar != null) botonBorrar.alConfirmar.AddListener(BorrarPartida);
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

    private void SalirABolsa()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        int ganadas = gm.AccionesAGanar;
        if (gm.SalirABolsa())
        {
            MostrarMensaje($"¡Saliste a la bolsa! Ganaste {ganadas} acciones.");
        }
    }

    private void BorrarPartida()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        gm.BorrarPartida();
        MostrarMensaje("Partida borrada. ¡A empezar de nuevo desde el garaje!");
    }

    private void MostrarMensaje(string texto)
    {
        mensaje = texto;
        mensajeHasta = Time.unscaledTime + DuracionMensaje;
        Refrescar();
    }

    private void Refrescar()
    {
        proximoRefresco = Time.unscaledTime + IntervaloRefresco;

        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        EstadoJuego estado = gm.Estado;

        // --- Texto de acciones (o mensaje temporal) ---
        if (mensaje != null && Time.unscaledTime < mensajeHasta)
        {
            Poner(textoAcciones, mensaje);
        }
        else
        {
            mensaje = null;
            int porcentaje = estado.acciones * 10;
            Poner(textoAcciones, $"Tenés {estado.acciones} acciones (+{porcentaje}% producción y clic)");
        }

        // --- Bolsa ---
        if (gm.PuedeSalirABolsa)
        {
            Poner(textoBolsaDetalle,
                "Reinicia plata, personal y departamentos. Conservás tus acciones, " +
                $"la facturación histórica ({FormatoNumeros.Plata(estado.facturadoHistorico)}) y los tratos.");
            botonBolsa.PonerTextoNormal($"Salir a la bolsa: +{gm.AccionesAGanar} acciones");
            botonBolsa.PonerInteractuable(true);
        }
        else
        {
            Poner(textoBolsaDetalle,
                $"Facturá {FormatoNumeros.Plata(GameManager.MinimoParaBolsa)} en esta partida para salir a la bolsa. " +
                $"Llevás {FormatoNumeros.Plata(estado.facturadoPartida)}.");
            botonBolsa.PonerTextoNormal("Salir a la bolsa");
            botonBolsa.PonerInteractuable(false);
        }
    }

    private static void Poner(TMP_Text texto, string valor)
    {
        if (texto != null && texto.text != valor) texto.text = valor;
    }
}
