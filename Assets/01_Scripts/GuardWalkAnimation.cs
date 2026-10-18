using UnityEngine;

/// <summary>
/// Envía el estado del movimiento al Animator.
///
/// Si el Animator tiene estos parámetros, los usa:
///   Speed    (Float)   0 quieto, 0.5 caminar, 1 correr
///   Grounded (Bool)
///   Crouch   (Bool)
///   Jump     (Trigger)
///
/// Si no los tiene (como ahora, que solo hay animación de caminar),
/// pausa la animación al estar quieto y la acelera al correr.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class GuardWalkAnimation : MonoBehaviour
{
    [SerializeField] private float runPlaybackSpeed = 1.6f;
    [SerializeField] private float speedDamping = 0.1f;

    private Animator animator;
    private CharacterController controller;
    private GuardPlayerMovement movement;

    [Tooltip("Segundos en el aire (sin saltar) antes de pasar a la pose de caída.")]
    [SerializeField] private float airborneDelay = 0.18f;

    private bool hasSpeed, hasGrounded, hasCrouch, hasJump, hasAirborne, hasCrouchRate;
    private int lastJumpCount;
    private float jumpTriggerTime = -10f;
    private float crouchClipSpeed;   // m/s a velocidad 1 de la animación agachado

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int GroundedId = Animator.StringToHash("Grounded");
    private static readonly int CrouchId = Animator.StringToHash("Crouch");
    private static readonly int JumpId = Animator.StringToHash("Jump");
    private static readonly int AirborneId = Animator.StringToHash("Airborne");
    private static readonly int CrouchRateId = Animator.StringToHash("CrouchRate");

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();
        movement = GetComponent<GuardPlayerMovement>();

        if (animator == null) return;

        animator.applyRootMotion = false;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == SpeedId) hasSpeed = true;
            if (p.nameHash == GroundedId) hasGrounded = true;
            if (p.nameHash == CrouchId) hasCrouch = true;
            if (p.nameHash == JumpId) hasJump = true;
            if (p.nameHash == AirborneId) hasAirborne = true;
            if (p.nameHash == CrouchRateId) hasCrouchRate = true;
        }

        // Velocidad propia del caminar agachado: así los pasos van al ritmo
        // real del desplazamiento (sin patinar ni ir "a cámara lenta").
        if (animator.runtimeAnimatorController != null)
            foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips)
                if (c.name == "Walk_Forward_Arc" || c.name == "Crouched_Walking")
                    crouchClipSpeed = c.averageSpeed.magnitude * Mathf.Max(0.01f, animator.humanScale);
    }

    private void LateUpdate()
    {
        if (animator == null) return;

        if (hasSpeed)
        {
            UpdateParameters();
            return;
        }

        // Modo simple: solo existe la animación de caminar.
        Vector3 velocity = controller.velocity;
        velocity.y = 0f;

        bool moving = velocity.sqrMagnitude > 0.01f;
        bool running = movement != null && movement.IsRunning;

        animator.speed = !moving ? 0f : running ? runPlaybackSpeed : 1f;
    }

    private void UpdateParameters()
    {
        animator.speed = 1f;

        float speed = movement != null ? movement.SpeedPercent : 0f;
        animator.SetFloat(SpeedId, speed, speedDamping, Time.deltaTime);

        if (movement == null) return;

        if (hasJump && movement.JumpCount != lastJumpCount)
        {
            animator.SetTrigger(JumpId);
            jumpTriggerTime = Time.time;
        }

        // Justo al saltar aún se lee "en el suelo" un fotograma: se ignora.
        bool justJumped = Time.time - jumpTriggerTime < 0.15f;
        if (hasGrounded) animator.SetBool(GroundedId, movement.IsGrounded && !justJumped);
        if (hasCrouch) animator.SetBool(CrouchId, movement.IsCrouching);
        // En el aire de verdad (no un parpadeo del suelo al bajar un escalón).
        if (hasAirborne) animator.SetBool(AirborneId, movement.AirTime > airborneDelay);

        if (hasCrouchRate)
        {
            float natural = crouchClipSpeed > 0.2f ? crouchClipSpeed : 0.9f;
            float rate = movement.IsCrouching ? movement.HorizontalSpeed / natural : 0f;
            animator.SetFloat(CrouchRateId, Mathf.Clamp(rate, 0f, 2.5f), 0.08f, Time.deltaTime);
        }


        // Un salto que no se usó no debe "guardarse" y repetirse al aterrizar.
        if (hasJump && Time.time - jumpTriggerTime > 0.2f)
            animator.ResetTrigger(JumpId);

        lastJumpCount = movement.JumpCount;
    }

    private void OnDisable()
    {
        if (animator != null)
            animator.speed = 1f;
    }
}
