using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una fila de la lista de Departamentos. Solo muestra datos del GameManager
/// y, al tocar el botón, le pide que mejore el departamento.
/// </summary>
public class UIDepartamentoItem : MonoBehaviour
{
    private const float IntervaloRefresco = 0.1f;

    [Header("Referencias")]
    [SerializeField] private Image insignia;
    [SerializeField] private TMP_Text textoSigla;
    [SerializeField] private TMP_Text textoNombre;
    [SerializeField] private TMP_Text textoNivel;
    [SerializeField] private TMP_Text textoDetalle;
    [SerializeField] private TMP_Text textoDescripcion;
    [SerializeField] private Button botonComprar;
    [SerializeField] private TMP_Text textoBoton;

    private static readonly CultureInfo Cultura = CultureInfo.InvariantCulture;
    private readonly List<string> partes = new List<string>();

    private DepartamentoData departamento;
    private float proximoRefresco;
    private bool suscripto;

    public void Configurar(DepartamentoData nuevo)
    {
        departamento = nuevo;

        botonComprar.onClick.RemoveAllListeners();
        botonComprar.onClick.AddListener(AlComprar);

        Suscribir();
        Refrescar();
    }

    private void OnEnable() => Suscribir();
    private void OnDisable() => Desuscribir();

    private void Suscribir()
    {
        if (suscripto || departamento == null || GameManager.Instance == null) return;
        GameManager.Instance.OnDepartamentoMejorado += AlMejorar;
        suscripto = true;
    }

    private void Desuscribir()
    {
        if (!suscripto) return;
        if (GameManager.Instance != null) GameManager.Instance.OnDepartamentoMejorado -= AlMejorar;
        suscripto = false;
    }

    private void AlMejorar(DepartamentoData d) => Refrescar();

    private void Update()
    {
        if (Time.unscaledTime >= proximoRefresco) Refrescar();
    }

    private void AlComprar()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && departamento != null) gm.MejorarDepartamento(departamento);
    }

    public void Refrescar()
    {
        proximoRefresco = Time.unscaledTime + IntervaloRefresco;

        GameManager gm = GameManager.Instance;
        if (gm == null || departamento == null) return;

        int nivel = gm.Nivel(departamento);

        insignia.color = nivel > 0 ? departamento.color : Color.Lerp(departamento.color, Color.white, 0.55f);
        Poner(textoSigla, departamento.sigla);
        Poner(textoNombre, departamento.nombre);
        Poner(textoNivel, departamento.nivelMaximo > 0 ? $"Nv. {nivel} / {departamento.nivelMaximo}" : $"Nv. {nivel}");
        textoNivel.color = departamento.color;
        Poner(textoDescripcion, departamento.descripcion);
        Poner(textoDetalle, ArmarDetalle(gm, nivel));

        if (gm.EstaAlMaximo(departamento))
        {
            Poner(textoBoton, "Máximo");
            botonComprar.interactable = false;
            return;
        }

        double costo = gm.CostoDepartamento(departamento);
        Poner(textoBoton, $"Mejorar\n<size=120%>{FormatoNumeros.Plata(costo)}</size>");
        botonComprar.interactable = gm.Estado.plata >= costo;
    }

    /// <summary>Resumen del efecto actual, armado según los efectos que use el departamento.</summary>
    private string ArmarDetalle(GameManager gm, int nivel)
    {
        partes.Clear();

        if (departamento.bonusProduccionPorNivel > 0)
        {
            double mult = 1.0 + departamento.bonusProduccionPorNivel * nivel;
            partes.Add("Producción ×" + mult.ToString("0.##", Cultura));
        }

        if (departamento.bonusClicPorNivel > 0 || departamento.porcentajeIngresoAlClicPorNivel > 0)
        {
            partes.Add("Clic " + FormatoNumeros.Plata(gm.ValorPorClic));
        }

        if (departamento.descuentoContratacionPorNivel > 0 && departamento.descuentoContratacionPorNivel < 1)
        {
            double mult = Math.Pow(departamento.descuentoContratacionPorNivel, nivel);
            partes.Add("Contratar ×" + mult.ToString("0.00", Cultura));
        }

        return partes.Count == 0 ? "" : "Ahora: " + string.Join(" · ", partes);
    }

    private static void Poner(TMP_Text texto, string valor)
    {
        if (texto != null && texto.text != valor) texto.text = valor;
    }
}
