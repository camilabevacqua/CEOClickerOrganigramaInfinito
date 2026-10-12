using System;
using UnityEngine;

/// <summary>
/// Sonidos y vibración. Escucha los eventos del GameManager (facturar, contratar,
/// mejorar, nueva etapa, salir a la bolsa) y reproduce el sonido que corresponda.
/// Los AudioClips se cambian desde el Inspector. Sonido y vibración se pueden
/// apagar desde la pestaña Empresa (se guardan en PlayerPrefs).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class Feedback : MonoBehaviour
{
    private const string ClaveSonido = "cfg_sonido";
    private const string ClaveVibracion = "cfg_vibracion";

    public static Feedback Instance { get; private set; }

    [Header("Sonidos (arrastrá tus AudioClips; podés dejar alguno vacío)")]
    [SerializeField] private AudioClip sonidoFacturar;
    [SerializeField] private AudioClip sonidoContratar;
    [SerializeField] private AudioClip sonidoMejorar;
    [SerializeField] private AudioClip sonidoEtapa;
    [SerializeField] private AudioClip sonidoBolsa;
    [Range(0f, 1f)] [SerializeField] private float volumen = 0.8f;
    [Tooltip("Variación de tono al facturar, para que no suene siempre igual.")]
    [Range(0f, 0.3f)] [SerializeField] private float variacionTono = 0.08f;

    [Header("Vibración (solo Android, en milisegundos)")]
    [SerializeField] private int vibracionFacturar = 12;
    [SerializeField] private int vibracionContratar = 30;
    [SerializeField] private int vibracionBolsa = 120;

    public bool SonidoActivado { get; private set; } = true;
    public bool VibracionActivada { get; private set; } = true;

    /// <summary>Avisa a la UI de ajustes que cambió algo.</summary>
    public event Action OnAjustesCambiados;

    // Nunca es true: solo sirve para que Unity agregue el permiso VIBRATE al build de Android
    private static readonly bool forzarPermisoVibracion = false;

    private AudioSource fuente;
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

        fuente = GetComponent<AudioSource>();
        fuente.playOnAwake = false;

        SonidoActivado = PlayerPrefs.GetInt(ClaveSonido, 1) == 1;
        VibracionActivada = PlayerPrefs.GetInt(ClaveVibracion, 1) == 1;

        PrepararVibrador();
    }

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        gm.OnFacturo += AlFacturar;
        gm.OnContrato += AlContratar;
        gm.OnDepartamentoMejorado += AlMejorar;
        gm.OnCambioEtapa += AlCambiarEtapa;
        gm.OnSalidaABolsa += AlSalirABolsa;
        suscripto = true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (!suscripto || GameManager.Instance == null) return;

        GameManager gm = GameManager.Instance;
        gm.OnFacturo -= AlFacturar;
        gm.OnContrato -= AlContratar;
        gm.OnDepartamentoMejorado -= AlMejorar;
        gm.OnCambioEtapa -= AlCambiarEtapa;
        gm.OnSalidaABolsa -= AlSalirABolsa;
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
