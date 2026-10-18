using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Movimiento del jugador: caminar (WASD/flechas), correr (Shift),
/// saltar (Espacio) y agacharse (C o Ctrl izquierdo).
/// La dirección es relativa a la cámara.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class GuardPlayerMovement : MonoBehaviour
{
    // Valores antiguos (los guardaban las escenas): ya no se usan.
    [SerializeField, HideInInspector] private float moveSpeed = 3f;
    [SerializeField, HideInInspector] private float runSpeed = 8f;
    [SerializeField, HideInInspector] private float crouchSpeed = 2f;

    [Header("Velocidades (m/s)")]
    [Tooltip("Caminar a paso natural.")]
    [SerializeField] private float walkSpeed = 2.4f;
    [SerializeField] private float sprintSpeed = 5.2f;
    [SerializeField] private float crouchWalkSpeed = 1.7f;

    [Header("Giro")]
    [SerializeField] private float rotationSpeed = 540f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private Transform cameraTransform;

    [Tooltip("Fracción del giro que se permite en el aire (evita girar de golpe al saltar).")]
    [SerializeField, Range(0f, 1f)] private float airTurnFactor = 0.25f;

    [Header("Colisión")]
    [Tooltip("Radio mínimo real (m) del jugador, para que el cuerpo no atraviese las paredes.")]
    [SerializeField] private float minWorldRadius = 0.28f;

    [Header("Saltar")]
    [SerializeField] private float jumpHeight = 1.1f;
    [Tooltip("Margen para saltar justo después de salir de un borde.")]
    [SerializeField] private float coyoteTime = 0.12f;
    [Tooltip("Gravedad extra al caer (salto más firme, sin flotar).")]
    [SerializeField] private float fallGravityMultiplier = 1.5f;
    [SerializeField] private float maxFallSpeed = 30f;
    [Tooltip("Distancia extra bajo los pies que aún cuenta como suelo.")]
    [SerializeField] private float groundProbeDistance = 0.12f;

    [Header("Agacharse")]
    [SerializeField, Range(0.3f, 0.9f)] private float crouchHeightFactor = 0.6f;

    // Lectura para animaciones, sonido, trampas, etc.
    public bool IsRunning { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsGrounded { get; private set; }
    public float HorizontalSpeed { get; private set; }
    /// Normalizada: 0 quieto, 0.5 caminar, 1 correr.
    public float SpeedPercent { get; private set; }
    /// Aumenta en 1 cada vez que el jugador salta.
    public int JumpCount { get; private set; }

    public event System.Action Jumped;
    public event System.Action Landed;

    /// Velocidad vertical actual (m/s), para animaciones.
    public float VerticalSpeed => verticalSpeed;
    /// Segundos seguidos sin tocar el suelo.
    public float AirTime { get; private set; }

#if UNITY_EDITOR
    private static bool debugAutoForward;
#endif
    private CharacterController controller;
    private float verticalSpeed;
    private float lastGroundedTime = -10f;
    private float standHeight;
    private Vector3 standCenter;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // Un poco más ancho: los hombros del modelo ya no entran en la pared.
        float scale = Mathf.Max(0.01f, transform.lossyScale.x);
        if (controller.radius * scale < minWorldRadius)
            controller.radius = minWorldRadius / scale;

        // El "skin width" deja al personaje flotando unos cm: se compensa.
        controller.center += Vector3.up * controller.skinWidth;

        standHeight = controller.height;
        standCenter = controller.center;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Start()
    {
        SnapToGround();
    }

    /// Apoya los pies en el suelo real (en la escena el jugador está unos cm
    /// por encima y caía al empezar).
    public void SnapToGround()
    {
        float scale = transform.lossyScale.y;
        Vector3 center = transform.TransformPoint(controller.center);
        float halfHeight = controller.height * scale * 0.5f;
        RaycastHit[] hits = Physics.RaycastAll(center, Vector3.down, halfHeight + 2f,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (RaycastHit h in hits)
            if (h.transform != transform && !h.transform.IsChildOf(transform) && h.distance < best)
                best = h.distance;
        if (best == float.MaxValue) return;

        float drop = best - halfHeight - controller.skinWidth * scale;
        if (drop <= 0.005f) return;
        controller.enabled = false;
        transform.position += Vector3.down * drop;
        controller.enabled = true;
        Physics.SyncTransforms();
    }

    private void OnDisable()
    {
        IsRunning = false;
        HorizontalSpeed = 0f;
        SpeedPercent = 0f;
    }

    private void Update()
    {
        Vector2 input = Vector2.zero;
        Keyboard keyboard = Keyboard.current;

        bool runHeld = false;
        bool crouchHeld = false;
        bool jumpPressed = false;

        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                input.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                input.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                input.x -= 1f;

            runHeld = keyboard.leftShiftKey.isPressed ||
                      keyboard.rightShiftKey.isPressed;
            crouchHeld = keyboard.cKey.isPressed ||
                         keyboard.leftCtrlKey.isPressed;
            jumpPressed = keyboard.spaceKey.wasPressedThisFrame;

#if UNITY_EDITOR
            // Solo para pruebas en el editor: F8 = avanzar solo (como mantener W).
            if (keyboard.f8Key.wasPressedThisFrame) debugAutoForward = !debugAutoForward;
            if (debugAutoForward) input.y = 1f;
            // F9 = darse la vuelta (cámara incluida).
            if (keyboard.f9Key.wasPressedThisFrame && cameraTransform != null)
            {
                transform.Rotate(0f, 180f, 0f);
                CameraIntroFollow f = cameraTransform.GetComponent<CameraIntroFollow>();
                if (f != null) f.SnapBehindPlayer();
            }
#endif
        }

        input = Vector2.ClampMagnitude(input, 1f);

        // ---- Agacharse
        if (crouchHeld && !IsCrouching)
            SetCrouch(true);
        else if (!crouchHeld && IsCrouching && CanStandUp())
            SetCrouch(false);

        // ---- Dirección relativa a la cámara
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(
                cameraTransform.forward, Vector3.up).normalized;

            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.ProjectOnPlane(
                    cameraTransform.up, Vector3.up).normalized;

            right = Vector3.Cross(Vector3.up, forward).normalized;
        }

        Vector3 direction = forward * input.y + right * input.x;
        bool moving = direction.sqrMagnitude > 0.001f;

        if (moving)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            float turn = IsGrounded ? 1f : airTurnFactor;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * turn * Time.deltaTime);
        }

        IsRunning = moving && runHeld && !IsCrouching;

        float speed = IsCrouching ? crouchWalkSpeed
                    : IsRunning ? sprintSpeed
                    : walkSpeed;

        // ---- Suelo, salto y gravedad
        bool wasGrounded = IsGrounded;
        IsGrounded = controller.isGrounded || (verticalSpeed <= 0f && ProbeGround());

        if (IsGrounded)
        {
            lastGroundedTime = Time.time;

            if (verticalSpeed < 0f)
                verticalSpeed = -3f;     // pegado al suelo (también al bajar escalones)

            if (!wasGrounded)
                Landed?.Invoke();
        }

        if (jumpPressed && !IsCrouching &&
            Time.time - lastGroundedTime <= coyoteTime)
        {
            verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastGroundedTime = -10f;
            JumpCount++;
            Jumped?.Invoke();
        }

        // Cae algo más rápido de lo que sube: salto firme, sin quedarse flotando.
        float g = verticalSpeed < 0f && !IsGrounded ? gravity * fallGravityMultiplier : gravity;
        verticalSpeed = Mathf.Max(verticalSpeed + g * Time.deltaTime, -maxFallSpeed);

        Vector3 velocity = direction * speed;
        velocity.y = verticalSpeed;

        CollisionFlags flags = controller.Move(velocity * Time.deltaTime);

        // Golpe con el techo: empieza a caer en vez de quedarse pegado.
        if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
            verticalSpeed = 0f;

        AirTime = IsGrounded ? 0f : AirTime + Time.deltaTime;

        // Velocidad REAL (si choca con una pared o un obstáculo no "camina en el sitio").
        Vector3 actual = controller.velocity;
        actual.y = 0f;
        float wanted = speed * direction.magnitude;
        float blocked = wanted > 0.01f ? Mathf.Clamp01(actual.magnitude / wanted) : 0f;

        HorizontalSpeed = moving ? actual.magnitude : 0f;
        SpeedPercent = !moving ? 0f
                     : (IsRunning ? 1f : 0.5f * direction.magnitude) * blocked;
    }

    private void SetCrouch(bool crouch)
    {
        IsCrouching = crouch;

        float height = crouch ? standHeight * crouchHeightFactor : standHeight;

        // Mantiene los pies en el mismo sitio al cambiar la altura.
        controller.height = height;
        controller.center = standCenter -
            Vector3.up * (standHeight - height) * 0.5f;
    }

    /// Suelo justo debajo de los pies (controller.isGrounded parpadea en
    /// bordes, escalones y rampas).
    private bool ProbeGround()
    {
        float scale = transform.lossyScale.y;
        float radius = controller.radius * transform.lossyScale.x * 0.95f;
        Vector3 bottom = transform.TransformPoint(
            controller.center - Vector3.up * (controller.height * 0.5f - controller.radius));
        float distance = controller.skinWidth * scale + groundProbeDistance;

        RaycastHit[] hits = Physics.SphereCastAll(bottom + Vector3.up * 0.05f, radius, Vector3.down,
            distance + 0.05f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (hit.distance <= 0f) continue;                 // ya solapado (pared)
            if (hit.normal.y < 0.6f) continue;                // pared o pendiente fuerte
            return true;
        }
        return false;
    }

    private bool CanStandUp()
    {
        float scale = transform.lossyScale.y;
        float radius = controller.radius * scale * 0.9f;

        Vector3 top = transform.TransformPoint(
            controller.center +
            Vector3.up * (controller.height * 0.5f - controller.radius));

        float distance = (standHeight - controller.height + controller.skinWidth * 2f) * scale;

        RaycastHit[] hits = Physics.SphereCastAll(
            top, radius, Vector3.up, distance,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform ||
                hit.transform.IsChildOf(transform))
                continue;

            return false;   // Hay techo: sigue agachado.
        }

        return true;
    }
}
