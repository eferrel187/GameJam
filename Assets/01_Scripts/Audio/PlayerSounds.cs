using UnityEngine;

/// <summary>
/// Sonidos del jugador: pasos (caminar, correr, agachado), salto,
/// aterrizaje, golpe al perder una vida y muerte. Lo añade GameAudio.
/// </summary>
public class PlayerSounds : MonoBehaviour
{
    [Tooltip("Segundos entre pasos.")]
    public float walkInterval = 0.47f;
    public float runInterval = 0.3f;
    public float crouchInterval = 0.6f;

    GuardPlayerMovement movement;
    GuardHealth health;
    CharacterController controller;

    float stepTimer;
    int lastStep = -1, lastRun = -1, lastCrouch = -1;
    float leftGround = -1f;
    int lastLives = -1;

    void Start()
    {
        movement = GetComponent<GuardPlayerMovement>();
        health = GetComponent<GuardHealth>();
        controller = GetComponent<CharacterController>();

        if (movement != null)
        {
            movement.Jumped += OnJump;
            movement.Landed += OnLand;
        }
        if (health != null) lastLives = health.Lives;
    }

    void OnDestroy()
    {
        if (movement != null)
        {
            movement.Jumped -= OnJump;
            movement.Landed -= OnLand;
        }
    }

    Vector3 Feet => controller != null
        ? transform.TransformPoint(controller.center) - Vector3.up * controller.height * transform.lossyScale.y * 0.5f
        : transform.position;

    void Update()
    {
        SoundLibrary lib = GameAudio.Library;
        if (lib == null || movement == null) return;

        // Vidas: golpe o muerte.
        if (health != null)
        {
            if (lastLives >= 0 && health.Lives < lastLives)
                GameAudio.Play(health.Lives <= 0 ? lib.muerte : lib.golpe, null, 0.9f, 0.02f);
            lastLives = health.Lives;
        }

        // Sin control (intro, cinemática, caído) no cuenta como estar en el aire.
        if (!movement.enabled || (health != null && health.IsKnockedDown)) leftGround = -1f;
        else if (!movement.IsGrounded && leftGround < 0f) leftGround = Time.time;

        // Pasos al ritmo de la velocidad real.
        bool walking = movement.enabled && movement.IsGrounded && movement.HorizontalSpeed > 0.3f;
        if (!walking)
        {
            stepTimer = 0.08f;   // el primer paso suena casi al empezar a andar
            return;
        }

        stepTimer -= Time.deltaTime;
        if (stepTimer > 0f) return;

        AudioClip clip;
        float volume = lib.volumenPasos;
        if (movement.IsCrouching)
        {
            clip = GameAudio.Pick(lib.pasosAgachado, ref lastCrouch);
            stepTimer = crouchInterval;
            volume *= 0.7f;
        }
        else if (movement.IsRunning)
        {
            clip = GameAudio.Pick(lib.pasosCorrer, ref lastRun);
            stepTimer = runInterval;
            volume *= 1.25f;
        }
        else
        {
            clip = GameAudio.Pick(lib.pasos, ref lastStep);
            stepTimer = walkInterval;
        }
        GameAudio.Play(clip, Feet, Mathf.Clamp01(volume), 0.07f, 12f);
    }

    void OnJump()
    {
        SoundLibrary lib = GameAudio.Library;
        if (lib == null) return;
        GameAudio.Play(lib.salto, Feet, 0.7f, 0.06f, 12f);
        leftGround = Time.time;
    }

    void OnLand()
    {
        SoundLibrary lib = GameAudio.Library;
        float air = leftGround >= 0f ? Time.time - leftGround : 0f;
        leftGround = -1f;
        if (lib == null || air < 0.25f) return;   // bajar un escalón no cuenta
        GameAudio.Play(lib.aterrizar, Feet, Mathf.Clamp01(0.45f + air * 0.6f), 0.05f, 14f);
        stepTimer = 0.2f;
    }
}
