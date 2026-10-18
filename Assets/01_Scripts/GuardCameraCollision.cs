using UnityEngine;

[DefaultExecutionOrder(10000)]
public class GuardCameraCollision : MonoBehaviour
{
    public float radius = 0.2f;
    public float wallMargin = 0.08f;

    Transform player;
    Camera targetCamera;
    GuardPlayerMovement movement;
    CharacterController controller;

    // Lo llama GameBootstrap en cada escena cargada.
    public static void Install()
    {
        Camera cam = Camera.main;

        if (cam && !cam.GetComponent<GuardCameraCollision>())
            cam.gameObject.AddComponent<GuardCameraCollision>();
    }

    void Start()
    {
        targetCamera = GetComponent<Camera>();

        GameObject obj = GameObject.Find("Player");
        if (!obj) return;

        player = obj.transform;
        movement = obj.GetComponent<GuardPlayerMovement>();
        controller = obj.GetComponent<CharacterController>();

        if (targetCamera)
            targetCamera.nearClipPlane =
                Mathf.Min(targetCamera.nearClipPlane, 0.05f);
    }

    Vector3 uncorrectedPosition;
    bool restorePosition;
    float currentDistance = -1f;
    float distanceVelocity;

    /// Olvida la distancia suavizada (tras un teletransporte).
    public void ResetDistance()
    {
        currentDistance = -1f;
        distanceVelocity = 0f;
        restorePosition = false;
    }

    void Update()
    {
        // El seguimiento calcula desde su propia posición,
        // sin acumular la corrección contra la pared.
        if (restorePosition)
        {
            transform.position = uncorrectedPosition;
            restorePosition = false;
        }
    }

    void LateUpdate()
    {
        if (!player) return;

        // Conserva el recorrido inicial de la cámara.
        if (movement && !movement.enabled) return;

        Vector3 pivot = controller
            ? controller.bounds.center
            : player.position + Vector3.up;

        Vector3 offset = transform.position - pivot;
        float distance = offset.magnitude;
        if (distance < 0.001f) return;

        Vector3 direction = offset / distance;
        float safeDistance = distance;

        RaycastHit[] hits = Physics.SphereCastAll(
            pivot,
            radius,
            direction,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            // Ignora el cuerpo y los colliders del jugador.
            if (hit.transform == player ||
                hit.transform.IsChildOf(player))
                continue;

            // La esfera ya tocaba algo al salir (p. ej. el borde de un hueco
            // junto al jugador): eso no es un obstáculo entre jugador y cámara.
            if (hit.distance <= 0f && hit.point == Vector3.zero)
                continue;

            safeDistance = Mathf.Min(
                safeDistance,
                Mathf.Max(0f, hit.distance - wallMargin));
        }

        uncorrectedPosition = transform.position;
        restorePosition = true;

        if (currentDistance < 0f)
            currentDistance = safeDistance;

        if (safeDistance < currentDistance)
        {
            // Acercamiento inmediato para no atravesar obstáculos.
            currentDistance = safeDistance;
            distanceVelocity = 0f;
        }
        else
        {
            // Recuperación suave cuando vuelve a haber espacio.
            currentDistance = Mathf.SmoothDamp(
                currentDistance,
                safeDistance,
                ref distanceVelocity,
                0.3f);

            currentDistance = Mathf.Min(currentDistance, safeDistance);
        }

        transform.position =
            pivot + direction * currentDistance;
    }
}
