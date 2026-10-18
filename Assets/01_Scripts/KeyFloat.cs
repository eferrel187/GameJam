using UnityEngine;

/// <summary>
/// Animación de la llave: flota con un rebote suave y gira sobre su propio
/// eje vertical, sin alejarse de su posición original.
/// GuardDoorAndKey la añade automáticamente a la llave (Key4).
/// </summary>
public class KeyFloat : MonoBehaviour
{
    [Tooltip("Altura del rebote (metros).")]
    public float bobHeight = 0.07f;
    [Tooltip("Rebotes por segundo.")]
    public float bobSpeed = 0.9f;
    [Tooltip("Grados por segundo de giro.")]
    public float spinSpeed = 70f;

    Vector3 basePosition;
    Quaternion baseRotation;
    Vector3 center;
    float angle;
    float phase;

    void Awake()
    {
        basePosition = transform.position;
        baseRotation = transform.rotation;

        // Centro visual de la llave: gira sobre sí misma, no alrededor del pivote.
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            center = b.center;
        }
        else
        {
            center = basePosition;
        }
    }

    void OnEnable()
    {
        angle = 0f;
        phase = 0f;
    }

    void Update()
    {
        angle = (angle + spinSpeed * Time.deltaTime) % 360f;
        phase += Time.deltaTime * bobSpeed * Mathf.PI * 2f;

        // Rebote suave: sube y baja con una curva senoidal (sin saltos).
        float bob = (Mathf.Sin(phase) * 0.5f + 0.5f) * bobHeight;

        transform.SetPositionAndRotation(basePosition, baseRotation);
        transform.RotateAround(center, Vector3.up, angle);
        transform.position += Vector3.up * bob;
    }

    void OnDisable()
    {
        // Al recogerla (o reiniciarla) vuelve a su sitio exacto.
        transform.SetPositionAndRotation(basePosition, baseRotation);
    }
}
