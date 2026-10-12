using System.IO;
using TMPro;
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

    // Paleta del juego
    private const string ColorFondo = "#E7ECF2";
    private const string ColorPanel = "#FFFFFF";
    private const string ColorTexto = "#13203A";
    private const string ColorPlata = "#0A7458";
    private const string ColorPlataOscuro = "#07513D";
    private const string ColorAcento = "#9A5B00";

    // =================================================================
    // PASO 1: carpetas, escena, cámara, EventSystem, Canvas y área segura
    // =================================================================
    [MenuItem("CEO Clicker/Paso 1 - Configurar escena principal", priority = 1)]
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

    // =================================================================
    // PASO 2: GameManager, texto de plata y botón Facturar
    // =================================================================
    [MenuItem("CEO Clicker/Paso 2 - GameManager y botón Facturar", priority = 2)]
    public static void ConfigurarPaso2()
    {
        if (!TextMeshProListo()) return;

        SafeArea area = Object.FindFirstObjectByType<SafeArea>();
        if (area == null)
        {
            EditorUtility.DisplayDialog("CEO Clicker",
                "No encontré el objeto AreaSegura en la escena abierta.\nAbrí la escena Principal (o corré el Paso 1) y probá de nuevo.",
                "OK");
            return;
        }

        // Ya no necesitamos la imagen roja de prueba
        DestruirHijo(area.transform, "Prueba");

        // GameManager (uno solo)
        if (Object.FindFirstObjectByType<GameManager>() == null)
        {
            GameObject gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();
            gm.transform.SetAsFirstSibling();
        }

        // Si ya existían de una corrida anterior, se recrean
        DestruirHijo(area.transform, "TextoPlata");
        DestruirHijo(area.transform, "BotonFacturar");

        CrearTextoPlata(area.transform);
        GameObject boton = CrearBotonFacturar(area.transform);

        Scene escena = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        Selection.activeGameObject = boton;
        Debug.Log("<color=#0A7458><b>CEO Clicker:</b></color> Paso 2 listo. Dale Play y tocá Facturar.");
    }

    // =================================================================
    // Partes del Paso 1
    // =================================================================
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
        cam.backgroundColor = Hex(ColorFondo);

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
        imgFondo.color = Hex(ColorFondo);
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

    // =================================================================
    // Partes del Paso 2
    // =================================================================
    private static void CrearTextoPlata(Transform padre)
    {
        TextMeshProUGUI texto = CrearTexto("TextoPlata", padre, "$0", 110, Hex(ColorPlata));

        // Arriba de todo, a lo ancho, con 40 px de margen a cada lado
        RectTransform rt = texto.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-80f, 160f);
        rt.anchoredPosition = new Vector2(0f, -80f);

        texto.gameObject.AddComponent<UITextoPlata>();
    }

    private static GameObject CrearBotonFacturar(Transform padre)
    {
        const float hundimiento = 18f;
        Sprite redondeado = SpriteRedondeado();

        // Raíz = sombra (también es la zona que recibe el toque)
        GameObject raiz = new GameObject("BotonFacturar", typeof(RectTransform));
        raiz.layer = LayerMask.NameToLayer("UI");
        RectTransform rtRaiz = raiz.GetComponent<RectTransform>();
        rtRaiz.SetParent(padre, false);
        rtRaiz.anchorMin = rtRaiz.anchorMax = new Vector2(0.5f, 0.5f);
        rtRaiz.pivot = new Vector2(0.5f, 0.5f);
        rtRaiz.sizeDelta = new Vector2(760f, 280f);
        rtRaiz.anchoredPosition = new Vector2(0f, -100f);

        Image sombra = raiz.AddComponent<Image>();
        sombra.sprite = redondeado;
        sombra.type = Image.Type.Sliced;
        sombra.pixelsPerUnitMultiplier = 0.25f; // esquinas más redondas
        sombra.color = Hex(ColorPlataOscuro);

        // Cara: igual de grande, pero levantada "hundimiento" píxeles
        RectTransform rtCara = CrearHijoEstirado("Cara", rtRaiz);
        rtCara.offsetMin = new Vector2(0f, hundimiento);
        rtCara.offsetMax = new Vector2(0f, hundimiento);

        Image cara = rtCara.gameObject.AddComponent<Image>();
        cara.sprite = redondeado;
        cara.type = Image.Type.Sliced;
        cara.pixelsPerUnitMultiplier = 0.25f;
        cara.color = Hex(ColorPlata);
        cara.raycastTarget = false; // el toque lo recibe la raíz

        TextMeshProUGUI texto = CrearTexto("Texto", rtCara, "Facturar", 100, Color.white);
        RectTransform rtTexto = texto.rectTransform;
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.offsetMin = Vector2.zero;
        rtTexto.offsetMax = Vector2.zero;

        // Botón: aclara/oscurece la cara al apretar
        Button boton = raiz.AddComponent<Button>();
        boton.targetGraphic = cara;
        ColorBlock colores = boton.colors;
        colores.normalColor = Color.white;
        colores.highlightedColor = Color.white;
        colores.pressedColor = new Color(0.85f, 0.85f, 0.85f);
        colores.selectedColor = Color.white;
        colores.fadeDuration = 0.05f;
        boton.colors = colores;
        Navigation nav = boton.navigation;
        nav.mode = Navigation.Mode.None;
        boton.navigation = nav;

        // Script del botón, con sus referencias asignadas
        UIBotonFacturar script = raiz.AddComponent<UIBotonFacturar>();
        SerializedObject so = new SerializedObject(script);
        so.FindProperty("cara").objectReferenceValue = rtCara;
        so.FindProperty("hundimiento").floatValue = hundimiento;
        so.ApplyModifiedPropertiesWithoutUndo();

        return raiz;
    }

    // =================================================================
    // Utilidades
    // =================================================================
    private static bool TextMeshProListo()
    {
        if (Resources.Load<TMP_Settings>("TMP Settings") != null) return true;

        EditorUtility.DisplayDialog("CEO Clicker",
            "Falta importar los recursos de TextMeshPro.\n\n" +
            "Andá a Window > TextMeshPro > Import TMP Essential Resources, apretá Import, " +
            "y después volvé a correr este paso.",
            "OK");
        return false;
    }

    private static TextMeshProUGUI CrearTexto(string nombre, Transform padre, string contenido, float tamanio, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.GetComponent<RectTransform>().SetParent(padre, false);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = contenido;
        t.fontSize = tamanio;
        t.color = color;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

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

    private static void DestruirHijo(Transform padre, string nombre)
    {
        Transform hijo = padre.Find(nombre);
        if (hijo != null) Object.DestroyImmediate(hijo.gameObject);
    }

    private static Sprite SpriteRedondeado()
    {
        // Sprite redondeado que viene con Unity (el mismo de los botones por defecto)
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }
}
