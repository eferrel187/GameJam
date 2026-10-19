using UnityEngine;

/// <summary>Puas de suelo que esperan ocultas, salen y vuelven a esconderse.</summary>
[DefaultExecutionOrder(150)]
public sealed class RisingSpikes : MonoBehaviour
{
    public bool activateOnStep = true;
    [Min(0f)] public float warningSeconds = 0.12f;
    [Min(0.02f)] public float riseSeconds = 0.16f;
    [Min(0.1f)] public float raisedSeconds = 1.5f;
    [Min(0.02f)] public float retractSeconds = 0.4f;
    [Min(0f)] public float cooldownSeconds = 0.8f;

    enum Phase { Hidden, Warning, Rising, Raised, Retracting, Cooldown }
    Phase phase;
    Transform player;
    CharacterController controller;
    GuardPlayerMovement movement;
    GuardHealth health;
    Renderer[] visuals;
    Bounds footprint;
    Vector3 raisedPosition, hiddenPosition;
    float clock, amount;
    bool initialized, wasTouching;
    Vector3 previousCenter;

    public bool IsRaised => amount >= 0.45f;

    void Awake()
    {
        visuals = GetComponentsInChildren<Renderer>(true);
        if (visuals.Length == 0) { enabled = false; return; }
        footprint = Measure();
        raisedPosition = transform.position;
        // Tambien se esconde la base, para no dejar puntas atravesando el piso.
        float bury = Mathf.Max(0.2f, footprint.size.y + 0.15f);
        hiddenPosition = raisedPosition - Vector3.up * bury;
        foreach (Collider collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        initialized = true;
        ResetTrap();
    }

    void Start()
    {
        GameObject found = GameObject.Find("Player");
        if (!found) return;
        player = found.transform;
        controller = found.GetComponent<CharacterController>();
        movement = found.GetComponent<GuardPlayerMovement>();
        health = found.GetComponent<GuardHealth>();
        if (controller) previousCenter = controller.bounds.center;
    }

    public void TriggerTrap()
    {
        if (!initialized || phase != Phase.Hidden) return;
        phase = Phase.Warning;
        clock = 0f;
        GameAudio.TrampaSuelo(footprint.center);
    }

    /// Centro (en planta) de la trampa, para colocar señales encima.
    public Vector3 FootprintCenter => footprint.center;
    public Bounds Footprint => footprint;

    /// Mueve la trampa a otro sitio (ya preparada).
    public void Relocate(Vector3 delta)
    {
        if (!initialized) return;
        raisedPosition += delta;
        hiddenPosition += delta;
        footprint.center += delta;
        transform.position += delta;
    }

    public void ResetTrap()
    {
        if (!initialized) return;
        phase = Phase.Hidden;
        clock = amount = 0f;
        wasTouching = false;
        transform.position = hiddenPosition;
        if (controller) previousCenter = controller.bounds.center;
    }

    void Update()
    {
        if (!initialized || !controller || !health) return;
        Vector3 center = controller.bounds.center;
        // No se activan durante menus, secuencias o la animacion de muerte.
        if (GameState.Paused || GameState.HudHidden || GameState.CinematicCamera ||
            health.IsKnockedDown || !movement || !movement.enabled)
        {
            previousCenter = center;
            return;
        }

        bool touching = OverFootprint(center);
        if (activateOnStep && phase == Phase.Hidden && touching && !wasTouching) TriggerTrap();
        wasTouching = touching;
        clock += Time.deltaTime;

        switch (phase)
        {
            case Phase.Warning:
                if (clock >= warningSeconds) { phase = Phase.Rising; clock = 0f; }
                break;
            case Phase.Rising:
                amount = Mathf.SmoothStep(0f, 1f, clock / Mathf.Max(0.02f, riseSeconds));
                if (clock >= riseSeconds) { amount = 1f; phase = Phase.Raised; clock = 0f; }
                break;
            case Phase.Raised:
                if (clock >= raisedSeconds) { phase = Phase.Retracting; clock = 0f; }
                break;
            case Phase.Retracting:
                amount = 1f - Mathf.SmoothStep(0f, 1f, clock / Mathf.Max(0.02f, retractSeconds));
                if (clock >= retractSeconds) { amount = 0f; phase = Phase.Cooldown; clock = 0f; }
                break;
            case Phase.Cooldown:
                // Hay que salir antes de que se pueda activar otra vez.
                if (clock >= cooldownSeconds && !touching) { phase = Phase.Hidden; clock = 0f; }
                break;
        }
        transform.position = Vector3.Lerp(hiddenPosition, raisedPosition, amount);
        if (IsRaised)
        {
            Bounds danger = Measure();
            danger.Expand(new Vector3(-0.06f, 0.08f, -0.06f));
            // Barrido entre fotogramas: correr no permite saltarse el volumen.
            Bounds expanded = danger;
            expanded.Expand(controller.bounds.size);
            Vector3 delta = center - previousCenter;
            bool crossed = delta.sqrMagnitude > 0.00001f && expanded.IntersectRay(
                new Ray(previousCenter, delta.normalized), out float distance) && distance <= delta.magnitude;
            if (danger.Intersects(controller.bounds) || crossed)
            {
                int lives = health.Lives;
                health.HitBySpikes();
                if (health.Lives < lives) GameAudio.TrampaCepo(danger.center);
            }
        }
        previousCenter = center;
    }

    bool OverFootprint(Vector3 center)
    {
        Bounds sensor = footprint;
        sensor.Expand(new Vector3(0.2f, 0f, 0.2f));
        return center.x >= sensor.min.x && center.x <= sensor.max.x &&
            center.z >= sensor.min.z && center.z <= sensor.max.z &&
            controller.bounds.min.y < footprint.min.y + 2.5f;
    }

    Bounds Measure()
    {
        Bounds bounds = visuals[0].bounds;
        for (int i = 1; i < visuals.Length; i++) bounds.Encapsulate(visuals[i].bounds);
        return bounds;
    }
}
