
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Jugador")]
    public Transform player;

    [Header("Camara")]
    public float distance = 4f;
    public float height = 1.5f;

    [Header("Mouse")]
    public float sensitivity = 0.12f;
    public float minPitch = -20f;
    public float maxPitch = 55f;

    [Header("Colisiones")]
    public LayerMask collisionLayers = ~0;
    public float collisionRadius = 0.35f;
    public float wallPadding = 0.15f;
    public float returnSpeed = 5f;

    private float yaw;
    private float pitch = 10f;
    private float currentDistance;

    void Start()
    {
        if (player == null)
        {
            GameObject found = GameObject.Find("Player");

            if (found != null)
                player = found.transform;
        }

        if (player == null)
        {
            Debug.LogError("No se encontro el Player.");
            enabled = false;
            return;
        }

        yaw = player.eulerAngles.y;
        currentDistance = distance;

        GetComponent<Camera>().nearClipPlane = 0.1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        UpdateCamera(true);
    }

    void LateUpdate()
    {
        if (player == null)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Mouse.current != null)
        {
            if (Cursor.lockState != CursorLockMode.Locked &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 mouse = Mouse.current.delta.ReadValue();

                yaw += mouse.x * sensitivity;
                pitch -= mouse.y * sensitivity;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }
        }

        UpdateCamera(false);
    }

    void UpdateCamera(bool instant)
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 target = player.position + Vector3.up * height;
        Vector3 direction = -(rotation * Vector3.forward);

        float allowedDistance = distance;

        RaycastHit[] hits = Physics.SphereCastAll(
            target,
            collisionRadius,
            direction,
            distance,
            collisionLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.collider.transform.IsChildOf(player))
                continue;

            float safeDistance = Mathf.Max(
                0f,
                hit.distance - wallPadding
            );

            allowedDistance = Mathf.Min(
                allowedDistance,
                safeDistance
            );
        }

        if (instant || allowedDistance < currentDistance)
        {
            currentDistance = allowedDistance;
        }
        else
        {
            currentDistance = Mathf.MoveTowards(
                currentDistance,
                allowedDistance,
                returnSpeed * Time.deltaTime
            );
        }

        transform.position = target + direction * currentDistance;
        transform.LookAt(target);
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
