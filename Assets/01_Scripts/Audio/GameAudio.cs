using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sonido del juego. Se instala solo al arrancar y en cada escena:
///  - pasos, salto, aterrizaje, golpe y muerte del jugador (PlayerSounds)
///  - crepitar de cada antorcha (sonido 3D en bucle)
///  - ambiente de mazmorra de fondo
/// Los demás scripts llaman a GameAudio.Llave(), GameAudio.Puerta(pos), etc.
/// </summary>
public class GameAudio : MonoBehaviour
{
    static GameAudio instance;
    static SoundLibrary lib;

    public static SoundLibrary Library
    {
        get
        {
            if (lib == null) lib = Resources.Load<SoundLibrary>("SoundLibrary");
            return lib;
        }
    }

    readonly List<AudioSource> pool = new List<AudioSource>();
    AudioSource ambience;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        instance = null;
        lib = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static GameAudio Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("GameAudio");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GameAudio>();
            }
            return instance;
        }
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Library == null)
        {
            Debug.LogWarning("GameAudio: falta Resources/SoundLibrary (menú Juego > Configurar sonidos).");
            return;
        }
        Instance.Setup();
    }

    void Setup()
    {
        // Un solo oído: el de la cámara principal.
        Camera main = Camera.main;
        foreach (AudioListener l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            l.enabled = main != null && l.gameObject == main.gameObject;
        if (main != null && main.GetComponent<AudioListener>() == null)
            main.gameObject.AddComponent<AudioListener>();

        // Jugador.
        GameObject player = GameObject.Find("Player");
        if (player != null && player.GetComponent<PlayerSounds>() == null)
            player.AddComponent<PlayerSounds>();

        // Antorchas.
        if (lib.antorcha != null)
            foreach (TorchFlicker torch in FindObjectsByType<TorchFlicker>(FindObjectsSortMode.None))
            {
                if (torch.name != "LuzAntorcha" || torch.GetComponent<AudioSource>() != null) continue;
                AudioSource s = torch.gameObject.AddComponent<AudioSource>();
                s.clip = lib.antorcha;
                s.loop = true;
                s.volume = lib.volumenAntorcha;
                s.pitch = Random.Range(0.9f, 1.1f);
                Make3D(s, 0.6f, 6.5f);
                s.time = Random.Range(0f, lib.antorcha.length * 0.9f);
                s.Play();
            }

        // Ambiente de fondo (continúa entre escenas).
        if (lib.ambiente != null)
        {
            if (ambience == null)
            {
                ambience = gameObject.AddComponent<AudioSource>();
                ambience.clip = lib.ambiente;
                ambience.loop = true;
                ambience.spatialBlend = 0f;
                ambience.ignoreListenerPause = true;
            }
            ambience.volume = lib.volumenAmbiente;
            if (!ambience.isPlaying) ambience.Play();
        }
    }

    static void Make3D(AudioSource s, float min, float max)
    {
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = min;
        s.maxDistance = max;
        s.dopplerLevel = 0f;
        s.playOnAwake = false;
    }

    void Update()
    {
        // En pausa se callan los efectos (el ambiente sigue).
        AudioListener.pause = GameState.Paused;
    }

    // ------------------------------------------------------------ reproducir

    AudioSource Free()
    {
        foreach (AudioSource s in pool)
            if (!s.isPlaying) return s;
        GameObject go = new GameObject("Sonido");
        go.transform.SetParent(transform, false);
        AudioSource n = go.AddComponent<AudioSource>();
        n.playOnAwake = false;
        pool.Add(n);
        return n;
    }

    /// Sonido suelto. Con posición suena en 3D; sin ella, "en la cabeza".
    public static AudioSource Play(AudioClip clip, Vector3? position = null, float volume = 1f,
        float pitchJitter = 0.05f, float maxDistance = 18f)
    {
        if (clip == null) return null;
        AudioSource s = Instance.Free();
        s.clip = clip;
        s.volume = volume;
        s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        s.loop = false;
        if (position.HasValue)
        {
            s.transform.position = position.Value;
            Make3D(s, 1.2f, maxDistance);
        }
        else
        {
            s.spatialBlend = 0f;
        }
        s.Play();
#if UNITY_EDITOR
        if (System.IO.File.Exists("Diag/registrar"))
            System.IO.File.AppendAllText("Diag/sonidos.txt",
                Time.time.ToString("0.0") + " " + clip.name + (position.HasValue ? " 3D" : "") + "\n");
#endif
        return s;
    }

    public static AudioClip Pick(AudioClip[] clips, ref int last)
    {
        if (clips == null || clips.Length == 0) return null;
        int i = clips.Length == 1 ? 0 : Random.Range(0, clips.Length - 1);
        if (i >= last && clips.Length > 1) i++;   // nunca el mismo dos veces seguidas
        last = i;
        return clips[i];
    }

    // ------------------------------------------------------------ atajos

    public static void Llave() { if (Library) Play(lib.llave, null, 0.8f, 0f); }
    public static void Puerta(Vector3 at) { if (Library) Play(lib.puerta, at, 1f, 0.04f, 22f); }
    public static void Terror() { if (Library) Play(lib.terror, null, 0.9f, 0f); }
    public static void TrampaCepo(Vector3 at) { if (Library) Play(lib.trampaCepo, at, 1f, 0.06f); }
    public static void ManoSale(Vector3 at) { if (Library) Play(lib.manoSale, at, 1f, 0f, 20f); }
    public static void ManoAgarra(Vector3 at) { if (Library) Play(lib.manoAgarra, at, 1f, 0f, 20f); }
    public static void ManoArrastra(Vector3 at) { if (Library) Play(lib.manoArrastra, at, 1f, 0f, 20f); }
    public static void TrampaSuelo(Vector3 at) { if (Library) Play(lib.trampaSuelo, at, 1f, 0.03f, 25f); }
}
