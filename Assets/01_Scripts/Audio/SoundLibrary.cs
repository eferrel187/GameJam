using UnityEngine;

/// <summary>
/// Todos los sonidos del juego (los archivos están en Assets/07_sonido).
/// Lo rellena el menú  Juego > Configurar sonidos  y se guarda en
/// Assets/Resources/SoundLibrary.asset para que GameAudio lo encuentre.
/// </summary>
public class SoundLibrary : ScriptableObject
{
    [Header("Jugador")]
    public AudioClip[] pasos;
    public AudioClip[] pasosCorrer;
    public AudioClip[] pasosAgachado;
    public AudioClip salto;
    public AudioClip aterrizar;
    public AudioClip golpe;
    public AudioClip muerte;

    [Header("Mundo")]
    public AudioClip antorcha;
    public AudioClip puerta;
    public AudioClip terror;
    public AudioClip llave;
    public AudioClip trampaCepo;
    public AudioClip trampaSuelo;
    public AudioClip ambiente;

    [Header("Mano monstruosa")]
    public AudioClip manoSale;
    public AudioClip manoAgarra;
    public AudioClip manoArrastra;

    [Header("Volúmenes")]
    [Range(0f, 1f)] public float volumenPasos = 0.55f;
    [Range(0f, 1f)] public float volumenAntorcha = 0.4f;
    [Range(0f, 1f)] public float volumenAmbiente = 0.3f;
}
