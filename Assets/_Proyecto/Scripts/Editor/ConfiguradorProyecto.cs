using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
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

    // Paleta del juego: se lee de Datos/Tema.asset (se crea solo con la paleta pastel)
    private const string RutaTema = RaizProyecto + "/Datos/Tema.asset";
    private static TemaColores temaCache;

    private static TemaColores Tema
    {
        get
        {
            if (temaCache == null) temaCache = CargarOCrearTema();
            return temaCache;
        }
    }

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
            "Etapas, salir a la bolsa y borrar partida\n(Pasos 6 y 10)", 36, Tema.textoSuave);
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
        soPestanas.FindProperty("colorSeleccionado").colorValue = Tema.seleccion;
        soPestanas.FindProperty("colorNormal").colorValue = Tema.botonSecundario;
        soPestanas.FindProperty("textoSeleccionado").colorValue = Tema.textoBoton;
        soPestanas.FindProperty("textoNormal").colorValue = Tema.texto;
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

        TextMeshProUGUI tituloEtapas = CrearTexto("TituloEtapas", contenido, "Etapas", 36, Tema.texto);
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
    // PASO 7: organigrama visual
    // =================================================================
    [MenuItem("CEO Clicker/Paso 7 - Organigrama", priority = 7)]
    public static void ConfigurarPaso7()
    {
        if (!TextMeshProListo()) return;

        SafeArea area = BuscarAreaSegura();
        if (area == null) return;

        Transform panel = BuscarDescendiente(area.transform, "PanelOrganigrama");
        if (panel == null)
        {
            EditorUtility.DisplayDialog("CEO Clicker", "Primero corré el Paso 3 (falta el PanelOrganigrama).", "OK");
            return;
        }

        // Se vacía el panel (texto provisorio o restos de una corrida anterior)
        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(panel.GetChild(i).gameObject);
        }
        UIOrganigrama viejo = panel.GetComponent<UIOrganigrama>();
        if (viejo != null) Object.DestroyImmediate(viejo);

        // Recorta lo que se salga del panel
        if (panel.GetComponent<RectMask2D>() == null) panel.gameObject.AddComponent<RectMask2D>();

        UIOrganigrama organigrama = panel.gameObject.AddComponent<UIOrganigrama>();
        Color linea = Tema.textoSuave;
        linea.a = 0.6f;

        SerializedObject so = new SerializedObject(organigrama);
        so.FindProperty("spriteNodo").objectReferenceValue = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        so.FindProperty("colorCEO").colorValue = Tema.botonFacturar;
        so.FindProperty("colorTextoNodo").colorValue = Tema.textoBoton;
        so.FindProperty("colorLinea").colorValue = linea;
        so.FindProperty("colorMas").colorValue = Tema.bloqueado;
        so.FindProperty("colorAyuda").colorValue = Tema.textoSuave;
        so.ApplyModifiedPropertiesWithoutUndo();

        GuardarEscenaActiva();
        Debug.Log("<color=#D65A92><b>CEO Clicker:</b></color> Paso 7 listo. Contratá gente y mirá crecer el organigrama.");
    }

    // =================================================================
    // PASO 8: animaciones y feedback
    // =================================================================
    [MenuItem("CEO Clicker/Paso 8 - Animaciones y feedback", priority = 8)]
    public static void ConfigurarPaso8()
    {
        if (!TextMeshProListo()) return;

        SafeArea area = BuscarAreaSegura();
        if (area == null) return;

        UIBotonFacturar boton = Object.FindFirstObjectByType<UIBotonFacturar>();
        UIOrganigrama organigrama = Object.FindFirstObjectByType<UIOrganigrama>();
        if (boton == null || organigrama == null)
        {
            EditorUtility.DisplayDialog("CEO Clicker", "Primero corré los Pasos 2 y 7 (falta el botón Facturar o el organigrama).", "OK");
            return;
        }

        // Capa para los "+$X": cubre el área segura, va encima de todo y no bloquea toques
        DestruirDescendiente(area.transform, "CapaFlotante");
        RectTransform capa = CrearHijoEstirado("CapaFlotante", area.transform);
        capa.SetAsLastSibling();

        UITextosFlotantes flotantes = capa.gameObject.AddComponent<UITextosFlotantes>();
        SerializedObject soFlotantes = new SerializedObject(flotantes);
        soFlotantes.FindProperty("boton").objectReferenceValue = boton;
        soFlotantes.FindProperty("color").colorValue = Tema.plata;
        soFlotantes.ApplyModifiedPropertiesWithoutUndo();

        // Color de los "$" del organigrama
        SerializedObject soOrganigrama = new SerializedObject(organigrama);
        soOrganigrama.FindProperty("colorSigno").colorValue = Tema.plata;
        soOrganigrama.ApplyModifiedPropertiesWithoutUndo();

        GuardarEscenaActiva();
        Debug.Log("<color=#D65A92><b>CEO Clicker:</b></color> Paso 8 listo. Facturá y contratá para ver las animaciones.");
    }

    // =================================================================
    // PASO 9: cartel de ganancias offline (el guardado está en el GameManager)
    // =================================================================
    [MenuItem("CEO Clicker/Paso 9 - Guardado y ganancias offline", priority = 9)]
    public static void ConfigurarPaso9()
    {
        if (!TextMeshProListo()) return;

        SafeArea area = BuscarAreaSegura();
        if (area == null) return;

        DestruirDescendiente(area.transform, "AvisoOffline");

        // Raíz siempre activa (tiene el script); el panel es lo que se prende y apaga
        RectTransform raiz = CrearHijoEstirado("AvisoOffline", area.transform);
        raiz.SetAsLastSibling();

        RectTransform panel = CrearHijoEstirado("Panel", raiz);
        Image oscuro = panel.gameObject.AddComponent<Image>();
        Color sombra = Tema.texto;
        sombra.a = 0.45f;
        oscuro.color = sombra; // bloquea los toques de atrás

        RectTransform tarjeta = CrearRect("Tarjeta", panel);
        tarjeta.anchorMin = tarjeta.anchorMax = new Vector2(0.5f, 0.5f);
        tarjeta.pivot = new Vector2(0.5f, 0.5f);
        tarjeta.sizeDelta = new Vector2(880f, 620f);
        tarjeta.anchoredPosition = Vector2.zero;
        AgregarImagenRedondeada(tarjeta.gameObject, Tema.panel, 0.3f);

        TextMeshProUGUI titulo = CrearTexto("Titulo", tarjeta, "¡Volviste!", 60, Tema.texto);
        FilaArriba(titulo.rectTransform, 40f, 40f, 50f, 80f);

        TextMeshProUGUI explicacion = CrearTexto("Explicacion", tarjeta, "Mientras no estabas, tu equipo facturó", 34, Tema.textoSuave);
        explicacion.fontStyle = FontStyles.Normal;
        FilaArriba(explicacion.rectTransform, 40f, 40f, 145f, 50f);

        TextMeshProUGUI monto = CrearTexto("TextoMonto", tarjeta, "$0", 100, Tema.plata);
        monto.enableAutoSizing = true;
        monto.fontSizeMin = 50f;
        monto.fontSizeMax = 100f;
        FilaArriba(monto.rectTransform, 40f, 40f, 205f, 120f);

        TextMeshProUGUI detalle = CrearTexto("TextoDetalle", tarjeta, "Estuviste afuera 1 h", 28, Tema.textoSuave);
        detalle.fontStyle = FontStyles.Normal;
        FilaArriba(detalle.rectTransform, 40f, 40f, 335f, 44f);

        RectTransform rtBoton = CrearRect("BotonCerrar", tarjeta);
        rtBoton.anchorMin = rtBoton.anchorMax = new Vector2(0.5f, 0f);
        rtBoton.pivot = new Vector2(0.5f, 0f);
        rtBoton.sizeDelta = new Vector2(480f, 130f);
        rtBoton.anchoredPosition = new Vector2(0f, 50f);
        Image imgBoton = AgregarImagenRedondeada(rtBoton.gameObject, Tema.botonFacturar, 0.3f);
        Button boton = rtBoton.gameObject.AddComponent<Button>();
        ConfigurarBoton(boton, imgBoton);
        TextMeshProUGUI textoBoton = CrearTexto("Texto", rtBoton, "¡Genial!", 48, Tema.textoBoton);
        Estirar(textoBoton.rectTransform);

        UIAvisoOffline aviso = raiz.gameObject.AddComponent<UIAvisoOffline>();
        SerializedObject so = new SerializedObject(aviso);
        so.FindProperty("panel").objectReferenceValue = panel.gameObject;
        so.FindProperty("tarjeta").objectReferenceValue = tarjeta;
        so.FindProperty("textoMonto").objectReferenceValue = monto;
        so.FindProperty("textoDetalle").objectReferenceValue = detalle;
        so.FindProperty("botonCerrar").objectReferenceValue = boton;
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.gameObject.SetActive(false); // oculto hasta que haya algo que mostrar

        GuardarEscenaActiva();
        Debug.Log("<color=#D65A92><b>CEO Clicker:</b></color> Paso 9 listo. La partida se guarda en " + Application.persistentDataPath);
    }

    // =================================================================
    // PASO 10: salir a la bolsa y borrar partida (pestaña Empresa)
    // =================================================================
    [MenuItem("CEO Clicker/Paso 10 - Salir a la bolsa y borrar partida", priority = 10)]
    public static void ConfigurarPaso10()
    {
        if (!TextMeshProListo()) return;

        SafeArea area = BuscarAreaSegura();
        if (area == null) return;

        Transform panelEmpresa = BuscarDescendiente(area.transform, "PanelEmpresa");
        Transform contenido = panelEmpresa != null ? BuscarDescendiente(panelEmpresa, "Contenido") : null;
        if (contenido == null)
        {
            EditorUtility.DisplayDialog("CEO Clicker", "Primero corré el Paso 6 (falta la lista de la pestaña Empresa).", "OK");
            return;
        }

        DestruirDescendiente(contenido, "TarjetaBolsa");
        DestruirDescendiente(contenido, "TarjetaBorrar");

        // --- Tarjeta "Salir a la bolsa" (arriba de todo) ---
        RectTransform bolsa = CrearTarjeta("TarjetaBolsa", contenido, 400f);
        bolsa.SetSiblingIndex(0);

        TextMeshProUGUI tituloBolsa = CrearTexto("Titulo", bolsa, "Salir a la bolsa", 40, Tema.texto);
        tituloBolsa.alignment = TextAlignmentOptions.MidlineLeft;
        FilaArriba(tituloBolsa.rectTransform, 32f, 32f, 24f, 56f);

        TextMeshProUGUI detalleBolsa = CrearTexto("TextoDetalle", bolsa, "Facturá $1.00M en esta partida para salir a la bolsa.", 26, Tema.textoSuave);
        detalleBolsa.fontStyle = FontStyles.Normal;
        detalleBolsa.alignment = TextAlignmentOptions.TopLeft;
        detalleBolsa.overflowMode = TextOverflowModes.Ellipsis;
        FilaArriba(detalleBolsa.rectTransform, 32f, 32f, 86f, 110f);

        TextMeshProUGUI acciones = CrearTexto("TextoAcciones", bolsa, "Tenés 0 acciones", 28, Tema.acento);
        acciones.alignment = TextAlignmentOptions.MidlineLeft;
        acciones.enableAutoSizing = true;
        acciones.fontSizeMin = 18f;
        acciones.fontSizeMax = 28f;
        FilaArriba(acciones.rectTransform, 32f, 32f, 200f, 50f);

        BotonDobleToque botonBolsa = CrearBotonDobleToque("BotonBolsa", bolsa, "Salir a la bolsa", Tema.botonMejorar, Tema.aviso, 100f);

        // --- Tarjeta "Borrar partida" (abajo de todo) ---
        RectTransform borrar = CrearTarjeta("TarjetaBorrar", contenido, 270f);
        borrar.SetAsLastSibling();

        TextMeshProUGUI tituloBorrar = CrearTexto("Titulo", borrar, "Borrar partida", 36, Tema.texto);
        tituloBorrar.alignment = TextAlignmentOptions.MidlineLeft;
        FilaArriba(tituloBorrar.rectTransform, 32f, 32f, 22f, 50f);

        TextMeshProUGUI detalleBorrar = CrearTexto("TextoDetalle", borrar,
            "Empieza todo de cero, incluidas las acciones. No se puede deshacer.", 24, Tema.textoSuave);
        detalleBorrar.fontStyle = FontStyles.Normal;
        detalleBorrar.alignment = TextAlignmentOptions.TopLeft;
        FilaArriba(detalleBorrar.rectTransform, 32f, 32f, 76f, 64f);

        BotonDobleToque botonBorrar = CrearBotonDobleToque("BotonBorrar", borrar, "Borrar partida", Tema.aviso, Tema.texto, 90f);

        // --- Script del panel ---
        UIPanelEmpresa viejo = panelEmpresa.GetComponent<UIPanelEmpresa>();
        if (viejo != null) Object.DestroyImmediate(viejo);
        UIPanelEmpresa script = panelEmpresa.gameObject.AddComponent<UIPanelEmpresa>();

        SerializedObject so = new SerializedObject(script);
        so.FindProperty("textoBolsaDetalle").objectReferenceValue = detalleBolsa;
        so.FindProperty("textoAcciones").objectReferenceValue = acciones;
        so.FindProperty("botonBolsa").objectReferenceValue = botonBolsa;
        so.FindProperty("botonBorrar").objectReferenceValue = botonBorrar;
        so.ApplyModifiedPropertiesWithoutUndo();

        GuardarEscenaActiva();
        Debug.Log("<color=#D65A92><b>CEO Clicker:</b></color> Paso 10 listo. Mirá la pestaña Empresa.");
    }

    // =================================================================
    // PASO 11: sonido y vibración
    // =================================================================
    [MenuItem("CEO Clicker/Paso 11 - Sonido y vibración", priority = 11)]
    public static void ConfigurarPaso11()
    {
        if (!TextMeshProListo()) return;

        SafeArea area = BuscarAreaSegura();
        if (area == null) return;

        Transform panelEmpresa = BuscarDescendiente(area.transform, "PanelEmpresa");
        Transform contenido = panelEmpresa != null ? BuscarDescendiente(panelEmpresa, "Contenido") : null;
        Transform tarjetaBorrar = contenido != null ? BuscarDescendiente(contenido, "TarjetaBorrar") : null;
        if (tarjetaBorrar == null)
        {
            EditorUtility.DisplayDialog("CEO Clicker", "Primero corré el Paso 10 (falta la pestaña Empresa completa).", "OK");
            return;
        }

        // 1) Objeto Feedback con su AudioSource
        Feedback feedback = Object.FindFirstObjectByType<Feedback>();
        if (feedback == null)
        {
            GameObject go = new GameObject("Feedback");
            go.AddComponent<AudioSource>();
            feedback = go.AddComponent<Feedback>();
        }
        AudioSource fuente = feedback.GetComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;

        // Sonidos por defecto (solo si el campo está vacío, para no pisar los tuyos)
        SerializedObject soFeedback = new SerializedObject(feedback);
        AsignarSonidoSiFalta(soFeedback, "sonidoFacturar", "facturar.wav");
        AsignarSonidoSiFalta(soFeedback, "sonidoContratar", "contratar.wav");
        AsignarSonidoSiFalta(soFeedback, "sonidoMejorar", "mejorar.wav");
        AsignarSonidoSiFalta(soFeedback, "sonidoEtapa", "etapa.wav");
        AsignarSonidoSiFalta(soFeedback, "sonidoBolsa", "bolsa.wav");
        soFeedback.ApplyModifiedPropertiesWithoutUndo();

        // 2) Tarjeta de ajustes en la pestaña Empresa (antes de "Borrar partida")
        DestruirDescendiente(contenido, "TarjetaAjustes");
        RectTransform tarjeta = CrearTarjeta("TarjetaAjustes", contenido, 200f);
        tarjeta.SetSiblingIndex(tarjetaBorrar.GetSiblingIndex());

        TextMeshProUGUI titulo = CrearTexto("Titulo", tarjeta, "Ajustes", 36, Tema.texto);
        titulo.alignment = TextAlignmentOptions.MidlineLeft;
        FilaArriba(titulo.rectTransform, 32f, 32f, 20f, 50f);

        RectTransform fila = CrearRect("Botones", tarjeta);
        fila.anchorMin = new Vector2(0f, 0f);
        fila.anchorMax = new Vector2(1f, 0f);
        fila.pivot = new Vector2(0.5f, 0f);
        fila.offsetMin = new Vector2(32f, 26f);
        fila.offsetMax = new Vector2(-32f, 116f);
        HorizontalLayoutGroup hl = fila.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 20f;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = true;
        hl.childForceExpandHeight = true;

        Button botonSonido = CrearBotonAjuste("BotonSonido", fila, "Sonido: Sí", out TextMeshProUGUI textoSonido);
        Button botonVibracion = CrearBotonAjuste("BotonVibracion", fila, "Vibración: Sí", out TextMeshProUGUI textoVibracion);

        UIAjustes ajustes = tarjeta.gameObject.AddComponent<UIAjustes>();
        SerializedObject so = new SerializedObject(ajustes);
        so.FindProperty("botonSonido").objectReferenceValue = botonSonido;
        so.FindProperty("botonVibracion").objectReferenceValue = botonVibracion;
        so.FindProperty("textoSonido").objectReferenceValue = textoSonido;
        so.FindProperty("textoVibracion").objectReferenceValue = textoVibracion;
        so.FindProperty("colorEncendido").colorValue = Tema.botonContratar;
        so.FindProperty("colorApagado").colorValue = Tema.bloqueado;
        so.ApplyModifiedPropertiesWithoutUndo();

        GuardarEscenaActiva();
        Selection.activeGameObject = feedback.gameObject;
        Debug.Log("<color=#D65A92><b>CEO Clicker:</b></color> Paso 11 listo. Cambiá los sonidos en el objeto Feedback.");
    }

    // =================================================================
    // PASO 12: configuración de Android y build
    // =================================================================
    private const string IdentificadorAndroid = "com.ivan.ceoclicker";
    private const string RutaIcono = RaizProyecto + "/Sprites/icono.png";
    private const string RutaApk = "Builds/CEOClicker.apk";

    [MenuItem("CEO Clicker/Paso 12 - Configurar Android", priority = 12)]
    public static void ConfigurarPaso12()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            EditorUtility.DisplayDialog("CEO Clicker",
                "Falta el módulo de Android.\n\nAbrí Unity Hub > Installs > ⚙ de tu versión 6000.0.74f1 > Add modules > " +
                "tildá \"Android Build Support\" (con OpenJDK y Android SDK & NDK Tools) > Install.\n\n" +
                "Después reabrí el proyecto y volvé a correr este paso.",
                "OK");
            return;
        }

        // Identidad de la app
        PlayerSettings.companyName = "Ivan";
        PlayerSettings.productName = "CEO Clicker";
        PlayerSettings.bundleVersion = "1.0";
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, IdentificadorAndroid);

        // Pantalla: vertical fija y dibujar también detrás del notch (SafeArea acomoda la interfaz)
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.Android.renderOutsideSafeArea = true;

        // IL2CPP + ARM64 (lo que piden los celulares actuales y Google Play)
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

        // Pantalla de carga con el fondo pastel
        PlayerSettings.SplashScreen.backgroundColor = Tema.fondo;
        PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.DarkOnLight;

        // Ícono
        TextureImporter importador = AssetImporter.GetAtPath(RutaIcono) as TextureImporter;
        if (importador != null)
        {
            importador.textureType = TextureImporterType.Default;
            importador.mipmapEnabled = false;
            importador.textureCompression = TextureImporterCompression.Uncompressed;
            importador.SaveAndReimport();
        }

        Texture2D icono = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaIcono);
        if (icono != null)
        {
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icono }, IconKind.Any);
        }
        else
        {
            Debug.LogWarning("No encontré el ícono en " + RutaIcono + ". La app va a usar el ícono de Unity.");
        }

        // Escena principal como única escena del build
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(RutaEscena, true) };
        AssetDatabase.SaveAssets();

        Debug.Log("<color=#D65A92><b>CEO Clicker:</b></color> Paso 12 listo. Android configurado (" + IdentificadorAndroid + ").");

        // Cambiar de plataforma (reimporta assets: puede tardar unos minutos)
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
            EditorUtility.DisplayDialog("CEO Clicker",
                "¿Cambiar la plataforma a Android ahora?\nUnity va a reimportar los assets y puede tardar unos minutos.",
                "Cambiar", "Más tarde"))
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }
    }

    [MenuItem("CEO Clicker/Paso 12b - Generar APK", priority = 13)]
    public static void GenerarApk()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUtility.DisplayDialog("CEO Clicker", "Primero corré el Paso 12 y cambiá la plataforma a Android.", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string[] escenas = EditorBuildSettings.scenes.Where(e => e.enabled).Select(e => e.path).ToArray();
        if (escenas.Length == 0) escenas = new[] { RutaEscena };

        Directory.CreateDirectory(Path.GetDirectoryName(RutaApk));
        EditorUserBuildSettings.buildAppBundle = false; // .apk para instalar directo (no .aab)

        BuildPlayerOptions opciones = new BuildPlayerOptions
        {
            scenes = escenas,
            locationPathName = RutaApk,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport reporte = BuildPipeline.BuildPlayer(opciones);
        BuildSummary resumen = reporte.summary;

        if (resumen.result == BuildResult.Succeeded)
        {
            float megas = resumen.totalSize / (1024f * 1024f);
            Debug.Log($"<color=#D65A92><b>CEO Clicker:</b></color> APK lista: {Path.GetFullPath(RutaApk)} ({megas:0.0} MB)");
            EditorUtility.RevealInFinder(RutaApk);
        }
        else
        {
            Debug.LogError("El build falló (" + resumen.result + "). Revisá los errores de arriba en la consola.");
        }
    }

    // =================================================================
    // PASO 13: rubro y nombre de la empresa al empezar
    // =================================================================
    private const string RutaRubros = RaizProyecto + "/Datos/Rubros.asset";

    [MenuItem("CEO Clicker/Paso 13 - Rubro y nombre de la empresa", priority = 14)]
    public static void ConfigurarPaso13()
    {
        if (!TextMeshProListo()) return;

        SafeArea area = BuscarAreaSegura();
        if (area == null) return;

        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            EditorUtility.DisplayDialog("CEO Clicker", "Primero corré el Paso 2 (falta el GameManager).", "OK");
            return;
        }

        CrearCarpetas();

        // 1) Datos de los rubros
        RubrosConfig rubros = CrearOObtenerRubros();
        SerializedObject soGm = new SerializedObject(gm);
        soGm.FindProperty("configRubros").objectReferenceValue = rubros;
        soGm.ApplyModifiedPropertiesWithoutUndo();

        // 2) Cartel de bienvenida (encima de todo)
        DestruirDescendiente(area.transform, "Bienvenida");
        RectTransform raiz = CrearHijoEstirado("Bienvenida", area.transform);
        raiz.SetAsLastSibling();

        RectTransform panel = CrearHijoEstirado("Panel", raiz);
        Image oscuro = panel.gameObject.AddComponent<Image>();
        Color sombra = Tema.texto;
        sombra.a = 0.55f;
        oscuro.color = sombra; // bloquea los toques de atrás

        RectTransform tarjeta = CrearRect("Tarjeta", panel);
        tarjeta.anchorMin = tarjeta.anchorMax = new Vector2(0.5f, 0.5f);
        tarjeta.pivot = new Vector2(0.5f, 0.5f);
        tarjeta.sizeDelta = new Vector2(920f, 1240f);
        tarjeta.anchoredPosition = Vector2.zero;
        AgregarImagenRedondeada(tarjeta.gameObject, Tema.panel, 0.3f);

        TextMeshProUGUI titulo = CrearTexto("Titulo", tarjeta, "¡Fundá tu empresa!", 60, Tema.texto);
        FilaArriba(titulo.rectTransform, 40f, 40f, 44f, 80f);

        TextMeshProUGUI subtitulo = CrearTexto("Subtitulo", tarjeta,
            "Todo gran imperio empezó en un garaje.", 30, Tema.textoSuave);
        subtitulo.fontStyle = FontStyles.Normal;
        FilaArriba(subtitulo.rectTransform, 40f, 40f, 126f, 44f);

        // Nombre
        TextMeshProUGUI etiquetaNombre = CrearTexto("EtiquetaNombre", tarjeta, "¿Cómo se llama?", 34, Tema.texto);
        etiquetaNombre.alignment = TextAlignmentOptions.MidlineLeft;
        FilaArriba(etiquetaNombre.rectTransform, 50f, 50f, 200f, 50f);

        TMP_InputField campo = CrearCampoTexto(tarjeta);
        FilaArriba((RectTransform)campo.transform, 50f, 50f, 258f, 110f);

        // Rubro
        TextMeshProUGUI etiquetaRubro = CrearTexto("EtiquetaRubro", tarjeta, "¿A qué se dedica?", 34, Tema.texto);
        etiquetaRubro.alignment = TextAlignmentOptions.MidlineLeft;
        FilaArriba(etiquetaRubro.rectTransform, 50f, 50f, 396f, 50f);

        RectTransform grilla = CrearRect("Rubros", tarjeta);
        FilaArriba(grilla, 50f, 50f, 456f, 500f);
        GridLayoutGroup gl = grilla.gameObject.AddComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(400f, 104f);
        gl.spacing = new Vector2(20f, 18f);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = 2;
        gl.childAlignment = TextAnchor.UpperCenter;

        // Plantilla (inactiva) que el script copia una vez por rubro
        RectTransform rtPlantilla = CrearRect("PlantillaRubro", grilla);
        Image imgPlantilla = AgregarImagenRedondeada(rtPlantilla.gameObject, Tema.botonSecundario, 0.3f);
        Button plantilla = rtPlantilla.gameObject.AddComponent<Button>();
        ConfigurarBoton(plantilla, imgPlantilla);
        TextMeshProUGUI textoPlantilla = CrearTexto("Texto", rtPlantilla, "Rubro", 34, Tema.texto);
        Estirar(textoPlantilla.rectTransform);
        rtPlantilla.gameObject.SetActive(false);

        // Ayuda + botón Empezar
        TextMeshProUGUI ayuda = CrearTexto("TextoAyuda", tarjeta, "Escribí el nombre de tu empresa", 28, Tema.textoSuave);
        ayuda.fontStyle = FontStyles.Normal;
        RectTransform rtAyuda = ayuda.rectTransform;
        rtAyuda.anchorMin = new Vector2(0f, 0f);
        rtAyuda.anchorMax = new Vector2(1f, 0f);
        rtAyuda.pivot = new Vector2(0.5f, 0f);
        rtAyuda.offsetMin = new Vector2(40f, 196f);
        rtAyuda.offsetMax = new Vector2(-40f, 240f);

        RectTransform rtBoton = CrearRect("BotonEmpezar", tarjeta);
        rtBoton.anchorMin = rtBoton.anchorMax = new Vector2(0.5f, 0f);
        rtBoton.pivot = new Vector2(0.5f, 0f);
        rtBoton.sizeDelta = new Vector2(560f, 130f);
        rtBoton.anchoredPosition = new Vector2(0f, 50f);
        Image imgBoton = AgregarImagenRedondeada(rtBoton.gameObject, Tema.botonFacturar, 0.3f);
        Button botonEmpezar = rtBoton.gameObject.AddComponent<Button>();
        ConfigurarBoton(botonEmpezar, imgBoton);
        TextMeshProUGUI textoBoton = CrearTexto("Texto", rtBoton, "¡Empezar!", 48, Tema.textoBoton);
        Estirar(textoBoton.rectTransform);

        // Script
        UIBienvenida bienvenida = raiz.gameObject.AddComponent<UIBienvenida>();
        SerializedObject so = new SerializedObject(bienvenida);
        so.FindProperty("panel").objectReferenceValue = panel.gameObject;
        so.FindProperty("tarjeta").objectReferenceValue = tarjeta;
        so.FindProperty("campoNombre").objectReferenceValue = campo;
        so.FindProperty("contenedorRubros").objectReferenceValue = grilla;
        so.FindProperty("plantillaRubro").objectReferenceValue = plantilla;
        so.FindProperty("botonEmpezar").objectReferenceValue = botonEmpezar;
        so.FindProperty("textoAyuda").objectReferenceValue = ayuda;
        so.FindProperty("colorTextoNormal").colorValue = Tema.texto;
        so.FindProperty("colorTextoElegido").colorValue = Tema.textoBoton;
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.gameObject.SetActive(false); // el script lo muestra si hace falta

        GuardarEscenaActiva();
        AssetDatabase.SaveAssets();
        Debug.Log("<color=#D65A92><b>CEO Clicker:</b></color> Paso 13 listo. Rubros en " + RutaRubros +
                  ". Para ver el cartel: Herramientas > Borrar partida guardada y Play.");
    }

    private static RubrosConfig CrearOObtenerRubros()
    {
        RubrosConfig existente = AssetDatabase.LoadAssetAtPath<RubrosConfig>(RutaRubros);
        if (existente != null) return existente; // no pisa lo que editaste a mano

        RubrosConfig config = ScriptableObject.CreateInstance<RubrosConfig>();
        AgregarRubro(config, "tecnologia", "Tecnología", "#8FA3E0", "Programando en un garaje");
        AgregarRubro(config, "pasteleria", "Pastelería", "#F28DB8", "Horneando en un garaje");
        AgregarRubro(config, "moda", "Moda", "#B18AE6", "Cosiendo en un garaje");
        AgregarRubro(config, "cafeteria", "Cafetería", "#D4A373", "Sirviendo cafés en un garaje");
        AgregarRubro(config, "videojuegos", "Videojuegos", "#6CC3D9", "Haciendo juegos en un garaje");
        AgregarRubro(config, "cosmetica", "Cosmética", "#F5A26B", "Mezclando cremas en un garaje");
        AgregarRubro(config, "floreria", "Florería", "#6CCB9F", "Armando ramos en un garaje");
        AgregarRubro(config, "mascotas", "Mascotas", "#E3B23C", "Paseando perros desde un garaje");

        AssetDatabase.CreateAsset(config, RutaRubros);
        AssetDatabase.SaveAssets();
        return config;
    }

    private static void AgregarRubro(RubrosConfig config, string id, string nombre, string colorHex, string etapaInicial)
    {
        config.rubros.Add(new RubrosConfig.Rubro
        {
            id = id,
            nombre = nombre,
            color = Hex(colorHex),
            etapaInicial = etapaInicial
        });
    }

    /// <summary>Campo de texto de TextMeshPro con el estilo del juego.</summary>
    private static TMP_InputField CrearCampoTexto(Transform padre)
    {
        GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = "CampoNombre";
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(padre, false);

        Image fondo = go.GetComponent<Image>();
        fondo.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        fondo.type = Image.Type.Sliced;
        fondo.pixelsPerUnitMultiplier = 0.3f;
        fondo.color = Tema.botonSecundario;

        TMP_InputField campo = go.GetComponent<TMP_InputField>();
        campo.characterLimit = GameManager.MaxLargoNombre;
        campo.lineType = TMP_InputField.LineType.SingleLine;

        RectTransform area = campo.textViewport;
        area.offsetMin = new Vector2(30f, 10f);
        area.offsetMax = new Vector2(-30f, -10f);

        TMP_Text texto = campo.textComponent;
        texto.fontSize = 46f;
        texto.color = Tema.texto;
        texto.alignment = TextAlignmentOptions.MidlineLeft;

        if (campo.placeholder is TMP_Text placeholder)
        {
            placeholder.text = "Ej: Mimi";
            placeholder.fontSize = 46f;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            Color suave = Tema.textoSuave;
            suave.a = 0.6f;
            placeholder.color = suave;
        }

        return campo;
    }

    private static void AsignarSonidoSiFalta(SerializedObject so, string propiedad, string archivo)
    {
        SerializedProperty prop = so.FindProperty(propiedad);
        if (prop == null || prop.objectReferenceValue != null) return;

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(RaizProyecto + "/Audio/" + archivo);
        if (clip != null) prop.objectReferenceValue = clip;
    }

    private static Button CrearBotonAjuste(string nombre, Transform padre, string texto, out TextMeshProUGUI textoBoton)
    {
        RectTransform rt = CrearRect(nombre, padre);
        Image img = AgregarImagenRedondeada(rt.gameObject, Tema.botonContratar, 0.3f);
        Button boton = rt.gameObject.AddComponent<Button>();
        ConfigurarBoton(boton, img);

        textoBoton = CrearTexto("Texto", rt, texto, 32, Tema.textoBoton);
        Estirar(textoBoton.rectTransform);
        return boton;
    }

    private static RectTransform CrearTarjeta(string nombre, Transform padre, float alto)
    {
        RectTransform tarjeta = CrearRect(nombre, padre);
        AgregarImagenRedondeada(tarjeta.gameObject, Tema.tarjeta, 0.4f).raycastTarget = false;
        LayoutElement le = tarjeta.gameObject.AddComponent<LayoutElement>();
        le.minHeight = alto;
        le.preferredHeight = alto;
        return tarjeta;
    }

    /// <summary>Botón a lo ancho, pegado abajo de la tarjeta, con confirmación por doble toque.</summary>
    private static BotonDobleToque CrearBotonDobleToque(string nombre, Transform padre, string texto, Color color,
        Color colorConfirmar, float alto)
    {
        RectTransform rt = CrearRect(nombre, padre);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(32f, 26f);
        rt.offsetMax = new Vector2(-32f, 26f + alto);

        Image img = AgregarImagenRedondeada(rt.gameObject, color, 0.3f);
        Button boton = rt.gameObject.AddComponent<Button>();
        ConfigurarBoton(boton, img);

        TextMeshProUGUI t = CrearTexto("Texto", rt, texto, 34, Tema.textoBoton);
        t.enableAutoSizing = true;
        t.fontSizeMin = 22f;
        t.fontSizeMax = 34f;
        Estirar(t.rectTransform);

        BotonDobleToque doble = rt.gameObject.AddComponent<BotonDobleToque>();
        SerializedObject so = new SerializedObject(doble);
        so.FindProperty("texto").objectReferenceValue = t;
        so.FindProperty("fondo").objectReferenceValue = img;
        so.FindProperty("colorConfirmar").colorValue = colorConfirmar;
        so.ApplyModifiedPropertiesWithoutUndo();
        return doble;
    }

    [MenuItem("CEO Clicker/Herramientas/Abrir carpeta de guardado", priority = 100)]
    public static void AbrirCarpetaGuardado()
    {
        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }

    [MenuItem("CEO Clicker/Herramientas/Borrar partida guardada", priority = 101)]
    public static void BorrarGuardado()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("CEO Clicker", "Frená el Play primero (si no, se vuelve a guardar enseguida).", "OK");
            return;
        }

        if (EditorUtility.DisplayDialog("CEO Clicker", "¿Borrar la partida guardada? No se puede deshacer.", "Borrar", "Cancelar"))
        {
            SistemaGuardado.Borrar();
            Debug.Log("Partida borrada.");
        }
    }

    // =================================================================
    // TEMA: aplica los colores de Datos/Tema y reconstruye la interfaz
    // =================================================================
    [MenuItem("CEO Clicker/Aplicar tema (reconstruir interfaz)", priority = 50)]
    public static void AplicarTema()
    {
        if (!TextMeshProListo()) return;

        SafeArea area = BuscarAreaSegura();
        if (area == null) return;

        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            EditorUtility.DisplayDialog("CEO Clicker", "Primero corré el Paso 2 (falta el GameManager).", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("CEO Clicker",
                "Se va a reconstruir toda la interfaz con los colores de Datos/Tema.\n" +
                "También se recolorean los puestos y departamentos.\n\n¿Seguimos?",
                "Sí, aplicar", "Cancelar"))
        {
            return;
        }

        CrearCarpetas();
        TemaColores tema = Tema;

        // Colores de los datos (puestos y departamentos), en el orden del GameManager
        RecolorearLista(gm, "puestos", tema.coloresPuestos);
        RecolorearLista(gm, "departamentos", tema.coloresDepartamentos);

        // Cámara y fondo
        Camera cam = Object.FindFirstObjectByType<Camera>();
        if (cam != null) cam.backgroundColor = tema.fondo;

        Transform fondo = area.transform.parent != null ? area.transform.parent.Find("Fondo") : null;
        if (fondo != null && fondo.TryGetComponent(out Image imgFondo)) imgFondo.color = tema.fondo;

        // Se rearma la interfaz con los pasos existentes (en orden)
        ConfigurarPaso2();
        ConfigurarPaso3();
        ConfigurarPaso4();
        ConfigurarPaso5();
        ConfigurarPaso6();
        ConfigurarPaso7();
        ConfigurarPaso8();
        ConfigurarPaso9();
        ConfigurarPaso10();
        ConfigurarPaso11();
        ConfigurarPaso13();

        AssetDatabase.SaveAssets();
        Selection.activeObject = tema;
        Debug.Log("<color=#D65A92><b>CEO Clicker:</b></color> Tema aplicado. Podés editar los colores en " + RutaTema +
                  " y volver a usar este menú.");
    }

    private static void RecolorearLista(GameManager gm, string propiedad, Color[] colores)
    {
        SerializedObject so = new SerializedObject(gm);
        SerializedProperty lista = so.FindProperty(propiedad);
        if (lista == null || colores == null) return;

        for (int i = 0; i < lista.arraySize && i < colores.Length; i++)
        {
            Object dato = lista.GetArrayElementAtIndex(i).objectReferenceValue;
            if (dato == null) continue;

            SerializedObject soDato = new SerializedObject(dato);
            SerializedProperty color = soDato.FindProperty("color");
            if (color == null) continue;
            color.colorValue = colores[i];
            soDato.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dato);
        }
    }

    private static TemaColores CargarOCrearTema()
    {
        TemaColores tema = AssetDatabase.LoadAssetAtPath<TemaColores>(RutaTema);
        if (tema != null) return tema;

        CrearCarpeta(RaizProyecto + "/Datos");
        tema = ScriptableObject.CreateInstance<TemaColores>(); // valores por defecto = paleta pastel
        AssetDatabase.CreateAsset(tema, RutaTema);
        AssetDatabase.SaveAssets();
        return tema;
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
        cam.backgroundColor = Tema.fondo;

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
        imgFondo.color = Tema.fondo;
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
        TextMeshProUGUI texto = CrearTexto("TextoPlata", padre, "$0", 110, Tema.plata);

        // Arriba de todo, a lo ancho, con 40 px de margen a cada lado
        RectTransform rt = texto.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-80f, 160f);
        rt.anchoredPosition = new Vector2(0f, -80f);
        // (Provisorio: en el Paso 4 lo reemplaza la barra superior)
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

        Image sombra = AgregarImagenRedondeada(rtRaiz.gameObject, Tema.botonFacturarSombra, 0.25f);

        // Cara: igual de grande, pero levantada "hundimiento" píxeles
        RectTransform rtCara = CrearHijoEstirado("Cara", rtRaiz);
        rtCara.offsetMin = new Vector2(0f, hundimiento);
        rtCara.offsetMax = new Vector2(0f, hundimiento);

        Image cara = AgregarImagenRedondeada(rtCara.gameObject, Tema.botonFacturar, 0.25f);
        cara.raycastTarget = false; // el toque lo recibe la raíz

        TextMeshProUGUI texto = CrearTexto("Texto", rtCara, "Facturar", 100, Tema.textoBoton);
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
        p.color = orden - 1 < Tema.coloresPuestos.Length ? Tema.coloresPuestos[orden - 1] : Hex(colorHex);
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
        AgregarImagenRedondeada(panel.gameObject, Tema.panel, 0.4f).raycastTarget = false;

        TextMeshProUGUI texto = CrearTexto("TextoProvisorio", panel, "Organigrama (Paso 7)", 40, Tema.textoSuave);
        texto.fontStyle = FontStyles.Normal;
        Estirar(texto.rectTransform);
    }

    private static void CrearPanelPestanas(Transform padre, UIPuestoItem prefab)
    {
        RectTransform panel = CrearRect("PanelPestanas", padre);
        Ubicar(panel, new Vector2(0f, 0f), new Vector2(1f, 0.46f), new Vector2(24f, 24f), new Vector2(-24f, -12f));
        AgregarImagenRedondeada(panel.gameObject, Tema.panel, 0.4f).raycastTarget = false;

        // --- Panel Personal ---
        RectTransform personal = CrearHijoEstirado("PanelPersonal", panel);

        // Encabezado: título + selector x1/x10/Máx
        RectTransform encabezado = CrearRect("Encabezado", personal);
        Ubicar(encabezado, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -110f), new Vector2(0f, 0f));

        TextMeshProUGUI titulo = CrearTexto("Titulo", encabezado, "Personal", 46, Tema.texto);
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
        so.FindProperty("colorSeleccionado").colorValue = Tema.seleccion;
        so.FindProperty("colorNormal").colorValue = Tema.botonSecundario;
        so.FindProperty("textoSeleccionado").colorValue = Tema.textoBoton;
        so.FindProperty("textoNormal").colorValue = Tema.texto;
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

        AgregarImagenRedondeada(raiz, Tema.tarjeta, 0.4f).raycastTarget = false;
        LayoutElement le = raiz.AddComponent<LayoutElement>();
        le.minHeight = alto;
        le.preferredHeight = alto;

        // Insignia de color con la inicial
        RectTransform rtInsignia = CrearRect("Insignia", rtRaiz);
        rtInsignia.anchorMin = rtInsignia.anchorMax = new Vector2(0f, 0.5f);
        rtInsignia.pivot = new Vector2(0f, 0.5f);
        rtInsignia.sizeDelta = new Vector2(140f, 140f);
        rtInsignia.anchoredPosition = new Vector2(24f, 0f);
        Image insignia = AgregarImagenRedondeada(rtInsignia.gameObject, Tema.bloqueado, 0.3f);
        insignia.raycastTarget = false;

        TextMeshProUGUI inicial = CrearTexto("Inicial", rtInsignia, "E", 56, Tema.textoBoton);
        Estirar(inicial.rectTransform);

        // Textos del medio
        TextMeshProUGUI nombre = CrearTexto("TextoNombre", rtRaiz, "Puesto", 42, Tema.texto);
        nombre.alignment = TextAlignmentOptions.MidlineLeft;
        nombre.overflowMode = TextOverflowModes.Ellipsis;
        FilaArriba(nombre.rectTransform, izquierdaTextos, derechaTextos, 22f, 54f);

        TextMeshProUGUI cantidad = CrearTexto("TextoCantidad", rtRaiz, "0 / 3", 36, Tema.texto);
        cantidad.alignment = TextAlignmentOptions.MidlineRight;
        FilaArriba(cantidad.rectTransform, izquierdaTextos, derechaTextos, 22f, 54f);

        TextMeshProUGUI detalle = CrearTexto("TextoDetalle", rtRaiz, "+$0/s c/u", 26, Tema.plata);
        detalle.alignment = TextAlignmentOptions.MidlineLeft;
        detalle.overflowMode = TextOverflowModes.Ellipsis;
        detalle.enableAutoSizing = true; // achica la letra si no entra (ej: "Supervisores")
        detalle.fontSizeMin = 18f;
        detalle.fontSizeMax = 26f;
        FilaArriba(detalle.rectTransform, izquierdaTextos, derechaTextos, 80f, 40f);

        TextMeshProUGUI descripcion = CrearTexto("TextoDescripcion", rtRaiz, "Descripción", 26, Tema.textoSuave);
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
        Image imgBoton = AgregarImagenRedondeada(rtBoton.gameObject, Tema.botonContratar, 0.3f);
        Button boton = rtBoton.gameObject.AddComponent<Button>();
        ConfigurarBoton(boton, imgBoton);

        TextMeshProUGUI textoBoton = CrearTexto("TextoBoton", rtBoton, "Contratar x1\n$15", 30, Tema.textoBoton);
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
        so.FindProperty("colorBloqueado").colorValue = Tema.bloqueado;
        so.FindProperty("colorDescripcion").colorValue = Tema.textoSuave;
        so.FindProperty("colorAviso").colorValue = Tema.aviso;
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
        AgregarImagenRedondeada(barra.gameObject, Tema.panel, 0.4f).raycastTarget = false;

        // Izquierda: etapa y nombre de empresa (en el Paso 6 se vuelven dinámicos)
        TextMeshProUGUI etapa = CrearTexto("TextoEtapa", barra, "Freelancer en un garaje", 28, Tema.acento);
        etapa.alignment = TextAlignmentOptions.MidlineLeft;
        etapa.overflowMode = TextOverflowModes.Ellipsis;
        etapa.textWrappingMode = TextWrappingModes.NoWrap; // una sola línea
        etapa.enableAutoSizing = true;
        etapa.fontSizeMin = 16f;
        etapa.fontSizeMax = 28f;
        FilaArriba(etapa.rectTransform, 32f, 520f, 30f, 40f);

        TextMeshProUGUI empresa = CrearTexto("TextoEmpresa", barra, "Garaje Unipersonal", 44, Tema.texto);
        empresa.alignment = TextAlignmentOptions.MidlineLeft;
        empresa.overflowMode = TextOverflowModes.Ellipsis;
        empresa.enableAutoSizing = true;
        empresa.fontSizeMin = 30f;
        empresa.fontSizeMax = 44f;
        FilaArriba(empresa.rectTransform, 32f, 520f, 72f, 64f);

        // Derecha: plata y $/seg
        TextMeshProUGUI plata = CrearTexto("TextoPlata", barra, "$0", 80, Tema.plata);
        plata.alignment = TextAlignmentOptions.MidlineRight;
        plata.enableAutoSizing = true;
        plata.fontSizeMin = 50f;
        plata.fontSizeMax = 80f;
        FilaArriba(plata.rectTransform, 500f, 32f, 18f, 96f);

        TextMeshProUGUI porSegundo = CrearTexto("TextoPorSegundo", barra, "+$0/s", 32, Tema.textoSuave);
        porSegundo.alignment = TextAlignmentOptions.MidlineRight;
        FilaArriba(porSegundo.rectTransform, 500f, 32f, 112f, 44f);

        // Abajo: barra de progreso de etapa (en el Paso 6 se llena)
        RectTransform fondoBarra = CrearRect("BarraProgreso", barra);
        fondoBarra.anchorMin = new Vector2(0f, 0f);
        fondoBarra.anchorMax = new Vector2(1f, 0f);
        fondoBarra.pivot = new Vector2(0.5f, 0f);
        fondoBarra.offsetMin = new Vector2(32f, 26f);
        fondoBarra.offsetMax = new Vector2(-32f, 48f); // 22 px de alto
        AgregarImagenRedondeada(fondoBarra.gameObject, Tema.barraFondo, 1f).raycastTarget = false;

        RectTransform relleno = CrearRect("Relleno", fondoBarra);
        relleno.anchorMin = Vector2.zero;
        relleno.anchorMax = new Vector2(0f, 1f); // el ancho = progreso (0 a 1)
        relleno.pivot = new Vector2(0f, 0.5f);
        relleno.offsetMin = Vector2.zero;
        relleno.offsetMax = Vector2.zero;
        AgregarImagenRedondeada(relleno.gameObject, Tema.barraRelleno, 1f).raycastTarget = false;

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
        AgregarImagenRedondeada(chip.gameObject, Tema.panel, 0.3f).raycastTarget = false;

        TextMeshProUGUI t = CrearTexto("Texto", chip, nombre + " <b>0</b>", 30, Tema.texto);
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
        d.color = orden - 1 < Tema.coloresDepartamentos.Length ? Tema.coloresDepartamentos[orden - 1] : Hex(colorHex);
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

        TextMeshProUGUI titulo = CrearTexto("Titulo", encabezado, texto, 46, Tema.texto);
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

        AgregarImagenRedondeada(raiz, Tema.tarjeta, 0.4f).raycastTarget = false;
        LayoutElement le = raiz.AddComponent<LayoutElement>();
        le.minHeight = alto;
        le.preferredHeight = alto;

        // Insignia con la sigla
        RectTransform rtInsignia = CrearRect("Insignia", rtRaiz);
        rtInsignia.anchorMin = rtInsignia.anchorMax = new Vector2(0f, 0.5f);
        rtInsignia.pivot = new Vector2(0f, 0.5f);
        rtInsignia.sizeDelta = new Vector2(140f, 140f);
        rtInsignia.anchoredPosition = new Vector2(24f, 0f);
        Image insignia = AgregarImagenRedondeada(rtInsignia.gameObject, Tema.bloqueado, 0.3f);
        insignia.raycastTarget = false;

        TextMeshProUGUI sigla = CrearTexto("Sigla", rtInsignia, "RRHH", 44, Tema.textoBoton);
        sigla.enableAutoSizing = true;
        sigla.fontSizeMin = 24f;
        sigla.fontSizeMax = 44f;
        Estirar(sigla.rectTransform);
        sigla.margin = new Vector4(8f, 0f, 8f, 0f);

        // Textos del medio
        TextMeshProUGUI nombre = CrearTexto("TextoNombre", rtRaiz, "Departamento", 40, Tema.texto);
        nombre.alignment = TextAlignmentOptions.MidlineLeft;
        nombre.overflowMode = TextOverflowModes.Ellipsis;
        nombre.enableAutoSizing = true;
        nombre.fontSizeMin = 30f;
        nombre.fontSizeMax = 40f;
        FilaArriba(nombre.rectTransform, izquierdaTextos, derechaTextos + 150f, 22f, 54f);

        TextMeshProUGUI nivel = CrearTexto("TextoNivel", rtRaiz, "Nv. 0", 34, Tema.texto);
        nivel.alignment = TextAlignmentOptions.MidlineRight;
        FilaArriba(nivel.rectTransform, izquierdaTextos, derechaTextos, 22f, 54f);

        TextMeshProUGUI detalle = CrearTexto("TextoDetalle", rtRaiz, "Ahora: ×1", 26, Tema.plata);
        detalle.alignment = TextAlignmentOptions.MidlineLeft;
        detalle.overflowMode = TextOverflowModes.Ellipsis;
        detalle.enableAutoSizing = true;
        detalle.fontSizeMin = 18f;
        detalle.fontSizeMax = 26f;
        FilaArriba(detalle.rectTransform, izquierdaTextos, derechaTextos, 80f, 40f);

        TextMeshProUGUI descripcion = CrearTexto("TextoDescripcion", rtRaiz, "Descripción", 24, Tema.textoSuave);
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
        Image imgBoton = AgregarImagenRedondeada(rtBoton.gameObject, Tema.botonMejorar, 0.3f);
        Button boton = rtBoton.gameObject.AddComponent<Button>();
        ConfigurarBoton(boton, imgBoton);

        TextMeshProUGUI textoBoton = CrearTexto("TextoBoton", rtBoton, "Mejorar\n$60", 30, Tema.textoBoton);
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

        Image fondo = AgregarImagenRedondeada(raiz, Tema.tarjeta, 0.4f);
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
        marcador.color = Tema.acento;
        marcador.raycastTarget = false;

        TextMeshProUGUI numero = CrearTexto("Numero", rtMarcador, "1", 34, Tema.textoBoton);
        Estirar(numero.rectTransform);

        // Textos
        TextMeshProUGUI etapa = CrearTexto("TextoEtapa", rtRaiz, "Etapa", 34, Tema.texto);
        etapa.alignment = TextAlignmentOptions.MidlineLeft;
        etapa.overflowMode = TextOverflowModes.Ellipsis;
        FilaArriba(etapa.rectTransform, 120f, 260f, 14f, 48f);

        TextMeshProUGUI empresa = CrearTexto("TextoEmpresa", rtRaiz, "Empresa", 26, Tema.textoSuave);
        empresa.fontStyle = FontStyles.Normal;
        empresa.alignment = TextAlignmentOptions.MidlineLeft;
        empresa.overflowMode = TextOverflowModes.Ellipsis;
        FilaArriba(empresa.rectTransform, 120f, 260f, 62f, 40f);

        TextMeshProUGUI meta = CrearTexto("TextoMeta", rtRaiz, "$0", 30, Tema.acento);
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
        so.FindProperty("fondoNormal").colorValue = Tema.tarjeta;
        so.FindProperty("fondoActual").colorValue = Tema.fondoEtapaActual;
        so.FindProperty("colorLograda").colorValue = Tema.etapaLograda;
        so.FindProperty("colorActual").colorValue = Tema.etapaActual;
        so.FindProperty("colorFutura").colorValue = Tema.bloqueado;
        so.FindProperty("colorTexto").colorValue = Tema.texto;
        so.FindProperty("colorTextoSuave").colorValue = Tema.textoSuave;
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
        Image img = AgregarImagenRedondeada(rt.gameObject, Tema.botonSecundario, 0.3f);
        Button b = rt.gameObject.AddComponent<Button>();
        ConfigurarBoton(b, img);

        TextMeshProUGUI t = CrearTexto("Texto", rt, texto, 36, Tema.texto);
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
        // "Background" es el redondeado liso de Unity (sin el borde gris de "UISprite")
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
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
