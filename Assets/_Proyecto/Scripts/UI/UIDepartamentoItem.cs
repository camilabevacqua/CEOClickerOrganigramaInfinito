using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tarjeta de un departamento: arriba el nivel (botón Mejorar) y abajo
/// una fila por cada puesto interno (Reclutadores, Capacitadores, etc.).
/// Solo muestra datos del GameManager y le pide las compras.
/// </summary>
public class UIDepartamentoItem : MonoBehaviour
{
    private const float IntervaloRefresco = 0.1f;

    [Header("Departamento")]
    [SerializeField] private Image insignia;
    [SerializeField] private TMP_Text textoSigla;
    [SerializeField] private TMP_Text textoNombre;
    [SerializeField] private TMP_Text textoNivel;
    [SerializeField] private TMP_Text textoDetalle;
    [SerializeField] private TMP_Text textoDescripcion;
    [SerializeField] private Button botonComprar;
    [SerializeField] private TMP_Text textoBoton;

    [Header("Puestos internos")]
    [Tooltip("Fila de ejemplo (inactiva) que se copia una vez por puesto interno.")]
    [SerializeField] private UIFilaRol plantillaFila;
    [Tooltip("Alto de la parte de arriba (nivel del departamento).")]
    [SerializeField] private float altoCabecera = 236f;
    [SerializeField] private float altoFila = 116f;

    [Header("Colores")]
    [SerializeField] private Color colorDescripcion = new Color32(0x9C, 0x7A, 0x93, 0xFF);
    [SerializeField] private Color colorAviso = new Color32(0xE0, 0x6C, 0x8C, 0xFF);

    private static readonly CultureInfo Cultura = CultureInfo.InvariantCulture;
    private readonly List<string> partes = new List<string>();
    private readonly List<UIFilaRol> filas = new List<UIFilaRol>();

    private DepartamentoData departamento;
    private float proximoRefresco;
    private bool suscripto;

    public void Configurar(DepartamentoData nuevo)
    {
        departamento = nuevo;

        botonComprar.onClick.RemoveAllListeners();
        botonComprar.onClick.AddListener(AlMejorar);

        CrearFilas();
        Suscribir();
        Refrescar();
    }

    /// <summary>Una fila por puesto interno, debajo de la parte de arriba. La tarjeta crece según la cantidad.</summary>
    private void CrearFilas()
    {
        foreach (UIFilaRol f in filas) if (f != null) Destroy(f.gameObject);
        filas.Clear();

        if (plantillaFila == null || GameManager.Instance == null) return;
        plantillaFila.gameObject.SetActive(false);

        IReadOnlyList<RolDepartamento> roles = GameManager.Instance.RolesDe(departamento);
        for (int i = 0; i < roles.Count; i++)
        {
            UIFilaRol fila = Instantiate(plantillaFila, transform);
            fila.name = "Fila_" + roles[i].id;
            fila.gameObject.SetActive(true);

            // Pegada arriba, debajo de la anterior
            RectTransform r = (RectTransform)fila.transform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            float desdeArriba = altoCabecera + i * altoFila;
            r.offsetMin = new Vector2(0f, -desdeArriba - altoFila);
            r.offsetMax = new Vector2(0f, -desdeArriba);

            fila.Configurar(departamento, roles[i]);
            filas.Add(fila);
        }

        // Alto total de la tarjeta para la lista
        float alto = altoCabecera + roles.Count * altoFila + 12f;
        LayoutElement le = GetComponent<LayoutElement>();
        if (le != null)
        {
            le.minHeight = alto;
            le.preferredHeight = alto;
        }
        ((RectTransform)transform).sizeDelta = new Vector2(((RectTransform)transform).sizeDelta.x, alto);
    }

    private void OnEnable() => Suscribir();
    private void OnDisable() => Desuscribir();

    private void Suscribir()
    {
        if (suscripto || departamento == null || GameManager.Instance == null) return;
        GameManager.Instance.OnDepartamentoMejorado += AlCambiar;
        GameManager.Instance.OnPersonalDepartamentoContratado += AlContratar;
        suscripto = true;
    }

    private void Desuscribir()
    {
        if (!suscripto) return;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDepartamentoMejorado -= AlCambiar;
            GameManager.Instance.OnPersonalDepartamentoContratado -= AlContratar;
        }
        suscripto = false;
    }

    private void AlCambiar(DepartamentoData d) => Refrescar();
    private void AlContratar(DepartamentoData d, RolDepartamento r) => Refrescar();

    private void Update()
    {
        if (Time.unscaledTime >= proximoRefresco) Refrescar();
    }

    private void AlMejorar()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && departamento != null) gm.MejorarDepartamento(departamento);
    }

    public void Refrescar()
    {
        proximoRefresco = Time.unscaledTime + IntervaloRefresco;

        GameManager gm = GameManager.Instance;
        if (gm == null || departamento == null) return;

        RefrescarDepartamento(gm);
        foreach (UIFilaRol fila in filas) fila.Refrescar(gm, colorDescripcion, colorAviso);
    }

    // ------------------------------------------------------------------
    // Parte de arriba: nivel del departamento
    // ------------------------------------------------------------------
    private void RefrescarDepartamento(GameManager gm)
    {
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

    /// <summary>Resumen del efecto actual del departamento (nivel + todo su personal).</summary>
    private string ArmarDetalle(GameManager gm, int nivel)
    {
        partes.Clear();

        double bonusProduccion = departamento.bonusProduccionPorNivel * nivel;
        bool usaClic = departamento.bonusClicPorNivel > 0 || departamento.porcentajeIngresoAlClicPorNivel > 0;
        double descuento = departamento.descuentoContratacionPorNivel > 0 && departamento.descuentoContratacionPorNivel < 1
            ? Math.Pow(departamento.descuentoContratacionPorNivel, nivel)
            : 1.0;
        bool usaDescuento = descuento < 1 || (departamento.descuentoContratacionPorNivel > 0 && departamento.descuentoContratacionPorNivel < 1);

        foreach (RolDepartamento rol in gm.RolesDe(departamento))
        {
            int personal = gm.PersonalRol(rol);
            bonusProduccion += rol.bonusProduccion * personal;
            if (rol.bonusClic > 0 || rol.porcentajeIngresoAlClic > 0) usaClic = true;
            if (rol.descuentoContratacion > 0 && rol.descuentoContratacion < 1)
            {
                descuento *= Math.Pow(rol.descuentoContratacion, personal);
                usaDescuento = true;
            }
        }

        bool usaProduccion = departamento.bonusProduccionPorNivel > 0 || bonusProduccion > 0;
        if (usaProduccion) partes.Add("Producción ×" + (1.0 + bonusProduccion).ToString("0.##", Cultura));
        if (usaClic) partes.Add("Clic " + FormatoNumeros.Plata(gm.ValorPorClic));
        if (usaDescuento) partes.Add("Contratar ×" + descuento.ToString("0.00", Cultura));

        return partes.Count == 0 ? "" : "Ahora: " + string.Join(" · ", partes);
    }

    private static void Poner(TMP_Text texto, string valor)
    {
        if (texto != null && texto.text != valor) texto.text = valor;
    }
}
