using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una fila dentro de la tarjeta de un departamento: un puesto interno
/// (ej: "Capacitadores 1 / 2 · c/u: +5% producción" + botón Contratar).
/// La crea y la refresca UIDepartamentoItem.
/// </summary>
public class UIFilaRol : MonoBehaviour
{
    [SerializeField] private Image insignia;
    [SerializeField] private TMP_Text textoInicial;
    [SerializeField] private TMP_Text textoNombre;
    [SerializeField] private TMP_Text textoDetalle;
    [SerializeField] private Button boton;
    [SerializeField] private TMP_Text textoBoton;

    private static readonly CultureInfo Cultura = CultureInfo.InvariantCulture;
    private readonly List<string> partes = new List<string>();

    private DepartamentoData departamento;
    private RolDepartamento rol;

    public void Configurar(DepartamentoData nuevoDepartamento, RolDepartamento nuevoRol)
    {
        departamento = nuevoDepartamento;
        rol = nuevoRol;

        boton.onClick.RemoveAllListeners();
        boton.onClick.AddListener(Contratar);
    }

    private void Contratar()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && rol != null) gm.ContratarRol(departamento, rol);
    }

    public void Refrescar(GameManager gm, Color colorDescripcion, Color colorAviso)
    {
        if (gm == null || rol == null) return;

        int personal = gm.PersonalRol(rol);
        int capacidad = gm.CapacidadRol(departamento, rol);
        bool desbloqueado = gm.RolDesbloqueado(departamento, rol);

        insignia.color = personal > 0 ? departamento.color : Color.Lerp(departamento.color, Color.white, 0.6f);
        Poner(textoInicial, desbloqueado ? rol.inicial : "?");

        // --- Bloqueado: todavía no se llegó al nivel ---
        if (!desbloqueado)
        {
            Poner(textoNombre, rol.nombrePlural);
            textoNombre.color = colorDescripcion;
            Poner(textoDetalle, $"Se desbloquea con {departamento.sigla} nivel {rol.nivelRequerido}.");
            textoDetalle.color = colorAviso;
            Poner(textoBoton, $"Nv {rol.nivelRequerido}");
            boton.interactable = false;
            return;
        }

        Poner(textoNombre, $"{rol.nombrePlural}: {personal} / {capacidad}");
        textoNombre.color = departamento.color;

        // --- Sin lugar ---
        if (personal >= capacidad)
        {
            Poner(textoDetalle, $"Subí {departamento.sigla} de nivel para sumar {rol.lugaresPorNivel} lugar{(rol.lugaresPorNivel == 1 ? "" : "es")}.");
            textoDetalle.color = colorAviso;
            Poner(textoBoton, "Sin lugar");
            boton.interactable = false;
            return;
        }

        // --- Se puede contratar ---
        Poner(textoDetalle, EfectoPorPersona());
        textoDetalle.color = colorDescripcion;

        double costo = gm.CostoRol(rol);
        Poner(textoBoton, $"Contratar\n<size=115%>{FormatoNumeros.Plata(costo)}</size>");
        boton.interactable = gm.Estado.plata >= costo;
    }

    /// <summary>Ej: "c/u: +5% producción" o "c/u: +$3 clic · +0.2% del $/s al clic".</summary>
    private string EfectoPorPersona()
    {
        partes.Clear();

        if (rol.bonusProduccion > 0)
            partes.Add("+" + (rol.bonusProduccion * 100).ToString("0.#", Cultura) + "% producción");
        if (rol.bonusClic > 0)
            partes.Add("+$" + rol.bonusClic.ToString("0.##", Cultura) + " clic");
        if (rol.porcentajeIngresoAlClic > 0)
            partes.Add("+" + (rol.porcentajeIngresoAlClic * 100).ToString("0.##", Cultura) + "% del $/s al clic");
        if (rol.descuentoContratacion > 0 && rol.descuentoContratacion < 1)
            partes.Add("-" + ((1 - rol.descuentoContratacion) * 100).ToString("0.#", Cultura) + "% costo de contratar");

        return partes.Count == 0 ? "" : "c/u: " + string.Join(" · ", partes);
    }

    private static void Poner(TMP_Text texto, string valor)
    {
        if (texto != null && texto.text != valor) texto.text = valor;
    }
}
