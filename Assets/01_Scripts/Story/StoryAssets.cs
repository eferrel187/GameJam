using UnityEngine;

/// <summary>
/// Recursos de la historia (prefabs del proyecto y sonidos de 07_sonido/historia).
/// Lo rellena  Juego > Preparar historia (niveles 4-6)  y se carga desde Resources,
/// así las reglas de los niveles funcionan también en el Build.
/// </summary>
public class StoryAssets : ScriptableObject
{
    [Header("Jugador para niveles sin personaje")]
    public GameObject playerRig;

    [Header("Puertas estilo Nivel 2")]
    public GameObject doorLeft;
    public GameObject doorRight;
    public GameObject doorFrame;

    [Header("Señales y decoración")]
    public GameObject skull;
    public GameObject[] brokenPots;
    public GameObject bookcaseFull;
    public GameObject bookcaseEmpty;
    public GameObject key;
    public GameObject monsterHand;
    public GameObject hazardCylinder;
    public GameObject spikesPlatform;
    public GameObject[] trees;
    public GameObject[] bushes;
    public GameObject grass;
    public GameObject candles;
    public Material wood;
    public Material darkWood;
    public Material darkness;

    [Header("Sonidos")]
    public AudioClip maullido;
    public AudioClip maullidoSuave;
    public AudioClip maullidoAsustado;
    public AudioClip ronroneo;
    public AudioClip musicaBaile;
    public AudioClip ambienteCampo;
    public AudioClip musicaTranquila;
    public AudioClip latido;
    public AudioClip zumbidoMano;
    public AudioClip estatuaGira;
    public AudioClip acierto;
    public AudioClip papel;
    public AudioClip crujido;
    public AudioClip rotor;
    public AudioClip jaulaRompe;
    public AudioClip serpienteSiseo;
    public AudioClip serpienteMordida;
    public AudioClip tic;
    public AudioClip libros;
    public AudioClip golpePuerta;

    static StoryAssets cached;

    public static StoryAssets Get
    {
        get
        {
            if (!cached) cached = Resources.Load<StoryAssets>("StoryAssets");
            if (!cached)
            {
                Debug.LogWarning("Falta Resources/StoryAssets. Usa Juego > Preparar historia (niveles 4-6).");
                cached = CreateInstance<StoryAssets>();
            }
            return cached;
        }
    }
}
