using UnityEngine;

/// <summary>
/// Ajusta este RectTransform para que ocupe solo el área segura de la pantalla
/// (evita notch, cámara frontal y barras de gestos). Se recalcula si cambia
/// la resolución, la orientación o el área segura.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private RectTransform panel;
    private Rect ultimaArea = new Rect(0, 0, 0, 0);
    private Vector2Int ultimaResolucion = Vector2Int.zero;
    private ScreenOrientation ultimaOrientacion = ScreenOrientation.AutoRotation;

    private void Awake()
    {
        panel = GetComponent<RectTransform>();
        Aplicar();
    }

    private void Update()
    {
        if (Screen.safeArea != ultimaArea
            || Screen.width != ultimaResolucion.x
            || Screen.height != ultimaResolucion.y
            || Screen.orientation != ultimaOrientacion)
        {
            Aplicar();
        }
    }

    private void Aplicar()
    {
        Rect area = Screen.safeArea;

        ultimaArea = area;
        ultimaResolucion = new Vector2Int(Screen.width, Screen.height);
        ultimaOrientacion = Screen.orientation;

        if (Screen.width <= 0 || Screen.height <= 0) return;

        // Convertimos el rectángulo en píxeles a anclas normalizadas (0 a 1)
        Vector2 anclaMin = area.position;
        Vector2 anclaMax = area.position + area.size;

        anclaMin.x /= Screen.width;
        anclaMin.y /= Screen.height;
        anclaMax.x /= Screen.width;
        anclaMax.y /= Screen.height;

        panel.anchorMin = anclaMin;
        panel.anchorMax = anclaMax;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
    }
}
