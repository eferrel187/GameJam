using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(Camera))]
public class CameraIntroFollow : MonoBehaviour
{
    [Header("Jugador")]
    [SerializeField] private Transform player;

    [Header("Introduccion")]
    [SerializeField, Min(0f)] private float initialPause = 1f;
    [SerializeField, Min(0.1f)] private float travelDuration = 8f;

    [Tooltip("Puntos opcionales, ordenados desde la mesa hacia el jugador.")]
    [SerializeField] private Transform[] routePoints;

    [Header("Tercera persona")]
    [SerializeField] private float distance = 2.5f;
    [SerializeField] private float cameraHeight = 1.8f;
    [SerializeField] private float lookHeight = 1.1f;
    [SerializeField, Min(0.01f)] private float followSmoothTime = 0.12f;

    private GuardPlayerMovement movement;
    private GuardWalkAnimation walkAnimation;
    private Animator playerAnimator;

    private bool movementWasEnabled;
    private bool animationWasEnabled;
    private float previousAnimatorSpeed;

    private Vector3[] positions;
    private Quaternion[] rotations;
    private float[] cumulativeDistances;
    private float totalDistance;

    private Vector3 followOffset;
    private Vector3 smoothVelocity;
    private float elapsed;
    private bool introFinished;
    private bool controlsLocked;

    [Header("Mouse")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minimumPitch = -20f;
    [SerializeField] private float maximumPitch = 55f;

    private float mouseYaw;
    private float mousePitch;

    private void UpdateMouseLook()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            return;
        }

        if (Mouse.current == null) return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        mouseYaw += delta.x * mouseSensitivity;
        mousePitch = Mathf.Clamp(
            mousePitch - delta.y * mouseSensitivity,
            minimumPitch, maximumPitch);
    }

    private void Awake()
    {
        if (player == null)
        {
            GameObject found = GameObject.Find("Player");
            if (found != null)
                player = found.transform;
        }

        if (player == null)
        {
            Debug.LogError("Asigna el Player a CameraIntroFollow.", this);
            enabled = false;
            return;
        }

        movement = player.GetComponent<GuardPlayerMovement>();
        walkAnimation = player.GetComponent<GuardWalkAnimation>();
        playerAnimator = player.GetComponentInChildren<Animator>();

        LockControls();

        // Mantiene el angulo inicial del seguimiento.
        // No gira con el Player para evitar conflictos con WASD
        // relativo a la camara.
        Vector3 forward = Vector3.ProjectOnPlane(
            player.forward, Vector3.up).normalized;

        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;

        followOffset = -forward * distance + Vector3.up * cameraHeight;

        BuildRoute();
    }

    private void LockControls()
    {
        if (movement != null)
        {
            movementWasEnabled = movement.enabled;
            movement.enabled = false;
        }

        if (walkAnimation != null)
        {
            animationWasEnabled = walkAnimation.enabled;
            walkAnimation.enabled = false;
        }

        if (playerAnimator != null)
        {
            previousAnimatorSpeed = playerAnimator.speed;
            playerAnimator.speed = 0f;
        }

        controlsLocked = true;
    }

    private void UnlockControls()
    {
        if (!controlsLocked) return;

        if (playerAnimator != null)
            playerAnimator.speed = previousAnimatorSpeed;

        if (walkAnimation != null)
            walkAnimation.enabled = animationWasEnabled;

        if (movement != null)
            movement.enabled = movementWasEnabled;

        controlsLocked = false;
    }

    private Quaternion FollowRotation(Vector3 cameraPosition)
    {
        Vector3 target = player.position + Vector3.up * lookHeight;
        Vector3 direction = target - cameraPosition;

        return direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction, Vector3.up)
            : transform.rotation;
    }

    private void BuildRoute()
    {
        int count = 0;

        if (routePoints != null)
            foreach (Transform point in routePoints)
                if (point != null) count++;

        positions = new Vector3[count + 2];
        rotations = new Quaternion[count + 2];
        cumulativeDistances = new float[count + 2];

        positions[0] = transform.position;
        rotations[0] = transform.rotation;

        int index = 1;

        if (routePoints != null)
        {
            foreach (Transform point in routePoints)
            {
                if (point == null) continue;

                positions[index] = point.position;
                rotations[index] = point.rotation;
                index++;
            }
        }

        positions[index] = player.position + followOffset;
        rotations[index] = FollowRotation(positions[index]);

        for (int i = 1; i < positions.Length; i++)
        {
            // Un minimo permite incluir puntos que solo cambian la rotacion.
            totalDistance += Mathf.Max(
                Vector3.Distance(positions[i - 1], positions[i]), 0.01f);

            cumulativeDistances[i] = totalDistance;
        }
    }

    private void LateUpdate()
    {
        if (player == null) return;

        if (!introFinished)
        {
            elapsed += Time.deltaTime;

            if (elapsed < initialPause) return;

            float progress = Mathf.Clamp01(
                (elapsed - initialPause) / travelDuration);

            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            float travelled = easedProgress * totalDistance;

            int segment = 1;

            while (segment < positions.Length - 1 &&
                   travelled > cumulativeDistances[segment])
                segment++;

            float segmentProgress = Mathf.InverseLerp(
                cumulativeDistances[segment - 1],
                cumulativeDistances[segment],
                travelled);

            transform.SetPositionAndRotation(
                Vector3.Lerp(
                    positions[segment - 1],
                    positions[segment],
                    segmentProgress),
                Quaternion.Slerp(
                    rotations[segment - 1],
                    rotations[segment],
                    segmentProgress));

            if (progress >= 1f)
            {
                introFinished = true;
                smoothVelocity = Vector3.zero;
                mouseYaw = transform.eulerAngles.y;
                mousePitch = Mathf.DeltaAngle(
                    0f, transform.eulerAngles.x);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                UnlockControls();
            }

            return;
        }

        UpdateMouseLook();

        Quaternion orbit = Quaternion.Euler(mousePitch, mouseYaw, 0f);
        Vector3 pivot = player.position + Vector3.up * lookHeight;
        Vector3 desiredPosition =
            pivot - orbit * Vector3.forward * distance;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref smoothVelocity,
            followSmoothTime);

        transform.rotation = FollowRotation(transform.position);
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        UnlockControls();
    }
}
