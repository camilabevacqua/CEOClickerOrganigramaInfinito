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
    private const string CarpetaPuestos = RaizProyecto + "/Datos/Puestos";
    private const string RutaPrefabPuesto = RaizProyecto + "/Prefabs/ItemPuesto.prefab";
    private const string CarpetaDepartamentos = RaizProyecto + "/Datos/Departamentos";
    private const string RutaPrefabDepartamento = RaizProyecto + "/Prefabs/ItemDepartamento.prefab";
    private const string RutaEtapas = RaizProyecto + "/Datos/Etapas.asset";
    private const string RutaPrefabEtapa = RaizProyecto + "/Prefabs/ItemEtapa.prefab";

    private static readonly string[] Carpetas =
    {
        "Scripts/Core", "Scripts/Data", "Scripts/UI", "Scripts/Utils", "Scripts/Editor",
        "Datos/Puestos", "Datos/Departamentos",
        "Prefabs", "Escenas", "Sprites", "Audio"
    };

    // Paleta del juego
    private const string ColorFondo = "#E7ECF2";
    private const string ColorPanel = "#FFFFFF";
    private const string ColorTarjeta = "#F3F5F8";
    private const string ColorTexto = "#13203A";
    private const string ColorTextoSuave = "#5A6478";
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

        SafeArea area = BuscarAreaSegura();
        if (area == null) return;

        // Ya no necesitamos la imagen roja de prueba
        DestruirDescendiente(area.transform, "Prueba");

        // GameManager (uno solo)
        if (Object.FindFirstObjectByType<GameManager>() == null)
        {
            GameObject gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();
            gm.transform.SetAsFirstSibling();
        }

        // Si ya existían de una corrida anterior, se recrean
        DestruirDescendiente(area.transform, "TextoPlata");
        DestruirDescendiente(area.transform, "BotonFacturar");

        CrearTextoPlata(area.transform);
        GameObject boton = CrearBotonFacturar(area.transform);

        GuardarEscenaActiva();

        Selection.activeGameObject = boton;
        Debug.Log("<color=#0A7458><b>CEO Clicker:</b></color> Paso 2 listo. Dale Play y tocá Facturar.");
    }

    // =================================================================
    // PASO 3: puestos (ScriptableObjects), layout y lista de Personal
    // =================================================================
    [MenuItem("CEO Clicker/Paso 3 - Puestos y lista de Personal", priority = 3)]
    public static void ConfigurarPaso3()
    {
        if (!TextMeshProListo()) return;

        SafeArea area = BuscarAreaSegura();
        if (area == null) return;

        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        Transform boton = BuscarDescendiente(area.transform, "BotonFacturar");
        if (gm == null || boton == null)
        {
            EditorUtility.DisplayDialog("CEO Clicker", "Primero corré el Paso 2 (falta el GameManager o el botón Facturar).", "OK");
            return;
        }

        CrearCarpetas();

        // 1) Datos de los puestos
        PuestoData[] puestos = CrearPuestos();

        SerializedObject soGm = new SerializedObject(gm);
        SerializedProperty lista = soGm.FindProperty("puestos");
        lista.arraySize = puestos.Length;
        for (int i = 0; i < puestos.Length; i++)
        {
            lista.GetArrayElementAtIndex(i).objectReferenceValue = puestos[i];
        }
        soGm.ApplyModifiedPropertiesWithoutUndo();

        // 2) Layout general de la pantalla
        RectTransform zonaFacturar = BuscarOCrearHijo("ZonaFacturar", area.transform);
        Ubicar(zonaFacturar, new Vector2(0f, 0.70f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -240f));

        RectTransform rtBoton = (RectTransform)boton;
        rtBoton.SetParent(zonaFacturar, false);
        rtBoton.anchorMin = rtBoton.anchorMax = new Vector2(0.5f, 0.5f);
        rtBoton.pivot = new Vector2(0.5f, 0.5f);
        rtBoton.sizeDelta = new Vector2(720f, 230f);
        rtBoton.anchoredPosition = Vector2.zero;

        DestruirDescendiente(area.transform, "PanelOrganigrama");
        CrearPanelOrganigramaProvisorio(area.transform);

        DestruirDescendiente(area.transform, "PanelPestanas");
        UIPuestoItem prefab = CrearPrefabItemPuesto();
        CrearPanelPestanas(area.transform, prefab);

        GuardarEscenaActiva();
        AssetDatabase.SaveAssets();

        Debug.Log("<color=#0A7458><b>CEO Clicker:</b></color> Paso 3 listo. Puestos en " + CarpetaPuestos +
                  ". Dale Play, facturá $15 y contratá un Empleado.");
    }

    // =================================================================
    // PASO 4: barra superior, estadísticas y ajuste del botón
    // =================================================================
    [MenuItem("CEO Clicker/Paso 4 - Barra superior y estadísticas", priority = 4)]
    public static void ConfigurarPaso4()
    {
        if (!TextMeshProListo()) return;

        SafeArea area = BuscarAreaSegura();
        if (area == null) return;

        Transform zona = BuscarDescendiente(area.transform, "ZonaFacturar");
        Transform boton = BuscarDescendiente(area.transform, "BotonFacturar");
        UIListaPersonal listaPersonal = Object.FindFirstObjectByType<UIListaPersonal>();
        if (zona == null || boton == null || listaPersonal == null)
        {
            EditorUtility.DisplayDialog("CEO Clicker", "Primero corré el Paso 3 (falta la zona del botón o la lista de Personal).", "OK");
            return;
        }

        // El texto de plata suelto del Paso 2 se reemplaza por la barra superior
        DestruirDescendiente(area.transform, "TextoPlata");
        DestruirDescendiente(area.transform, "BarraSuperior");
        CrearBarraSuperior(area.transform);

        // Zona del botón: debajo de la barra
        RectTransform rtZona = (RectTransform)zona;
        Ubicar(rtZona, new Vector2(0f, 0.70f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -262f));

        // Botón pegado arriba de la zona, más bajo para dejar lugar a las estadísticas
        RectTransform rtBoton = (RectTransform)boton;
        rtBoton.anchorMin = rtBoton.anchorMax = new Vector2(0.5f, 1f);
        rtBoton.pivot = new Vector2(0.5f, 1f);
        rtBoton.sizeDelta = new Vector2(680f, 200f);
        rtBoton.anchoredPosition = new Vector2(0f, -16f);

        DestruirDescendiente(rtZona, "Estadisticas");
        CrearEstadisticas(rtZona);

        // Prefab de la fila de puesto actualizado (texto de detalle con tamaño automático)
        UIPuestoItem prefab = CrearPrefabItemPuesto();
        SerializedObject soLista = new SerializedObject(listaPersonal);
        soLista.FindProperty("prefabItem").objectReferenceValue = prefab;
        soLista.ApplyModifiedPropertiesWithoutUndo();

        GuardarEscenaActiva();
        AssetDatabase.SaveAssets();

        Debug.Log("<color=#0A7458><b>CEO Clicker:</b></color> Paso 4 listo. Contratá Empleados y mirá cómo sube la plata sola.");
    }

    // =================================================================
    // PASO 5: departamentos y pestañas
    // =================================================================
    [MenuItem("CEO Clicker/Paso 5 - Departamentos y pestañas", priority = 5)]
    public static void ConfigurarPaso5()
    {
        if (!TextMeshProListo()) return;

        SafeArea area = BuscarAreaSegura();
        if (area == null) return;

        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        Transform panelPestanas = BuscarDescendiente(area.transform, "PanelPestanas");
        Transform panelPersonal = panelPestanas != null ? BuscarDescendiente(panelPestanas, "PanelPersonal") : null;
        if (gm == null || panelPersonal == null)
        {
            EditorUtility.DisplayDialog("CEO Clicker", "Primero corré los Pasos 2, 3 y 4 (falta el GameManager o el panel de Personal).", "OK");
            return;
        }

        CrearCarpetas();

        // 1) Datos de los departamentos
        DepartamentoData[] departamentos = CrearDepartamentos();

        SerializedObject soGm = new SerializedObject(gm);
        SerializedProperty lista = soGm.FindProperty("departamentos");
        lista.arraySize = departamentos.Length;
        for (int i = 0; i < departamentos.Length; i++)
        {
            lista.GetArrayElementAtIndex(i).objectReferenceValue = departamentos[i];
        }
        soGm.ApplyModifiedPropertiesWithoutUndo();

        // 2) Pestañas
        DestruirDescendiente(panelPestanas, "BarraPestanas");
        DestruirDescendiente(panelPestanas, "PanelDepartamentos");
        DestruirDescendiente(panelPestanas, "PanelEmpresa");

        // Personal baja para dejar lugar a la barra de pestañas
        Ubicar((RectTransform)panelPersonal, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -100f));

        RectTransform barra = CrearRect("BarraPestanas", panelPestanas);
        Ubicar(barra, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -100f), new Vector2(-16f, -16f));
        HorizontalLayoutGroup hl = barra.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 10f;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = true;
        hl.childForceExpandHeight = true;

        Button tabPersonal = CrearBotonChico("TabPersonal", barra, "Personal");
        Button tabDepartamentos = CrearBotonChico("TabDepartamentos", barra, "Departamentos");
        Button tabEmpresa = CrearBotonChico("TabEmpresa", barra, "Empresa");

        // Panel Departamentos
        RectTransform panelDeptos = CrearRect("PanelDepartamentos", panelPestanas);
        Ubicar(panelDeptos, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -100f));
        CrearEncabezado(panelDeptos, "Departamentos");
        RectTransform contenidoDeptos = CrearListaConScroll(panelDeptos, 110f);

        UIDepartamentoItem prefabDepto = CrearPrefabItemDepartamento();
        UIListaDepartamentos listaDeptos = panelDeptos.gameObject.AddComponent<UIListaDepartamentos>();
        SerializedObject soLista = new SerializedObject(listaDeptos);
        soLista.FindProperty("prefabItem").objectReferenceValue = prefabDepto;
        soLista.FindProperty("contenedor").objectReferenceValue = contenidoDeptos;
        soLista.ApplyModifiedPropertiesWithoutUndo();

        // Panel Empresa (provisorio hasta los Pasos 6 y 10)
        RectTransform panelEmpresa = CrearRect("PanelEmpresa", panelPestanas);
        Ubicar(panelEmpresa, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -100f));
        TextMeshProUGUI provisorio = CrearTexto("TextoProvisorio", panelEmpresa,
            "Etapas, salir a la bolsa y borrar partida\n(Pasos 6 y 10)", 36, Hex(ColorTextoSuave));
        provisorio.fontStyle = FontStyles.Normal;
        Estirar(provisorio.rectTransform);

        // Script de pestañas
        UIPestanas viejo = panelPestanas.GetComponent<UIPestanas>();
        if (viejo != null) Object.DestroyImmediate(viejo);
        UIPestanas pestanas = panelPestanas.gameObject.AddComponent<UIPestanas>();

        SerializedObject soPestanas = new SerializedObject(pestanas);
        SerializedProperty arreglo = soPestanas.FindProperty("pestanas");
        Button[] botones = { tabPersonal, tabDepartamentos, tabEmpresa };
        GameObject[] paneles = { panelPersonal.gameObject, panelDeptos.gameObject, panelEmpresa.gameObject };
        arreglo.arraySize = botones.Length;
        for (int i = 0; i < botones.Length; i++)
        {
            SerializedProperty elemento = arreglo.GetArrayElementAtIndex(i);
            elemento.FindPropertyRelative("boton").objectReferenceValue = botones[i];
            elemento.FindPropertyRelative("panel").objectReferenceValue = paneles[i];
        }
        soPestanas.ApplyModifiedPropertiesWithoutUndo();

        GuardarEscenaActiva();
        AssetDatabase.SaveAssets();

        Debug.Log("<color=#0A7458><b>CEO Clicker:</b></color> Paso 5 listo. Departamentos en " + CarpetaDepartamentos + ".");
    }

    // =================================================================
    // PASO 6: etapas, barra de progreso y lista en la pestaña Empresa
    // =================================================================
    [MenuItem("CEO Clicker/Paso 6 - Etapas y progreso", priority = 6)]
    public static void ConfigurarPaso6()
    {
        if (!TextMeshProListo()) return;

        SafeArea area = BuscarAreaSegura();
        if (area == null) return;

        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        UIBarraSuperior barra = Object.FindFirstObjectByType<UIBarraSuperior>();
        Transform panelEmpresa = BuscarDescendiente(area.transform, "PanelEmpresa");
        if (gm == null || barra == null || panelEmpresa == null)
        {
            EditorUtility.DisplayDialog("CEO Clicker", "Primero corré los Pasos 4 y 5 (falta la barra superior o el panel Empresa).", "OK");
            return;
        }

        CrearCarpetas();

        // 1) Datos de las etapas
        EtapasConfig etapas = CrearOObtenerEtapas();
        SerializedObject soGm = new SerializedObject(gm);
        soGm.FindProperty("configEtapas").objectReferenceValue = etapas;
        soGm.ApplyModifiedPropertiesWithoutUndo();

        // 2) Barra superior: conectar etapa, empresa y relleno de progreso
        SerializedObject soBarra = new SerializedObject(barra);
        soBarra.FindProperty("textoEtapa").objectReferenceValue = BuscarComponente<TMP_Text>(barra.transform, "TextoEtapa");
        soBarra.FindProperty("textoEmpresa").objectReferenceValue = BuscarComponente<TMP_Text>(barra.transform, "TextoEmpresa");
        soBarra.FindProperty("rellenoProgreso").objectReferenceValue = BuscarDescendiente(barra.transform, "Relleno");
        soBarra.ApplyModifiedPropertiesWithoutUndo();

        // 3) Pestaña Empresa: se vacía y se arma de nuevo (el objeto PanelEmpresa se conserva para las pestañas)
        for (int i = panelEmpresa.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(panelEmpresa.GetChild(i).gameObject);
        }
        UIListaEtapas viejo = panelEmpresa.GetComponent<UIListaEtapas>();
        if (viejo != null) Object.DestroyImmediate(viejo);

        CrearEncabezado(panelEmpresa, "Empresa");
        RectTransform contenido = CrearListaConScroll(panelEmpresa, 110f);

        TextMeshProUGUI tituloEtapas = CrearTexto("TituloEtapas", contenido, "Etapas", 36, Hex(ColorTexto));
        tituloEtapas.alignment = TextAlignmentOptions.MidlineLeft;
        tituloEtapas.margin = new Vector4(8f, 0f, 0f, 0f);
        tituloEtapas.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;

        RectTransform seccion = CrearRect("SeccionEtapas", contenido);
        VerticalLayoutGroup vl = seccion.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 10f;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        UIEtapaItem prefab = CrearPrefabItemEtapa();
        UIListaEtapas lista = panelEmpresa.gameObject.AddComponent<UIListaEtapas>();
        SerializedObject soLista = new SerializedObject(lista);
        soLista.FindProperty("prefabItem").objectReferenceValue = prefab;
        soLista.FindProperty("contenedor").objectReferenceValue = seccion;
        soLista.ApplyModifiedPropertiesWithoutUndo();

        GuardarEscenaActiva();
        AssetDatabase.SaveAssets();

        Debug.Log("<color=#0A7458><b>CEO Clicker:</b></color> Paso 6 listo. Etapas en " + RutaEtapas + ".");
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

        // Raíz = sombra (también es la zona que recibe el toque)
        RectTransform rtRaiz = CrearRect("BotonFacturar", padre);
        rtRaiz.anchorMin = rtRaiz.anchorMax = new Vector2(0.5f, 0.5f);
        rtRaiz.pivot = new Vector2(0.5f, 0.5f);
        rtRaiz.sizeDelta = new Vector2(760f, 280f);
        rtRaiz.anchoredPosition = new Vector2(0f, -100f);

        Image sombra = AgregarImagenRedondeada(rtRaiz.gameObject, Hex(ColorPlataOscuro), 0.25f);

        // Cara: igual de grande, pero levantada "hundimiento" píxeles
        RectTransform rtCara = CrearHijoEstirado("Cara", rtRaiz);
        rtCara.offsetMin = new Vector2(0f, hundimiento);
        rtCara.offsetMax = new Vector2(0f, hundimiento);

        Image cara = AgregarImagenRedondeada(rtCara.gameObject, Hex(ColorPlata), 0.25f);
        cara.raycastTarget = false; // el toque lo recibe la raíz

        TextMeshProUGUI texto = CrearTexto("Texto", rtCara, "Facturar", 100, Color.white);
        Estirar(texto.rectTransform);

        Button boton = rtRaiz.gameObject.AddComponent<Button>();
        ConfigurarBoton(boton, cara);

        // Script del botón, con sus referencias asignadas
        UIBotonFacturar script = rtRaiz.gameObject.AddComponent<UIBotonFacturar>();
        SerializedObject so = new SerializedObject(script);
        so.FindProperty("cara").objectReferenceValue = rtCara;
        so.FindProperty("hundimiento").floatValue = hundimiento;
        so.ApplyModifiedPropertiesWithoutUndo();

        return rtRaiz.gameObject;
    }

    // =================================================================
    // Partes del Paso 3
    // =================================================================
    private static PuestoData[] CrearPuestos()
    {
        PuestoData empleado = CrearOObtenerPuesto(1, "empleado", "Empleado", "Empleados", "E", "#64748B",
            "Hace el trabajo de verdad. Mate en mano, factura de a poquito.",
            15, 0.4, 3, false, out bool nuevoE);

        PuestoData supervisor = CrearOObtenerPuesto(2, "supervisor", "Supervisor", "Supervisores", "S", "#2C6A9A",
            "Controla que nadie se cuelgue con el celular.",
            120, 3, 2, false, out bool nuevoS);

        PuestoData gerente = CrearOObtenerPuesto(3, "gerente", "Gerente", "Gerentes", "G", "#0A7458",
            "Arma reuniones que podrían haber sido un mail.",
            1300, 20, 2, false, out bool nuevoG);

        PuestoData director = CrearOObtenerPuesto(4, "director", "Director", "Directores", "D", "#9A5B00",
            "Define la estrategia desde un sillón de cuero.",
            15000, 120, 2, false, out bool nuevoD);

        PuestoData vice = CrearOObtenerPuesto(5, "vicepresidente", "Vicepresidente", "Vicepresidentes", "VP", "#6D3FC4",
            "Viaja en business y cierra tratos enormes.",
            180000, 800, 0, true, out bool nuevoVP);

        // Jefes: solo en los recién creados (para no pisar lo que balanceaste a mano)
        if (nuevoE) { empleado.jefe = supervisor; empleado.lugaresPorJefe = 5; }
        if (nuevoS) { supervisor.jefe = gerente; supervisor.lugaresPorJefe = 4; }
        if (nuevoG) { gerente.jefe = director; gerente.lugaresPorJefe = 3; }
        if (nuevoD) { director.jefe = vice; director.lugaresPorJefe = 3; }
        if (nuevoVP) { vice.jefe = null; vice.lugaresPorJefe = 0; }

        PuestoData[] todos = { empleado, supervisor, gerente, director, vice };
        foreach (PuestoData p in todos) EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return todos;
    }

    private static PuestoData CrearOObtenerPuesto(int orden, string id, string nombre, string plural, string inicial,
        string colorHex, string descripcion, double costo, double produccion, int lugaresBase, bool sinLimite,
        out bool nuevo)
    {
        string ruta = $"{CarpetaPuestos}/{orden}_{nombre}.asset";
        PuestoData existente = AssetDatabase.LoadAssetAtPath<PuestoData>(ruta);
        if (existente != null)
        {
            nuevo = false;
            return existente;
        }

        PuestoData p = ScriptableObject.CreateInstance<PuestoData>();
        p.id = id;
        p.nombre = nombre;
        p.nombrePlural = plural;
        p.inicial = inicial;
        p.color = Hex(colorHex);
        p.descripcion = descripcion;
        p.costoBase = costo;
        p.produccionPorSegundo = produccion;
        p.lugaresBase = lugaresBase;
        p.sinLimite = sinLimite;

        AssetDatabase.CreateAsset(p, ruta);
        nuevo = true;
        return p;
    }

    private static void CrearPanelOrganigramaProvisorio(Transform padre)
    {
        RectTransform panel = CrearRect("PanelOrganigrama", padre);
        Ubicar(panel, new Vector2(0f, 0.46f), new Vector2(1f, 0.70f), new Vector2(24f, 12f), new Vector2(-24f, -12f));
        AgregarImagenRedondeada(panel.gameObject, Hex(ColorPanel), 0.4f).raycastTarget = false;

        TextMeshProUGUI texto = CrearTexto("TextoProvisorio", panel, "Organigrama (Paso 7)", 40, Hex(ColorTextoSuave));
        texto.fontStyle = FontStyles.Normal;
        Estirar(texto.rectTransform);
    }

    private static void CrearPanelPestanas(Transform padre, UIPuestoItem prefab)
    {
        RectTransform panel = CrearRect("PanelPestanas", padre);
        Ubicar(panel, new Vector2(0f, 0f), new Vector2(1f, 0.46f), new Vector2(24f, 24f), new Vector2(-24f, -12f));
        AgregarImagenRedondeada(panel.gameObject, Hex(ColorPanel), 0.4f).raycastTarget = false;

        // --- Panel Personal ---
        RectTransform personal = CrearHijoEstirado("PanelPersonal", panel);

        // Encabezado: título + selector x1/x10/Máx
        RectTransform encabezado = CrearRect("Encabezado", personal);
        Ubicar(encabezado, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -110f), new Vector2(0f, 0f));

        TextMeshProUGUI titulo = CrearTexto("Titulo", encabezado, "Personal", 46, Hex(ColorTexto));
        titulo.alignment = TextAlignmentOptions.MidlineLeft;
        Ubicar(titulo.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(32f, 0f), new Vector2(0f, 0f));

        RectTransform selector = CrearRect("Selector", encabezado);
        selector.anchorMin = selector.anchorMax = new Vector2(1f, 0.5f);
        selector.pivot = new Vector2(1f, 0.5f);
        selector.sizeDelta = new Vector2(450f, 80f);
        selector.anchoredPosition = new Vector2(-24f, 0f);
        HorizontalLayoutGroup hl = selector.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 12f;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = true;
        hl.childForceExpandHeight = true;

        Button bX1 = CrearBotonChico("BotonX1", selector, "x1");
        Button bX10 = CrearBotonChico("BotonX10", selector, "x10");
        Button bMax = CrearBotonChico("BotonMax", selector, "Máx");

        // Lista con scroll
        RectTransform scroll = CrearRect("Scroll", personal);
        Ubicar(scroll, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -110f));
        Image zonaToque = scroll.gameObject.AddComponent<Image>();
        zonaToque.color = new Color(1f, 1f, 1f, 0f); // invisible, pero recibe el arrastre

        RectTransform viewport = CrearHijoEstirado("Viewport", scroll);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform contenido = CrearRect("Contenido", viewport);
        contenido.anchorMin = new Vector2(0f, 1f);
        contenido.anchorMax = new Vector2(1f, 1f);
        contenido.pivot = new Vector2(0.5f, 1f);
        contenido.sizeDelta = Vector2.zero;
        contenido.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vl = contenido.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(16, 16, 4, 24);
        vl.spacing = 14f;
        vl.childAlignment = TextAnchor.UpperCenter;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        ContentSizeFitter fitter = contenido.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect sr = scroll.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Elastic;
        sr.scrollSensitivity = 40f;
        sr.viewport = viewport;
        sr.content = contenido;

        // Script de la lista
        UIListaPersonal listaUI = personal.gameObject.AddComponent<UIListaPersonal>();
        SerializedObject so = new SerializedObject(listaUI);
        so.FindProperty("prefabItem").objectReferenceValue = prefab;
        so.FindProperty("contenedor").objectReferenceValue = contenido;
        so.FindProperty("botonX1").objectReferenceValue = bX1;
        so.FindProperty("botonX10").objectReferenceValue = bX10;
        so.FindProperty("botonMax").objectReferenceValue = bMax;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Crea (o recrea) el prefab de la fila de un puesto.</summary>
    private static UIPuestoItem CrearPrefabItemPuesto()
    {
        const float alto = 220f;
        const float izquierdaTextos = 188f;  // 24 margen + 140 insignia + 24
        const float derechaTextos = 328f;    // 24 margen + 280 botón + 24

        GameObject raiz = new GameObject("ItemPuesto", typeof(RectTransform));
        raiz.layer = LayerMask.NameToLayer("UI");
        RectTransform rtRaiz = raiz.GetComponent<RectTransform>();
        rtRaiz.sizeDelta = new Vector2(1000f, alto);

        AgregarImagenRedondeada(raiz, Hex(ColorTarjeta), 0.4f).raycastTarget = false;
        LayoutElement le = raiz.AddComponent<LayoutElement>();
        le.minHeight = alto;
        le.preferredHeight = alto;

        // Insignia de color con la inicial
        RectTransform rtInsignia = CrearRect("Insignia", rtRaiz);
        rtInsignia.anchorMin = rtInsignia.anchorMax = new Vector2(0f, 0.5f);
        rtInsignia.pivot = new Vector2(0f, 0.5f);
        rtInsignia.sizeDelta = new Vector2(140f, 140f);
        rtInsignia.anchoredPosition = new Vector2(24f, 0f);
        Image insignia = AgregarImagenRedondeada(rtInsignia.gameObject, Color.gray, 0.3f);
        insignia.raycastTarget = false;

        TextMeshProUGUI inicial = CrearTexto("Inicial", rtInsignia, "E", 56, Color.white);
        Estirar(inicial.rectTransform);

        // Textos del medio
        TextMeshProUGUI nombre = CrearTexto("TextoNombre", rtRaiz, "Puesto", 42, Hex(ColorTexto));
        nombre.alignment = TextAlignmentOptions.MidlineLeft;
        nombre.overflowMode = TextOverflowModes.Ellipsis;
        FilaArriba(nombre.rectTransform, izquierdaTextos, derechaTextos, 22f, 54f);

        TextMeshProUGUI cantidad = CrearTexto("TextoCantidad", rtRaiz, "0 / 3", 36, Hex(ColorTexto));
        cantidad.alignment = TextAlignmentOptions.MidlineRight;
        FilaArriba(cantidad.rectTransform, izquierdaTextos, derechaTextos, 22f, 54f);

        TextMeshProUGUI detalle = CrearTexto("TextoDetalle", rtRaiz, "+$0/s c/u", 26, Hex(ColorPlata));
        detalle.alignment = TextAlignmentOptions.MidlineLeft;
        detalle.overflowMode = TextOverflowModes.Ellipsis;
        detalle.enableAutoSizing = true; // achica la letra si no entra (ej: "Supervisores")
        detalle.fontSizeMin = 18f;
        detalle.fontSizeMax = 26f;
        FilaArriba(detalle.rectTransform, izquierdaTextos, derechaTextos, 80f, 40f);

        TextMeshProUGUI descripcion = CrearTexto("TextoDescripcion", rtRaiz, "Descripción", 26, Hex(ColorTextoSuave));
        descripcion.fontStyle = FontStyles.Normal;
        descripcion.alignment = TextAlignmentOptions.TopLeft;
        descripcion.overflowMode = TextOverflowModes.Ellipsis;
        FilaArriba(descripcion.rectTransform, izquierdaTextos, derechaTextos, 124f, 80f);

        // Botón Contratar
        RectTransform rtBoton = CrearRect("BotonComprar", rtRaiz);
        rtBoton.anchorMin = rtBoton.anchorMax = new Vector2(1f, 0.5f);
        rtBoton.pivot = new Vector2(1f, 0.5f);
        rtBoton.sizeDelta = new Vector2(280f, 150f);
        rtBoton.anchoredPosition = new Vector2(-24f, 0f);
        Image imgBoton = AgregarImagenRedondeada(rtBoton.gameObject, Hex(ColorPlata), 0.3f);
        Button boton = rtBoton.gameObject.AddComponent<Button>();
        ConfigurarBoton(boton, imgBoton);

        TextMeshProUGUI textoBoton = CrearTexto("TextoBoton", rtBoton, "Contratar x1\n$15", 30, Color.white);
        Estirar(textoBoton.rectTransform);

        // Script con referencias
        UIPuestoItem item = raiz.AddComponent<UIPuestoItem>();
        SerializedObject so = new SerializedObject(item);
        so.FindProperty("insignia").objectReferenceValue = insignia;
        so.FindProperty("textoInicial").objectReferenceValue = inicial;
        so.FindProperty("textoNombre").objectReferenceValue = nombre;
        so.FindProperty("textoCantidad").objectReferenceValue = cantidad;
        so.FindProperty("textoDetalle").objectReferenceValue = detalle;
        so.FindProperty("textoDescripcion").objectReferenceValue = descripcion;
        so.FindProperty("botonComprar").objectReferenceValue = boton;
        so.FindProperty("textoBoton").objectReferenceValue = textoBoton;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefabPuesto);
        Object.DestroyImmediate(raiz);
        return prefab.GetComponent<UIPuestoItem>();
    }

    // =================================================================
    // Partes del Paso 4
    // =================================================================
    private static void CrearBarraSuperior(Transform padre)
    {
        RectTransform barra = CrearRect("BarraSuperior", padre);
        barra.anchorMin = new Vector2(0f, 1f);
        barra.anchorMax = new Vector2(1f, 1f);
        barra.pivot = new Vector2(0.5f, 1f);
        barra.offsetMin = new Vector2(24f, -246f); // alto 230, 16 px desde arriba
        barra.offsetMax = new Vector2(-24f, -16f);
        AgregarImagenRedondeada(barra.gameObject, Hex(ColorPanel), 0.4f).raycastTarget = false;

        // Izquierda: etapa y nombre de empresa (en el Paso 6 se vuelven dinámicos)
        TextMeshProUGUI etapa = CrearTexto("TextoEtapa", barra, "Freelancer en un garaje", 28, Hex(ColorAcento));
        etapa.alignment = TextAlignmentOptions.MidlineLeft;
        etapa.overflowMode = TextOverflowModes.Ellipsis;
        FilaArriba(etapa.rectTransform, 32f, 520f, 30f, 40f);

        TextMeshProUGUI empresa = CrearTexto("TextoEmpresa", barra, "Garaje Unipersonal", 44, Hex(ColorTexto));
        empresa.alignment = TextAlignmentOptions.MidlineLeft;
        empresa.overflowMode = TextOverflowModes.Ellipsis;
        empresa.enableAutoSizing = true;
        empresa.fontSizeMin = 30f;
        empresa.fontSizeMax = 44f;
        FilaArriba(empresa.rectTransform, 32f, 520f, 72f, 64f);

        // Derecha: plata y $/seg
        TextMeshProUGUI plata = CrearTexto("TextoPlata", barra, "$0", 80, Hex(ColorPlata));
        plata.alignment = TextAlignmentOptions.MidlineRight;
        plata.enableAutoSizing = true;
        plata.fontSizeMin = 50f;
        plata.fontSizeMax = 80f;
        FilaArriba(plata.rectTransform, 500f, 32f, 18f, 96f);

        TextMeshProUGUI porSegundo = CrearTexto("TextoPorSegundo", barra, "+$0/s", 32, Hex(ColorTextoSuave));
        porSegundo.alignment = TextAlignmentOptions.MidlineRight;
        FilaArriba(porSegundo.rectTransform, 500f, 32f, 112f, 44f);

        // Abajo: barra de progreso de etapa (en el Paso 6 se llena)
        RectTransform fondoBarra = CrearRect("BarraProgreso", barra);
        fondoBarra.anchorMin = new Vector2(0f, 0f);
        fondoBarra.anchorMax = new Vector2(1f, 0f);
        fondoBarra.pivot = new Vector2(0.5f, 0f);
        fondoBarra.offsetMin = new Vector2(32f, 26f);
        fondoBarra.offsetMax = new Vector2(-32f, 48f); // 22 px de alto
        AgregarImagenRedondeada(fondoBarra.gameObject, Hex(ColorFondo), 1f).raycastTarget = false;

        RectTransform relleno = CrearRect("Relleno", fondoBarra);
        relleno.anchorMin = Vector2.zero;
        relleno.anchorMax = new Vector2(0f, 1f); // el ancho = progreso (0 a 1)
        relleno.pivot = new Vector2(0f, 0.5f);
        relleno.offsetMin = Vector2.zero;
        relleno.offsetMax = Vector2.zero;
        AgregarImagenRedondeada(relleno.gameObject, Hex(ColorAcento), 1f).raycastTarget = false;

        UIBarraSuperior script = barra.gameObject.AddComponent<UIBarraSuperior>();
        SerializedObject so = new SerializedObject(script);
        so.FindProperty("textoPlata").objectReferenceValue = plata;
        so.FindProperty("textoPorSegundo").objectReferenceValue = porSegundo;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CrearEstadisticas(Transform padre)
    {
        RectTransform fila = CrearRect("Estadisticas", padre);
        fila.anchorMin = new Vector2(0f, 0f);
        fila.anchorMax = new Vector2(1f, 0f);
        fila.pivot = new Vector2(0.5f, 0f);
        fila.offsetMin = new Vector2(24f, 8f);
        fila.offsetMax = new Vector2(-24f, 72f); // 64 px de alto

        HorizontalLayoutGroup hl = fila.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 12f;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = true;
        hl.childForceExpandHeight = true;

        TextMeshProUGUI personal = CrearChipEstadistica("Personal", fila);
        TextMeshProUGUI tratos = CrearChipEstadistica("Tratos", fila);
        TextMeshProUGUI acciones = CrearChipEstadistica("Acciones", fila);

        UIEstadisticas script = fila.gameObject.AddComponent<UIEstadisticas>();
        SerializedObject so = new SerializedObject(script);
        so.FindProperty("textoPersonal").objectReferenceValue = personal;
        so.FindProperty("textoTratos").objectReferenceValue = tratos;
        so.FindProperty("textoAcciones").objectReferenceValue = acciones;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TextMeshProUGUI CrearChipEstadistica(string nombre, Transform padre)
    {
        RectTransform chip = CrearRect("Chip" + nombre, padre);
        AgregarImagenRedondeada(chip.gameObject, Hex(ColorPanel), 0.3f).raycastTarget = false;

        TextMeshProUGUI t = CrearTexto("Texto", chip, nombre + " <b>0</b>", 30, Hex(ColorTexto));
        t.fontStyle = FontStyles.Normal;
        t.enableAutoSizing = true;
        t.fontSizeMin = 20f;
        t.fontSizeMax = 30f;
        Estirar(t.rectTransform);
        return t;
    }

    // =================================================================
    // Partes del Paso 5
    // =================================================================
    private static DepartamentoData[] CrearDepartamentos()
    {
        DepartamentoData rrhh = CrearOObtenerDepartamento(1, "rrhh", "Recursos Humanos", "RRHH", "#2C6A9A",
            "Capacitaciones y after office: todo el personal produce +25% por nivel.",
            400, 2.4, 0, 0.25, 0, 0, 1);

        DepartamentoData marketing = CrearOObtenerDepartamento(2, "marketing", "Marketing", "MKT", "#C2410C",
            "Publicidad en redes: el clic vale más, suma 1% de tu $/s por nivel y la producción sube +10%.",
            60, 2.2, 0, 0.1, 1, 0.01, 1);

        DepartamentoData finanzas = CrearOObtenerDepartamento(3, "finanzas", "Finanzas", "FIN", "#6D3FC4",
            "Contadores creativos: contratar sale 5% más barato por nivel.",
            2500, 2.8, 12, 0, 0, 0, 0.95);

        DepartamentoData[] todos = { rrhh, marketing, finanzas };
        foreach (DepartamentoData d in todos) EditorUtility.SetDirty(d);
        AssetDatabase.SaveAssets();
        return todos;
    }

    private static DepartamentoData CrearOObtenerDepartamento(int orden, string id, string nombre, string sigla,
        string colorHex, string descripcion, double costo, double multiplicador, int nivelMaximo,
        double bonusProduccion, double bonusClic, double porcentajeIngresoAlClic, double descuento)
    {
        string ruta = $"{CarpetaDepartamentos}/{orden}_{sigla}.asset";
        DepartamentoData existente = AssetDatabase.LoadAssetAtPath<DepartamentoData>(ruta);
        if (existente != null) return existente; // no pisa lo que balanceaste a mano

        DepartamentoData d = ScriptableObject.CreateInstance<DepartamentoData>();
        d.id = id;
        d.nombre = nombre;
        d.sigla = sigla;
        d.color = Hex(colorHex);
        d.descripcion = descripcion;
        d.costoBase = costo;
        d.multiplicadorCosto = multiplicador;
        d.nivelMaximo = nivelMaximo;
        d.bonusProduccionPorNivel = bonusProduccion;
        d.bonusClicPorNivel = bonusClic;
        d.porcentajeIngresoAlClicPorNivel = porcentajeIngresoAlClic;
        d.descuentoContratacionPorNivel = descuento;

        AssetDatabase.CreateAsset(d, ruta);
        return d;
    }

    /// <summary>Encabezado de 110 px con un título a la izquierda.</summary>
    private static RectTransform CrearEncabezado(Transform padre, string texto)
    {
        RectTransform encabezado = CrearRect("Encabezado", padre);
        Ubicar(encabezado, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -110f), new Vector2(0f, 0f));

        TextMeshProUGUI titulo = CrearTexto("Titulo", encabezado, texto, 46, Hex(ColorTexto));
        titulo.alignment = TextAlignmentOptions.MidlineLeft;
        Ubicar(titulo.rectTransform, Vector2.zero, Vector2.one, new Vector2(32f, 0f), new Vector2(-32f, 0f));
        return encabezado;
    }

    /// <summary>Lista vertical con scroll que ocupa todo el padre menos "margenArriba". Devuelve el contenedor de filas.</summary>
    private static RectTransform CrearListaConScroll(Transform padre, float margenArriba)
    {
        RectTransform scroll = CrearRect("Scroll", padre);
        Ubicar(scroll, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -margenArriba));
        Image zonaToque = scroll.gameObject.AddComponent<Image>();
        zonaToque.color = new Color(1f, 1f, 1f, 0f); // invisible, pero recibe el arrastre

        RectTransform viewport = CrearHijoEstirado("Viewport", scroll);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform contenido = CrearRect("Contenido", viewport);
        contenido.anchorMin = new Vector2(0f, 1f);
        contenido.anchorMax = new Vector2(1f, 1f);
        contenido.pivot = new Vector2(0.5f, 1f);
        contenido.sizeDelta = Vector2.zero;
        contenido.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vl = contenido.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(16, 16, 4, 24);
        vl.spacing = 14f;
        vl.childAlignment = TextAnchor.UpperCenter;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        ContentSizeFitter fitter = contenido.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect sr = scroll.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Elastic;
        sr.scrollSensitivity = 40f;
        sr.viewport = viewport;
        sr.content = contenido;

        return contenido;
    }

    /// <summary>Crea (o recrea) el prefab de la fila de un departamento.</summary>
    private static UIDepartamentoItem CrearPrefabItemDepartamento()
    {
        const float alto = 220f;
        const float izquierdaTextos = 188f;
        const float derechaTextos = 328f;

        GameObject raiz = new GameObject("ItemDepartamento", typeof(RectTransform));
        raiz.layer = LayerMask.NameToLayer("UI");
        RectTransform rtRaiz = raiz.GetComponent<RectTransform>();
        rtRaiz.sizeDelta = new Vector2(1000f, alto);

        AgregarImagenRedondeada(raiz, Hex(ColorTarjeta), 0.4f).raycastTarget = false;
        LayoutElement le = raiz.AddComponent<LayoutElement>();
        le.minHeight = alto;
        le.preferredHeight = alto;

        // Insignia con la sigla
        RectTransform rtInsignia = CrearRect("Insignia", rtRaiz);
        rtInsignia.anchorMin = rtInsignia.anchorMax = new Vector2(0f, 0.5f);
        rtInsignia.pivot = new Vector2(0f, 0.5f);
        rtInsignia.sizeDelta = new Vector2(140f, 140f);
        rtInsignia.anchoredPosition = new Vector2(24f, 0f);
        Image insignia = AgregarImagenRedondeada(rtInsignia.gameObject, Color.gray, 0.3f);
        insignia.raycastTarget = false;

        TextMeshProUGUI sigla = CrearTexto("Sigla", rtInsignia, "RRHH", 44, Color.white);
        sigla.enableAutoSizing = true;
        sigla.fontSizeMin = 24f;
        sigla.fontSizeMax = 44f;
        Estirar(sigla.rectTransform);
        sigla.margin = new Vector4(8f, 0f, 8f, 0f);

        // Textos del medio
        TextMeshProUGUI nombre = CrearTexto("TextoNombre", rtRaiz, "Departamento", 40, Hex(ColorTexto));
        nombre.alignment = TextAlignmentOptions.MidlineLeft;
        nombre.overflowMode = TextOverflowModes.Ellipsis;
        nombre.enableAutoSizing = true;
        nombre.fontSizeMin = 30f;
        nombre.fontSizeMax = 40f;
        FilaArriba(nombre.rectTransform, izquierdaTextos, derechaTextos + 150f, 22f, 54f);

        TextMeshProUGUI nivel = CrearTexto("TextoNivel", rtRaiz, "Nv. 0", 34, Hex(ColorTexto));
        nivel.alignment = TextAlignmentOptions.MidlineRight;
        FilaArriba(nivel.rectTransform, izquierdaTextos, derechaTextos, 22f, 54f);

        TextMeshProUGUI detalle = CrearTexto("TextoDetalle", rtRaiz, "Ahora: ×1", 26, Hex(ColorPlata));
        detalle.alignment = TextAlignmentOptions.MidlineLeft;
        detalle.overflowMode = TextOverflowModes.Ellipsis;
        detalle.enableAutoSizing = true;
        detalle.fontSizeMin = 18f;
        detalle.fontSizeMax = 26f;
        FilaArriba(detalle.rectTransform, izquierdaTextos, derechaTextos, 80f, 40f);

        TextMeshProUGUI descripcion = CrearTexto("TextoDescripcion", rtRaiz, "Descripción", 24, Hex(ColorTextoSuave));
        descripcion.fontStyle = FontStyles.Normal;
        descripcion.alignment = TextAlignmentOptions.TopLeft;
        descripcion.overflowMode = TextOverflowModes.Ellipsis;
        FilaArriba(descripcion.rectTransform, izquierdaTextos, derechaTextos, 124f, 80f);

        // Botón Mejorar
        RectTransform rtBoton = CrearRect("BotonComprar", rtRaiz);
        rtBoton.anchorMin = rtBoton.anchorMax = new Vector2(1f, 0.5f);
        rtBoton.pivot = new Vector2(1f, 0.5f);
        rtBoton.sizeDelta = new Vector2(280f, 150f);
        rtBoton.anchoredPosition = new Vector2(-24f, 0f);
        Image imgBoton = AgregarImagenRedondeada(rtBoton.gameObject, Hex(ColorAcento), 0.3f);
        Button boton = rtBoton.gameObject.AddComponent<Button>();
        ConfigurarBoton(boton, imgBoton);

        TextMeshProUGUI textoBoton = CrearTexto("TextoBoton", rtBoton, "Mejorar\n$60", 30, Color.white);
        Estirar(textoBoton.rectTransform);

        // Script con referencias
        UIDepartamentoItem item = raiz.AddComponent<UIDepartamentoItem>();
        SerializedObject so = new SerializedObject(item);
        so.FindProperty("insignia").objectReferenceValue = insignia;
        so.FindProperty("textoSigla").objectReferenceValue = sigla;
        so.FindProperty("textoNombre").objectReferenceValue = nombre;
        so.FindProperty("textoNivel").objectReferenceValue = nivel;
        so.FindProperty("textoDetalle").objectReferenceValue = detalle;
        so.FindProperty("textoDescripcion").objectReferenceValue = descripcion;
        so.FindProperty("botonComprar").objectReferenceValue = boton;
        so.FindProperty("textoBoton").objectReferenceValue = textoBoton;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefabDepartamento);
        Object.DestroyImmediate(raiz);
        return prefab.GetComponent<UIDepartamentoItem>();
    }

    // =================================================================
    // Partes del Paso 6
    // =================================================================
    private static EtapasConfig CrearOObtenerEtapas()
    {
        EtapasConfig existente = AssetDatabase.LoadAssetAtPath<EtapasConfig>(RutaEtapas);
        if (existente != null) return existente; // no pisa lo que editaste a mano

        EtapasConfig config = ScriptableObject.CreateInstance<EtapasConfig>();
        AgregarEtapa(config, "Freelancer en un garaje", "Garaje Unipersonal", 0);
        AgregarEtapa(config, "Emprendedor", "Garaje & Asociados", 1e3);
        AgregarEtapa(config, "Startup", "Garaje Labs", 25e3);
        AgregarEtapa(config, "PyME", "Garaje S.R.L.", 5e5);
        AgregarEtapa(config, "Empresa nacional", "Garaje S.A.", 1e7);
        AgregarEtapa(config, "Corporación", "Garaje Corp.", 2.5e8);
        AgregarEtapa(config, "Multinacional", "Garaje Global Holdings", 1e10);
        AgregarEtapa(config, "Imperio global", "Grupo Garaje Internacional", 1e12);

        AssetDatabase.CreateAsset(config, RutaEtapas);
        AssetDatabase.SaveAssets();
        return config;
    }

    private static void AgregarEtapa(EtapasConfig config, string etapa, string empresa, double minimo)
    {
        config.etapas.Add(new EtapasConfig.Etapa
        {
            nombreEtapa = etapa,
            nombreEmpresa = empresa,
            minimoFacturado = minimo
        });
    }

    /// <summary>Crea (o recrea) el prefab de la fila de una etapa.</summary>
    private static UIEtapaItem CrearPrefabItemEtapa()
    {
        const float alto = 120f;

        GameObject raiz = new GameObject("ItemEtapa", typeof(RectTransform));
        raiz.layer = LayerMask.NameToLayer("UI");
        RectTransform rtRaiz = raiz.GetComponent<RectTransform>();
        rtRaiz.sizeDelta = new Vector2(1000f, alto);

        Image fondo = AgregarImagenRedondeada(raiz, Hex(ColorTarjeta), 0.4f);
        fondo.raycastTarget = false;
        LayoutElement le = raiz.AddComponent<LayoutElement>();
        le.minHeight = alto;
        le.preferredHeight = alto;

        // Círculo con el número de etapa
        RectTransform rtMarcador = CrearRect("Marcador", rtRaiz);
        rtMarcador.anchorMin = rtMarcador.anchorMax = new Vector2(0f, 0.5f);
        rtMarcador.pivot = new Vector2(0f, 0.5f);
        rtMarcador.sizeDelta = new Vector2(72f, 72f);
        rtMarcador.anchoredPosition = new Vector2(24f, 0f);
        Image marcador = rtMarcador.gameObject.AddComponent<Image>();
        marcador.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        marcador.color = Hex(ColorAcento);
        marcador.raycastTarget = false;

        TextMeshProUGUI numero = CrearTexto("Numero", rtMarcador, "1", 34, Color.white);
        Estirar(numero.rectTransform);

        // Textos
        TextMeshProUGUI etapa = CrearTexto("TextoEtapa", rtRaiz, "Etapa", 34, Hex(ColorTexto));
        etapa.alignment = TextAlignmentOptions.MidlineLeft;
        etapa.overflowMode = TextOverflowModes.Ellipsis;
        FilaArriba(etapa.rectTransform, 120f, 260f, 14f, 48f);

        TextMeshProUGUI empresa = CrearTexto("TextoEmpresa", rtRaiz, "Empresa", 26, Hex(ColorTextoSuave));
        empresa.fontStyle = FontStyles.Normal;
        empresa.alignment = TextAlignmentOptions.MidlineLeft;
        empresa.overflowMode = TextOverflowModes.Ellipsis;
        FilaArriba(empresa.rectTransform, 120f, 260f, 62f, 40f);

        TextMeshProUGUI meta = CrearTexto("TextoMeta", rtRaiz, "$0", 30, Hex(ColorAcento));
        meta.alignment = TextAlignmentOptions.MidlineRight;
        RectTransform rtMeta = meta.rectTransform;
        rtMeta.anchorMin = new Vector2(1f, 0f);
        rtMeta.anchorMax = new Vector2(1f, 1f);
        rtMeta.pivot = new Vector2(1f, 0.5f);
        rtMeta.sizeDelta = new Vector2(230f, 0f);
        rtMeta.anchoredPosition = new Vector2(-24f, 0f);

        UIEtapaItem item = raiz.AddComponent<UIEtapaItem>();
        SerializedObject so = new SerializedObject(item);
        so.FindProperty("fondo").objectReferenceValue = fondo;
        so.FindProperty("marcador").objectReferenceValue = marcador;
        so.FindProperty("textoNumero").objectReferenceValue = numero;
        so.FindProperty("textoEtapa").objectReferenceValue = etapa;
        so.FindProperty("textoEmpresa").objectReferenceValue = empresa;
        so.FindProperty("textoMeta").objectReferenceValue = meta;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefabEtapa);
        Object.DestroyImmediate(raiz);
        return prefab.GetComponent<UIEtapaItem>();
    }

    private static T BuscarComponente<T>(Transform padre, string nombre) where T : Component
    {
        Transform t = BuscarDescendiente(padre, nombre);
        return t != null ? t.GetComponent<T>() : null;
    }

    private static Button CrearBotonChico(string nombre, Transform padre, string texto)
    {
        RectTransform rt = CrearRect(nombre, padre);
        Image img = AgregarImagenRedondeada(rt.gameObject, Hex(ColorFondo), 0.3f);
        Button b = rt.gameObject.AddComponent<Button>();
        ConfigurarBoton(b, img);

        TextMeshProUGUI t = CrearTexto("Texto", rt, texto, 36, Hex(ColorTexto));
        Estirar(t.rectTransform);
        return b;
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

    private static SafeArea BuscarAreaSegura()
    {
        SafeArea area = Object.FindFirstObjectByType<SafeArea>();
        if (area == null)
        {
            EditorUtility.DisplayDialog("CEO Clicker",
                "No encontré el objeto AreaSegura en la escena abierta.\nAbrí la escena Principal (o corré el Paso 1) y probá de nuevo.",
                "OK");
        }
        return area;
    }

    private static void GuardarEscenaActiva()
    {
        Scene escena = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
    }

    private static TextMeshProUGUI CrearTexto(string nombre, Transform padre, string contenido, float tamanio, Color color)
    {
        RectTransform rt = CrearRect(nombre, padre);

        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = contenido;
        t.fontSize = tamanio;
        t.color = color;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    private static RectTransform CrearRect(string nombre, Transform padre)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform rt = go.GetComponent<RectTransform>();
        if (padre != null) rt.SetParent(padre, false);
        return rt;
    }

    private static RectTransform CrearHijoEstirado(string nombre, Transform padre)
    {
        RectTransform rt = CrearRect(nombre, padre);
        Estirar(rt);
        return rt;
    }

    private static RectTransform BuscarOCrearHijo(string nombre, Transform padre)
    {
        Transform t = padre.Find(nombre);
        return t != null ? (RectTransform)t : CrearRect(nombre, padre);
    }

    private static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Ubicar(RectTransform rt, Vector2 anclaMin, Vector2 anclaMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anclaMin;
        rt.anchorMax = anclaMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    /// <summary>Fila pegada arriba: margen izquierdo/derecho, distancia desde arriba y alto.</summary>
    private static void FilaArriba(RectTransform rt, float izquierda, float derecha, float desdeArriba, float alto)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(izquierda, -desdeArriba - alto);
        rt.offsetMax = new Vector2(-derecha, -desdeArriba);
    }

    private static Image AgregarImagenRedondeada(GameObject go, Color color, float multiplicadorEsquinas)
    {
        Image img = go.AddComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = multiplicadorEsquinas; // más chico = esquinas más redondas
        img.color = color;
        return img;
    }

    private static void ConfigurarBoton(Button boton, Graphic objetivo)
    {
        boton.targetGraphic = objetivo;

        ColorBlock colores = boton.colors;
        colores.normalColor = Color.white;
        colores.highlightedColor = Color.white;
        colores.pressedColor = new Color(0.85f, 0.85f, 0.85f);
        colores.selectedColor = Color.white;
        colores.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.5f);
        colores.fadeDuration = 0.05f;
        boton.colors = colores;

        Navigation nav = boton.navigation;
        nav.mode = Navigation.Mode.None;
        boton.navigation = nav;
    }

    private static Transform BuscarDescendiente(Transform padre, string nombre)
    {
        foreach (Transform hijo in padre.GetComponentsInChildren<Transform>(true))
        {
            if (hijo != padre && hijo.name == nombre) return hijo;
        }
        return null;
    }

    private static void DestruirDescendiente(Transform padre, string nombre)
    {
        Transform hijo = BuscarDescendiente(padre, nombre);
        if (hijo != null) Object.DestroyImmediate(hijo.gameObject);
    }

    private static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }
}
