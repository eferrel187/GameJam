using UnityEngine;

/// <summary>
/// Luz de antorcha viva: la intensidad, el alcance y el color tiemblan como
/// una llama real (ruido suave + pequeños "chispazos"), y la luz se mueve unos
/// milímetros para que las sombras y reflejos de la pared bailen.
/// Si la antorcha tiene una malla de llama (hijo con "flame", "fire" o "llama"
/// en el nombre), también la hace oscilar.
/// Lo coloca el menú  Juego > Iluminar mazmorra (antorchas).
/// </summary>
[RequireComponent(typeof(Light))]
public class TorchFlicker : MonoBehaviour
{
    [Header("Llama")]
    public float baseIntensity = 2.6f;
    public float baseRange = 7f;
    [Range(0f, 1f)] public float flicker = 0.28f;
    public float speed = 7f;
    public Color hotColor = new Color(1f, 0.72f, 0.36f);
    public Color coolColor = new Color(1f, 0.46f, 0.16f);

    [Header("Movimiento")]
    public float jitter = 0.025f;
    public Transform flameMesh;

    Light lightSource;
    Vector3 basePosition;
    Vector3 flameScale;
    float seed;
    float spark;
    float smoothK = 1f;

    void Awake()
    {
        lightSource = GetComponent<Light>();
        basePosition = transform.localPosition;
        seed = Random.value * 100f;
        if (flameMesh != null) flameScale = flameMesh.localScale;
    }

    void Update()
    {
        float t = Time.time * speed + seed;

        // Dos capas de ruido: un vaivén lento y un temblor rápido.
        float slow = Mathf.PerlinNoise(t * 0.25f, seed) - 0.5f;
        float fast = Mathf.PerlinNoise(seed, t) - 0.5f;

        // De vez en cuando la llama "chisporrotea" y baja un instante.
        // Bajadas suaves y ocasionales (sin saltos bruscos).
        if (spark <= 0f && Random.value < Time.deltaTime * 0.25f) spark = Random.Range(0.06f, 0.12f);
        spark = Mathf.MoveTowards(spark, 0f, Time.deltaTime * 0.5f);

        float k = 1f + (slow * 1.2f + fast * 0.6f) * flicker - spark;
        k = smoothK = Mathf.Lerp(smoothK, k, 1f - Mathf.Exp(-Time.deltaTime * 12f));
        lightSource.intensity = baseIntensity * Mathf.Max(0.35f, k);
        lightSource.range = baseRange * (0.94f + 0.06f * k);
        lightSource.color = Color.Lerp(coolColor, hotColor, Mathf.Clamp01(0.5f + slow * 1.2f));

        transform.localPosition = basePosition + new Vector3(
            Mathf.PerlinNoise(t * 0.7f, 3f) - 0.5f,
            Mathf.PerlinNoise(t * 0.9f, 7f) - 0.5f,
            Mathf.PerlinNoise(t * 0.7f, 11f) - 0.5f) * jitter * 2f;

        if (flameMesh != null)
        {
            float sy = 1f + fast * 0.35f + slow * 0.15f;
            float sx = 1f - fast * 0.12f;
            flameMesh.localScale = new Vector3(flameScale.x * sx, flameScale.y * sy, flameScale.z * sx);
        }
    }
}
