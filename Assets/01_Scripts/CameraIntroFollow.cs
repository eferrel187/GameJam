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

    [Header("Caídas")]
    [Tooltip("Al caer (o estar tirado) la cámara mira desde arriba a esta altura.")]
    [SerializeField] private float overheadHeight = 3.5f;
    [Tooltip("Segundos en el aire para considerar que es una caída (un salto dura ~0.7 s).")]
    [SerializeField] private float fallAirTime = 0.75f;

    private GuardHealth health;
    private float airTime;

    private bool fallLatched;

    /// 0 = normal, 1 = golpeado en el suelo, 2 = cayendo / en un pozo.
    private int FallState()
    {
        if (movement != null && movement.enabled)
        {
            airTime = movement.IsGrounded ? 0f : airTime + Time.deltaTime;

            // Un salto normal no baja a más de ~6.6 m/s: más rápido = caída.
            CharacterController cc = player.GetComponent<CharacterController>();
            bool fastFall = cc != null && !movement.IsGrounded && cc.velocity.y < -7.5f;

            if (airTime > fallAirTime || fastFall) fallLatched = true;
            else if (movement.IsGrounded) fallLatched = false;
        }

        if (fallLatched) return 2;
        if (health != null && health.IsKnockedDown) return 1;
        return 0;
    }

    private void UpdateMouseLook()
    {
        // En pausa o en Game Over el ratón es para los botones.
        if (GameState.Paused || GameState.HudHidden) return;

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
        health = player.GetComponent<GuardHealth>();
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

        // En estos niveles se empieza a jugar directamente (sin recorrido).
        if (System.Array.IndexOf(NoIntroScenes, gameObject.scene.name) >= 0)
            FinishIntroNow();
    }

    /// Niveles que empiezan sin la animación de cámara.
    public static string[] NoIntroScenes = { "Nivel2", "Nivel3", "Nivel4", "Nivel5", "Nivel6" };

    private void FinishIntroNow()
    {
        int last = positions.Length - 1;
        transform.SetPositionAndRotation(positions[last], rotations[last]);
        introFinished = true;
        smoothVelocity = Vector3.zero;
        mouseYaw = transform.eulerAngles.y;
        mousePitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        UnlockControls();
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

    // Altura a la que mira la cámara (baja al agacharse).
    private float currentLook = -1f;
    private float crouchBlend;

    private Quaternion FollowRotation(Vector3 cameraPosition)
    {
        float look = currentLook >= 0f ? currentLook : lookHeight;
        Vector3 target = player.position + Vector3.up * look;
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
            // Espera a que termine el menú y el tutorial.
            if (GameState.HoldIntro) return;

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

        // Durante una cinemática la cámara la mueve la secuencia.
        if (GameState.CinematicCamera) return;

        UpdateMouseLook();

        // Agachado: la cámara baja y mira casi horizontal, para pasar
        // por debajo de techos bajos sin quedar dentro de ellos.
        bool crouching = movement != null && movement.IsCrouching;
        crouchBlend = Mathf.MoveTowards(crouchBlend, crouching ? 1f : 0f,
            Time.deltaTime * 4f);
        currentLook = Mathf.Lerp(lookHeight, lookHeight * 0.45f, crouchBlend);
        float pitch = Mathf.Lerp(mousePitch, Mathf.Min(mousePitch, -2f), crouchBlend);

        Quaternion orbit = Quaternion.Euler(pitch, mouseYaw, 0f);
        Vector3 pivot = player.position + Vector3.up * currentLook;
        Vector3 desiredPosition =
            pivot - orbit * Vector3.forward * distance;

        int fall = FallState();
        bool overhead = fall != 0;
        Vector3 back = Quaternion.Euler(0f, mouseYaw, 0f) * Vector3.back;

        if (fall == 2)
        {
            // Sigue la caída desde arriba (por el hueco por donde cayó).
            desiredPosition = pivot + Vector3.up * overheadHeight + back * 0.2f;
        }
        else if (fall == 1)
        {
            // Golpeado en el suelo: plano alto desde atrás, bajo el techo.
            desiredPosition = pivot + Vector3.up * 1.6f + back * 2f;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref smoothVelocity,
            overhead ? followSmoothTime * 2f : followSmoothTime);

        transform.rotation = FollowRotation(transform.position);
    }

    /// Coloca la cámara de golpe detrás del jugador (al reaparecer).
    public void SnapBehindPlayer()
    {
        if (player == null || !introFinished) return;

        mouseYaw = player.eulerAngles.y;
        mousePitch = 12f;

        Quaternion orbit = Quaternion.Euler(mousePitch, mouseYaw, 0f);
        Vector3 pivot = player.position + Vector3.up * lookHeight;
        transform.position = pivot - orbit * Vector3.forward * distance;
        transform.rotation = FollowRotation(transform.position);
        smoothVelocity = Vector3.zero;

        GuardCameraCollision collision = GetComponent<GuardCameraCollision>();
        if (collision != null) collision.ResetDistance();
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        UnlockControls();
    }
}
