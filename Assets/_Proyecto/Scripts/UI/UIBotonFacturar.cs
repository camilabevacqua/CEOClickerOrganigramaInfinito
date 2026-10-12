using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Botón "Facturar": al tocarlo le pide al GameManager que facture.
/// La "cara" del botón baja mientras está apretado y tapa la sombra (efecto de hundirse).
/// </summary>
[RequireComponent(typeof(Button))]
public class UIBotonFacturar : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("La parte de arriba del botón (la que se mueve al apretar).")]
    [SerializeField] private RectTransform cara;

    [Tooltip("Cuántos píxeles baja la cara al apretar. Igual a la altura visible de la sombra.")]
    [SerializeField] private float hundimiento = 18f;

    private Button boton;
    private Vector2 posicionOriginal;
    private bool hundido;

    /// <summary>Posición en pantalla del último toque (para el "+$X" flotante).</summary>
    public Vector2 UltimaPosicionToque { get; private set; }

    /// <summary>False si todavía no hubo ningún toque (ej: clic con teclado).</summary>
    public bool HuboToque { get; private set; }

    private void Awake()
    {
        boton = GetComponent<Button>();
        if (cara != null) posicionOriginal = cara.anchoredPosition;
        boton.onClick.AddListener(AlTocar);
    }

    private void OnDestroy()
    {
        if (boton != null) boton.onClick.RemoveListener(AlTocar);
    }

    private void OnDisable()
    {
        Hundir(false);
    }

    private void AlTocar()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.Facturar();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        UltimaPosicionToque = eventData.position;
        HuboToque = true;
        Hundir(true);
    }
    public void OnPointerUp(PointerEventData eventData) => Hundir(false);
    public void OnPointerExit(PointerEventData eventData) => Hundir(false);

    private void Hundir(bool valor)
    {
        if (cara == null || hundido == valor) return;
        hundido = valor;
        cara.anchoredPosition = valor ? posicionOriginal + Vector2.down * hundimiento : posicionOriginal;
    }
}
