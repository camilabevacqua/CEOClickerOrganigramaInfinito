using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Herramientas de editor para armar el proyecto automáticamente.
/// Menú: CEO Clicker > ...
/// (Está en una carpeta "Editor", así que NO se incluye en el build del juego.)
/// </summary>
public static class ConfiguradorProyecto
{
    private const string RaizProyecto = "Assets/_Proyecto";
    private const string RutaEscena = RaizProyecto + "/Escenas/Principal.unity";

    private static readonly string[] Carpetas =
    {
        "Scripts/Core", "Scripts/Data", "Scripts/UI", "Scripts/Utils", "Scripts/Editor",
        "Datos/Puestos", "Datos/Departamentos",
        "Prefabs", "Escenas", "Sprites", "Audio"
    };

    // ---------------------------------------------------------------
    // PASO 1: carpetas, escena, cámara, EventSystem, Canvas y área segura
    // ---------------------------------------------------------------
    [MenuItem("CEO Clicker/Paso 1 - Configurar escena principal")]
    public static void ConfigurarPaso1()
    {
        if (File.Exists(RutaEscena) &&
            !EditorUtility.DisplayDialog("CEO Clicker",
                "La escena Principal ya existe. ¿Querés recrearla desde cero?\n(Se pierde lo que tenga adentro.)",
                "Sí, recrear", "Cancelar"))
        {
            return;
        }

        // Si la escena abierta tiene cambios sin guardar, Unity pregunta antes de cerrarla
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        CrearCarpetas();

        Scene escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CrearCamara();
        CrearEventSystem();
        GameObject canvas = CrearCanvas();

        EditorSceneManager.SaveScene(escena, RutaEscena);

        // Escena en la lista del build (única y habilitada)
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(RutaEscena, true) };

        // Orientación vertical
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject = canvas;
        Debug.Log("<color=#0A7458><b>CEO Clicker:</b></color> Paso 1 listo. Escena guardada en " + RutaEscena);
    }

    // ---------------------------------------------------------------
    // Partes
    // ---------------------------------------------------------------
    private static void CrearCarpetas()
    {
        foreach (string carpeta in Carpetas)
        {
            CrearCarpeta(RaizProyecto + "/" + carpeta);
        }
    }

    private static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;

        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        string nombre = Path.GetFileName(ruta);

        CrearCarpeta(padre);
        AssetDatabase.CreateFolder(padre, nombre);
    }

    private static void CrearCamara()
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.position = new Vector3(0f, 0f, -10f);

        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("#E7ECF2");

        go.AddComponent<AudioListener>();
    }

    private static void CrearEventSystem()
    {
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        InputSystemUIInputModule modulo = go.AddComponent<InputSystemUIInputModule>();
        modulo.AssignDefaultActions(); // acciones de UI del Input System nuevo (clic, toque, etc.)
    }

    private static GameObject CrearCanvas()
    {
        GameObject go = new GameObject("Canvas", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f; // prioriza el ancho

        go.AddComponent<GraphicRaycaster>();

        // Fondo: fuera del área segura, para que pinte también la zona del notch
        RectTransform fondo = CrearHijoEstirado("Fondo", go.transform);
        Image imgFondo = fondo.gameObject.AddComponent<Image>();
        imgFondo.color = Hex("#E7ECF2");
        imgFondo.raycastTarget = false;

        // Contenedor que respeta Screen.safeArea: TODA la interfaz va adentro
        RectTransform areaSegura = CrearHijoEstirado("AreaSegura", go.transform);
        areaSegura.gameObject.AddComponent<SafeArea>();

        // Imagen de prueba (rojo semitransparente) para ver el área segura. Se borra en el Paso 2.
        RectTransform prueba = CrearHijoEstirado("Prueba", areaSegura);
        Image imgPrueba = prueba.gameObject.AddComponent<Image>();
        imgPrueba.color = new Color(1f, 0f, 0f, 0.35f);
        imgPrueba.raycastTarget = false;

        return go;
    }

    // ---------------------------------------------------------------
    // Utilidades
    // ---------------------------------------------------------------
    private static RectTransform CrearHijoEstirado(string nombre, Transform padre)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(padre, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    private static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }
}
