using UnityEngine;

[DefaultExecutionOrder(10000)]
public class GuardCameraCollision : MonoBehaviour
{
    public float radius = 0.25f;
    public float wallMargin = 0.08f;

    Transform player;
    Camera targetCamera;
    GuardPlayerMovement movement;
    CharacterController controller;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
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

            safeDistance = Mathf.Min(
                safeDistance,
                Mathf.Max(0f, hit.distance - wallMargin));
        }

        // Corrige después del movimiento de la cámara.
        transform.position = pivot + direction * safeDistance;
    }
}
