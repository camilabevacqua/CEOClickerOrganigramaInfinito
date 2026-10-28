using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Música de fondo, sonidos y vibración. Escucha los eventos del GameManager
/// (facturar, contratar, mejorar, nueva etapa, salir a la bolsa, fundar la empresa,
/// ganancias offline) y le agrega un "tic" a los demás botones de la interfaz.
/// Los AudioClips se cambian desde el Inspector. Música, sonido y vibración se
/// pueden apagar desde la pestaña Empresa (se guardan en PlayerPrefs).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class Feedback : MonoBehaviour
{
    private const string ClaveSonido = "cfg_sonido";
    private const string ClaveMusica = "cfg_musica";
    private const string ClaveVibracion = "cfg_vibracion";

    public static Feedback Instance { get; private set; }

    [Header("Música (se repite en loop)")]
    [SerializeField] private AudioClip musica;
    [Range(0f, 1f)] [SerializeField] private float volumenMusica = 0.35f;
    [Tooltip("Segundos que tarda en subir el volumen al empezar.")]
    [SerializeField] private float fundidoMusica = 2f;

    [Header("Sonidos (arrastrá tus AudioClips; podés dejar alguno vacío)")]
    [SerializeField] private AudioClip sonidoFacturar;
    [SerializeField] private AudioClip sonidoContratar;
    [SerializeField] private AudioClip sonidoMejorar;
    [SerializeField] private AudioClip sonidoEtapa;
    [SerializeField] private AudioClip sonidoBolsa;
    [SerializeField] private AudioClip sonidoFundar;
    [SerializeField] private AudioClip sonidoOffline;
    [Tooltip("\"Tic\" de los demás botones (pestañas, x1/x10/Máx, ajustes, etc.).")]
    [SerializeField] private AudioClip sonidoToque;
    [Range(0f, 1f)] [SerializeField] private float volumen = 0.8f;
    [Tooltip("Variación de tono al facturar, para que no suene siempre igual.")]
    [Range(0f, 0.3f)] [SerializeField] private float variacionTono = 0.08f;

    [Header("Vibración (solo Android, en milisegundos)")]
    [SerializeField] private int vibracionFacturar = 12;
    [SerializeField] private int vibracionContratar = 30;
    [SerializeField] private int vibracionBolsa = 120;

    public bool SonidoActivado { get; private set; } = true;
    public bool MusicaActivada { get; private set; } = true;
    public bool VibracionActivada { get; private set; } = true;

    /// <summary>Avisa a la UI de ajustes que cambió algo.</summary>
    public event Action OnAjustesCambiados;

    // Nunca es true: solo sirve para que Unity agregue el permiso VIBRATE al build de Android
    private static readonly bool forzarPermisoVibracion = false;

    private AudioSource fuente;        // sonidos
    private AudioSource fuenteMusica;  // música
    private Coroutine fundido;
    private bool suscripto;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject vibrador;
    private int versionSdk;
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Dos AudioSource: uno para sonidos sueltos y otro para la música en loop
        AudioSource[] fuentes = GetComponents<AudioSource>();
        fuente = fuentes[0];
        fuenteMusica = fuentes.Length > 1 ? fuentes[1] : gameObject.AddComponent<AudioSource>();

        fuente.playOnAwake = false;
        fuenteMusica.playOnAwake = false;
        fuenteMusica.loop = true;
        fuenteMusica.priority = 0; // la música nunca se corta por falta de canales

        SonidoActivado = PlayerPrefs.GetInt(ClaveSonido, 1) == 1;
        MusicaActivada = PlayerPrefs.GetInt(ClaveMusica, 1) == 1;
        VibracionActivada = PlayerPrefs.GetInt(ClaveVibracion, 1) == 1;

        PrepararVibrador();
    }

    private void Start()
    {
        if (MusicaActivada) EmpezarMusica();

        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OnFacturo += AlFacturar;
            gm.OnContrato += AlContratar;
            gm.OnDepartamentoMejorado += AlMejorar;
            gm.OnPersonalDepartamentoContratado += AlContratarPersonal;
            gm.OnCambioEtapa += AlCambiarEtapa;
            gm.OnSalidaABolsa += AlSalirABolsa;
            gm.OnEmpresaFundada += AlFundarEmpresa;
            gm.OnGananciaOffline += AlGanarOffline;
            suscripto = true;

            // La carga (y la ganancia offline) pasa en el Awake del GameManager, antes de este Start
            if (gm.HayAvisoOfflinePendiente) Reproducir(sonidoOffline, false);
        }

        // Las listas crean sus botones en Start: esperamos un frame para engancharlos a todos
        StartCoroutine(EngancharBotonesDespues());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (!suscripto || GameManager.Instance == null) return;

        GameManager gm = GameManager.Instance;
        gm.OnFacturo -= AlFacturar;
        gm.OnContrato -= AlContratar;
        gm.OnDepartamentoMejorado -= AlMejorar;
        gm.OnPersonalDepartamentoContratado -= AlContratarPersonal;
        gm.OnCambioEtapa -= AlCambiarEtapa;
        gm.OnSalidaABolsa -= AlSalirABolsa;
        gm.OnEmpresaFundada -= AlFundarEmpresa;
        gm.OnGananciaOffline -= AlGanarOffline;
    }

    // ------------------------------------------------------------------
    // Reacciones a eventos del juego
    // ------------------------------------------------------------------
    private void AlFacturar(double valor)
    {
        Reproducir(sonidoFacturar, true);
        Vibrar(vibracionFacturar);
    }

    private void AlContratar(PuestoData puesto, int cantidad)
    {
        Reproducir(sonidoContratar, false);
        Vibrar(vibracionContratar);
    }

    private void AlMejorar(DepartamentoData departamento)
    {
        Reproducir(sonidoMejorar, false);
        Vibrar(vibracionContratar);
    }

    private void AlContratarPersonal(DepartamentoData departamento, RolDepartamento rol)
    {
        Reproducir(sonidoContratar, false);
        Vibrar(vibracionContratar);
    }

    private void AlCambiarEtapa(int indice)
    {
        Reproducir(sonidoEtapa, false);
        Vibrar(vibracionBolsa);
    }

    private void AlSalirABolsa(int acciones)
    {
        Reproducir(sonidoBolsa, false);
        Vibrar(vibracionBolsa);
    }

    private void AlFundarEmpresa()
    {
        Reproducir(sonidoFundar, false);
        Vibrar(vibracionBolsa);
    }

    private void AlGanarOffline(double monto, double segundos)
    {
        Reproducir(sonidoOffline, false);
    }

    // ------------------------------------------------------------------
    // "Tic" en los botones que no tienen sonido propio
    // ------------------------------------------------------------------
    private IEnumerator EngancharBotonesDespues()
    {
        yield return null;

        Button[] botones = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button boton in botones)
        {
            // Facturar, Contratar y Mejorar ya suenan con los eventos del juego
            if (boton.GetComponent<UIBotonFacturar>() != null) continue;
            if (boton.GetComponentInParent<UIPuestoItem>(true) != null) continue;
            if (boton.GetComponentInParent<UIDepartamentoItem>(true) != null) continue;

            boton.onClick.AddListener(Toque);
        }
    }

    /// <summary>Sonido corto de botón. Se puede llamar desde cualquier script.</summary>
    public void Toque()
    {
        Reproducir(sonidoToque, false);
    }

    // ------------------------------------------------------------------
    // Sonido
    // ------------------------------------------------------------------
    public void Reproducir(AudioClip clip, bool variarTono)
    {
        if (!SonidoActivado || clip == null || fuente == null) return;

        fuente.pitch = variarTono ? 1f + UnityEngine.Random.Range(-variacionTono, variacionTono) : 1f;
        fuente.PlayOneShot(clip, volumen);
    }

    public void CambiarSonido(bool activado)
    {
        SonidoActivado = activado;
        PlayerPrefs.SetInt(ClaveSonido, activado ? 1 : 0);
        PlayerPrefs.Save();
        OnAjustesCambiados?.Invoke();
    }

    // ------------------------------------------------------------------
    // Música
    // ------------------------------------------------------------------
    public void CambiarMusica(bool activada)
    {
        MusicaActivada = activada;
        PlayerPrefs.SetInt(ClaveMusica, activada ? 1 : 0);
        PlayerPrefs.Save();

        if (activada) EmpezarMusica();
        else FrenarMusica();

        OnAjustesCambiados?.Invoke();
    }

    private void EmpezarMusica()
    {
        if (musica == null || fuenteMusica == null) return;

        if (fuenteMusica.clip != musica) fuenteMusica.clip = musica;
        if (!fuenteMusica.isPlaying)
        {
            fuenteMusica.volume = 0f;
            fuenteMusica.Play();
        }
        Fundir(volumenMusica, fundidoMusica, false);
    }

    private void FrenarMusica()
    {
        if (fuenteMusica == null || !fuenteMusica.isPlaying) return;
        Fundir(0f, 0.4f, true);
    }

    private void Fundir(float volumenFinal, float duracion, bool pararAlFinal)
    {
        if (fundido != null) StopCoroutine(fundido);
        fundido = StartCoroutine(FundidoMusica(volumenFinal, duracion, pararAlFinal));
    }

    private IEnumerator FundidoMusica(float volumenFinal, float duracion, bool pararAlFinal)
    {
        float inicio = fuenteMusica.volume;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            fuenteMusica.volume = Mathf.Lerp(inicio, volumenFinal, t / duracion);
            yield return null;
        }

        fuenteMusica.volume = volumenFinal;
        if (pararAlFinal) fuenteMusica.Stop();
        fundido = null;
    }

    // ------------------------------------------------------------------
    // Vibración (Android): vibración corta con la API nativa,
    // porque Handheld.Vibrate() es demasiado larga para cada toque
    // ------------------------------------------------------------------
    public void CambiarVibracion(bool activada)
    {
        VibracionActivada = activada;
        PlayerPrefs.SetInt(ClaveVibracion, activada ? 1 : 0);
        PlayerPrefs.Save();
        OnAjustesCambiados?.Invoke();

        if (activada) Vibrar(vibracionContratar); // para sentir que se prendió
    }

    private void PrepararVibrador()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject actividad = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                vibrador = actividad.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }

            using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                versionSdk = version.GetStatic<int>("SDK_INT");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("No se pudo preparar la vibración: " + ex.Message);
            vibrador = null;
        }
#endif
    }

    public void Vibrar(int milisegundos)
    {
        if (!VibracionActivada || milisegundos <= 0) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (vibrador == null) return;

        try
        {
            if (versionSdk >= 26)
            {
                using (AndroidJavaClass efectoVibracion = new AndroidJavaClass("android.os.VibrationEffect"))
                {
                    // -1 = DEFAULT_AMPLITUDE
                    AndroidJavaObject efecto = efectoVibracion.CallStatic<AndroidJavaObject>("createOneShot", (long)milisegundos, -1);
                    vibrador.Call("vibrate", efecto);
                }
            }
            else
            {
                vibrador.Call("vibrate", (long)milisegundos);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("No se pudo vibrar: " + ex.Message);
        }
#endif

        if (forzarPermisoVibracion) Handheld.Vibrate();
    }
}
