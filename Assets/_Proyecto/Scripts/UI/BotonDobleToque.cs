using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Botón con confirmación por doble toque: el primer toque lo "arma"
/// (cambia texto y color) y el segundo, dentro del tiempo límite, confirma.
/// Si pasa el tiempo, vuelve solo a la normalidad.
/// </summary>
[RequireComponent(typeof(Button))]
public class BotonDobleToque : MonoBehaviour
{
    [SerializeField] private TMP_Text texto;
    [SerializeField] private Image fondo;
    [SerializeField] private string textoConfirmar = "¿Seguro? Tocá de nuevo";
    [SerializeField] private Color colorConfirmar = new Color32(0xE0, 0x6C, 0x8C, 0xFF);
    [SerializeField] private float tiempoLimite = 3f;

    /// <summary>Se dispara en el segundo toque (confirmación).</summary>
    public UnityEvent alConfirmar = new UnityEvent();

    private Button boton;
    private string textoNormal = "";
    private Color colorNormal = Color.white;
    private float armadoHasta = -1f;
    private bool inicializado;

    public bool Armado => armadoHasta > 0f;

    private void Awake()
    {
        Inicializar();
    }

    private void Inicializar()
    {
        if (inicializado) return;
        inicializado = true;

        boton = GetComponent<Button>();
        if (texto != null) textoNormal = texto.text;
        if (fondo != null) colorNormal = fondo.color;
        boton.onClick.AddListener(Tocar);
    }

    /// <summary>Cambia el texto que se ve cuando no está armado.</summary>
    public void PonerTextoNormal(string valor)
    {
        Inicializar();
        textoNormal = valor;
        if (!Armado && texto != null && texto.text != valor) texto.text = valor;
    }

    public void PonerInteractuable(bool valor)
    {
        Inicializar();
        if (boton.interactable == valor) return;
        boton.interactable = valor;
        if (!valor) Desarmar();
    }

    private void Tocar()
    {
        if (Armado)
        {
            Desarmar();
            alConfirmar.Invoke();
            return;
        }

        armadoHasta = Time.unscaledTime + tiempoLimite;
        if (texto != null) texto.text = textoConfirmar;
        if (fondo != null) fondo.color = colorConfirmar;
    }

    private void Update()
    {
        if (Armado && Time.unscaledTime > armadoHasta) Desarmar();
    }

    private void OnDisable()
    {
        if (inicializado) Desarmar();
    }

    private void Desarmar()
    {
        armadoHasta = -1f;
        if (texto != null) texto.text = textoNormal;
        if (fondo != null) fondo.color = colorNormal;
    }
}
