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
            long porcentaje = (long)estado.acciones * 10;
            Poner(textoAcciones, $"Tenés {estado.acciones} acciones (+{porcentaje}% producción y clic)");
        }

        // --- Bolsa ---
        if (gm.PuedeSalirABolsa)
        {
            int acciones = gm.AccionesAGanar;
            Poner(textoBolsaDetalle,
                "Tu empresa sale a la bolsa y queda en manos de los accionistas. " +
                $"Vos te llevás +{acciones} acciones y empezás una empresa nueva con +10% por acción.");
            botonBolsa.PonerTextoNormal($"Salir a la bolsa y fundar otra empresa (+{acciones} acciones)");
            botonBolsa.PonerInteractuable(true);
        }
        else
        {
            Poner(textoBolsaDetalle,
                $"Facturá {FormatoNumeros.Plata(GameManager.MinimoParaBolsa)} en esta empresa para salir a la bolsa. " +
                $"Llevás {FormatoNumeros.Plata(estado.facturadoPartida)}.");
            botonBolsa.PonerTextoNormal("Salir a la bolsa y fundar otra empresa");
            botonBolsa.PonerInteractuable(false);
        }
    }

    private static void Poner(TMP_Text texto, string valor)
    {
        if (texto != null && texto.text != valor) texto.text = valor;
    }
}
