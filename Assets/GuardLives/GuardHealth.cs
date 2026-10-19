using System.Collections;
using UnityEngine;

public class GuardHealth : MonoBehaviour
{
    public int maxLives = 5;
    public float immunitySeconds = 2f;
    public Transform respawnPoint;

    [Tooltip("Segundos en el suelo (con el mensaje de muerte) antes de revivir solo.")]
    public float reviveDelaySeconds = 3f;

    public int Lives { get; private set; }
    /// Verdadero mientras el jugador está caído por un golpe.
    public bool IsKnockedDown => busy;

    /// Cuántas veces ha muerto en este nivel.
    public int Deaths => maxLives - Lives;

    /// Se pierde una vida (antes de la animación de caída).
    public static event System.Action<GuardHealth> Damaged;
    /// El jugador ya está otra vez en el punto de inicio: los niveles
    /// restablecen sus trampas, llaves y acertijos.
    public static event System.Action<GuardHealth> Respawned;

    Vector3 spawn; Quaternion facing; float nextHit; bool busy;
    float shieldUntil;
    Texture2D heart;
    Animator animator;

    void Awake()
    {
        Lives = maxLives;
        // Las vidas perdidas en el nivel anterior se mantienen.
        string level = gameObject.scene.name;
        if (GameState.CarriedLives > 0 && level != "Nivel1" && level != "Tutorial")
            Lives = Mathf.Min(maxLives, GameState.CarriedLives);
        spawn = transform.position;
        facing = transform.rotation;
        heart = Resources.Load<Texture2D>("GuardHeart");
        animator = GetComponentInChildren<Animator>();
    }

    /// Un rato sin poder recibir daño (al reaparecer o al recuperar el control).
    public void Shield(float seconds)
    {
        shieldUntil = Mathf.Max(shieldUntil, Time.time + seconds);
    }

    /// Nadie puede hacer daño durante textos, transiciones, escenas o pausas.
    public bool CanBeHurt =>
        !busy && Lives > 0 && Time.time >= nextHit && Time.time >= shieldUntil &&
        !GameState.Paused && !GameState.CinematicCamera && !GameState.Narrating && !GameState.Transition;

    public void Hit()
    {
        if (!CanBeHurt) return;
        Lives--;
        nextHit = Time.time + immunitySeconds + reviveDelaySeconds;
        Damaged?.Invoke(this);
        StartCoroutine(Respawn());
    }

    /// Muerte por púas: además deja una calavera donde cayó.
    public void HitBySpikes()
    {
        int before = Lives;
        Hit();
        if (Lives < before) StoryKit.SkullAt(transform.position);
    }

    /// Derrota forzada por una secuencia (p. ej. se acabó el tiempo).
    public void ForceHit()
    {
        nextHit = 0f;
        shieldUntil = 0f;
        Hit();
    }

    IEnumerator Respawn()
    {
        busy = true;

        // Conserva si la intro de la cámara ya había dado el control.
        var movement = GetComponent<GuardPlayerMovement>();
        bool wasEnabled = movement != null && movement.enabled;
        if (movement != null)
        {
            movement.RestoreControls();
            movement.enabled = false;
        }

        // Animación: cae al suelo (o muere si no quedan vidas).
        bool dead = Lives <= 0;
        SetTrigger(dead ? "Dead" : "Hit");

        if (dead)
            yield break;   // Se queda en el suelo; GameFlow muestra "SIN VIDAS".

        // Cae, se muestra el mensaje de muerte (GameFlow) y revive solo.
        yield return new WaitForSeconds(reviveDelaySeconds);

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.SetPositionAndRotation(
            respawnPoint ? respawnPoint.position : spawn,
            respawnPoint ? respawnPoint.rotation : facing);
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;

        foreach (var trap in FindObjectsByType<LinkedFloorTrap>(FindObjectsSortMode.None))
            trap.ResetTrap();

        // La cámara vuelve detrás del jugador al instante.
        var follow = FindFirstObjectByType<CameraIntroFollow>();
        if (follow != null) follow.SnapBehindPlayer();

        // Vuelve a estar de pie.
        if (animator != null && animator.HasState(0, Animator.StringToHash("Idle")))
        {
            animator.ResetTrigger("Hit");
            animator.Play("Idle", 0, 0f);
        }

        // Cada nivel restablece sus trampas, llaves y acertijos.
        Respawned?.Invoke(this);

        yield return null;

        // Vuelve a mostrar el contexto del nivel antes de devolver el control.
        yield return StoryNarrator.AfterDeath();

        Shield(1.2f);
        if (movement != null) movement.enabled = wasEnabled;
        busy = false;
    }

    void SetTrigger(string name)
    {
        if (animator == null) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Trigger)
            {
                animator.speed = 1f;
                animator.SetTrigger(name);
                return;
            }
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider.GetComponentInParent<SpikeDamage>() != null)
        {
            int before = Lives;
            HitBySpikes();
            if (Lives < before) GameAudio.TrampaCepo(hit.point);
        }
    }

    void OnGUI()
    {
        if (!heart || GameState.HudHidden) return;
        var old = GUI.color;
        float scale = Mathf.Clamp(Screen.width / 1280f, .65f, 2f);
        for (int i = 0; i < maxLives; i++)
        {
            GUI.color = i < Lives ? Color.white : new Color(.25f, .25f, .25f, .4f);
            GUI.DrawTexture(new Rect((18 + i * 52) * scale, 18 * scale, 46 * scale, 46 * scale),
                heart, ScaleMode.ScaleToFit, true);
        }
        GUI.color = old;
        // La pantalla de "SIN VIDAS" la muestra GameFlow.
    }
}
