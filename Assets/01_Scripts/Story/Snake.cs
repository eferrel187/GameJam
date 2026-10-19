using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serpiente (hecha por código): repta en S por su zona; si el jugador se
/// acerca, lo persigue hasta morderle. La mordida invierte los controles
/// unos segundos (una nueva mordida renueva la duración, no la acumula).
/// </summary>
public class Snake : MonoBehaviour
{
    public static bool Frozen;

    [Header("Zona")]
    public Vector3 home;
    public Vector2 areaMin, areaMax;   // (x, z)

    [Header("Comportamiento")]
    public float patrolSpeed = 1.0f;
    public float chaseSpeed = 2.0f;
    public float detectRadius = 4.5f;
    public float giveUpRadius = 10f;
    public float biteRange = 0.95f;
    public float invertSeconds = 5f;

    enum State { Patrol, Chase, Bite, Retreat }
    State state;
    float stateClock, cooldown;

    const int Segments = 16;
    const float Spacing = 0.13f;
    readonly List<Vector3> trail = new List<Vector3>();
    Transform head, tongue;
    readonly Transform[] body = new Transform[Segments];
    Vector3 basePos;       // posición sin el vaivén
    float heading;         // grados
    float phase;
    Vector3 patrolTarget;
    float lunge;

    Transform player;
    CharacterController controller;
    GuardPlayerMovement movement;
    GuardHealth health;

    void Start()
    {
        GameObject p = GameObject.Find("Player");
        if (p)
        {
            player = p.transform;
            controller = p.GetComponent<CharacterController>();
            movement = p.GetComponent<GuardPlayerMovement>();
            health = p.GetComponent<GuardHealth>();
        }
        Build();
        ResetSnake();
    }

    void Build()
    {
        Material green = StoryKit.Mat(new Color(.22f, .42f, .13f), 0.55f);
        Material dark = StoryKit.Mat(new Color(.1f, .2f, .06f), 0.55f);
        Material belly = StoryKit.Mat(new Color(.55f, .55f, .25f), 0.4f);
        Material eye = StoryKit.Mat(new Color(1f, .85f, .1f), 0.9f, new Color(1f, .7f, 0f) * 0.8f);
        Material black = StoryKit.Mat(Color.black, 0.9f);
        Material red = StoryKit.Mat(new Color(.8f, .05f, .08f), 0.5f);

        for (int i = 0; i < Segments; i++)
        {
            float k = i / (float)(Segments - 1);
            float r = Mathf.Lerp(0.15f, 0.045f, k * k);
            Transform s = StoryKit.Prim(PrimitiveType.Sphere, "Cuerpo" + i, transform.position,
                new Vector3(r * 2f, r * 1.5f, Spacing * 2.3f), i % 3 == 1 ? dark : green, transform).transform;
            body[i] = s;
        }
        head = StoryKit.Prim(PrimitiveType.Sphere, "Cabeza", transform.position, new Vector3(0.26f, 0.15f, 0.34f), green, transform).transform;
        Transform jaw = StoryKit.Prim(PrimitiveType.Sphere, "Mandibula", Vector3.zero, new Vector3(0.8f, 0.5f, 0.9f), belly, head).transform;
        jaw.localPosition = new Vector3(0f, -0.25f, 0.05f);
        jaw.localScale = new Vector3(0.85f, 0.5f, 0.85f);
        for (int s = -1; s <= 1; s += 2)
        {
            Transform e = StoryKit.Prim(PrimitiveType.Sphere, "Ojo", Vector3.zero, Vector3.one, eye, head).transform;
            e.localPosition = new Vector3(0.33f * s, 0.32f, 0.22f);
            e.localScale = new Vector3(0.28f, 0.4f, 0.22f);
            Transform pu = StoryKit.Prim(PrimitiveType.Sphere, "Pupila", Vector3.zero, Vector3.one, black, e).transform;
            pu.localPosition = new Vector3(0.3f * s, 0f, 0.35f);
            pu.localScale = new Vector3(0.3f, 0.8f, 0.3f);
        }
        tongue = StoryKit.Prim(PrimitiveType.Cube, "Lengua", Vector3.zero, Vector3.one, red, head).transform;
        tongue.localPosition = new Vector3(0f, -0.1f, 0.6f);
        tongue.localScale = new Vector3(0.08f, 0.04f, 0.5f);
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    public void ResetSnake()
    {
        state = State.Patrol;
        stateClock = cooldown = 0f;
        lunge = 0f;
        basePos = home;
        basePos.y = StoryKit.GroundY(home, home.y);
        heading = Random.Range(0f, 360f);
        trail.Clear();
        Vector3 back = Quaternion.Euler(0f, heading, 0f) * Vector3.back;
        for (int i = 0; i < Segments * 3; i++) trail.Add(basePos + back * (i * Spacing / 3f));
        PickPatrolTarget();
        Pose();
    }

    void PickPatrolTarget()
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = new Vector3(Random.Range(areaMin.x, areaMax.x), basePos.y, Random.Range(areaMin.y, areaMax.y));
            if (HasGround(p)) { patrolTarget = p; return; }
        }
        patrolTarget = home;
    }

    bool HasGround(Vector3 p)
    {
        float g = StoryKit.GroundY(p, float.NegativeInfinity, 1.0f, 1.6f);
        return !float.IsNegativeInfinity(g) && Mathf.Abs(g - basePos.y) < 0.45f;
    }

    void Update()
    {
        if (Frozen || GameState.Narrating || GameState.CinematicCamera || GameState.Paused) { Pose(); return; }
        float dt = Time.deltaTime;
        stateClock += dt;
        cooldown -= dt;
        bool playerOk = player && health && !health.IsKnockedDown && movement && movement.enabled;
        Vector3 toPlayer = playerOk ? player.position - basePos : Vector3.one * 999f;
        float distP = new Vector2(toPlayer.x, toPlayer.z).magnitude;
        bool sameLevel = Mathf.Abs(toPlayer.y) < 1.2f;

        float speed = patrolSpeed;
        Vector3 target = patrolTarget;
        switch (state)
        {
            case State.Patrol:
                if (StoryKit.Flat(patrolTarget - basePos).magnitude < 0.4f || stateClock > 7f) { PickPatrolTarget(); stateClock = 0f; }
                if (playerOk && sameLevel && distP < detectRadius && cooldown <= 0f)
                {
                    state = State.Chase; stateClock = 0f;
                    StoryKit.One(StoryAssets.Get.serpienteSiseo, head.position, 0.9f, 0.1f, 14f);
                }
                break;
            case State.Chase:
                speed = chaseSpeed;
                target = player ? player.position : home;
                if (!playerOk || !sameLevel || distP > giveUpRadius) { state = State.Patrol; stateClock = 0f; break; }
                if (distP < biteRange) { state = State.Bite; stateClock = 0f; }
                break;
            case State.Bite:
                speed = 0f;
                target = player ? player.position : home;
                lunge = Mathf.Sin(Mathf.Clamp01(stateClock / 0.35f) * Mathf.PI);
                if (stateClock >= 0.17f && stateClock - dt < 0.17f)
                {
                    StoryKit.One(StoryAssets.Get.serpienteMordida, head.position, 1f, 0.08f, 14f);
                    if (playerOk && distP < biteRange + 0.35f && sameLevel)
                        movement.InvertControls(invertSeconds);   // renueva, no acumula
                }
                if (stateClock > 0.4f) { lunge = 0f; state = State.Retreat; stateClock = 0f; cooldown = 5f; }
                break;
            case State.Retreat:
                speed = patrolSpeed * 1.4f;
                target = basePos - StoryKit.Flat(toPlayer).normalized * 3f;
                if (stateClock > 2.2f) { state = State.Patrol; stateClock = 0f; PickPatrolTarget(); }
                break;
        }

        // Girar hacia el objetivo y reptar.
        Vector3 to = StoryKit.Flat(target - basePos);
        if (to.sqrMagnitude > 0.0025f)
        {
            float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            heading = Mathf.MoveTowardsAngle(heading, want, (state == State.Chase ? 220f : 140f) * dt);
        }
        if (speed > 0f)
        {
            Vector3 fwd = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            Vector3 next = basePos + fwd * speed * dt;
            if (HasGround(next + fwd * 0.3f))
            {
                basePos = next;
                basePos.y = Mathf.Lerp(basePos.y, StoryKit.GroundY(next, basePos.y, 0.6f, 1.2f), 0.3f);
            }
            else
            {
                heading += 150f * dt * 3f;
                if (state == State.Patrol) PickPatrolTarget();
            }
            phase += speed * dt * 7f;
        }

        // Vaivén lateral (en S): el rastro de la cabeza forma las curvas del cuerpo.
        Vector3 side = Quaternion.Euler(0f, heading, 0f) * Vector3.right;
        Vector3 headPos = basePos + side * Mathf.Sin(phase) * 0.16f;
        if (trail.Count == 0 || (headPos - trail[0]).sqrMagnitude > (Spacing / 3f) * (Spacing / 3f))
        {
            trail.Insert(0, headPos);
            if (trail.Count > Segments * 3 + 4) trail.RemoveAt(trail.Count - 1);
        }
        Pose();
    }

    void Pose()
    {
        if (!head || trail.Count < 2) return;
        Vector3 fwd = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
        Vector3 hp = trail[0] + Vector3.up * (0.1f + lunge * 0.22f) + fwd * (lunge * 0.35f);
        head.position = hp + fwd * 0.12f;
        head.rotation = Quaternion.LookRotation(fwd) * Quaternion.Euler(-lunge * 20f, 0f, 0f);
        // Lengua que entra y sale.
        float flick = Mathf.Repeat(Time.time * 1.7f + home.x, 1f);
        tongue.localScale = new Vector3(0.08f, 0.04f, flick < 0.18f || state == State.Chase ? 0.55f : 0.05f);

        for (int i = 0; i < Segments; i++)
        {
            int idx = Mathf.Min(trail.Count - 1, (i + 1) * 3);
            int prev = Mathf.Max(0, idx - 3);
            Vector3 a = trail[idx], b = trail[prev];
            float k = i / (float)(Segments - 1);
            float r = Mathf.Lerp(0.15f, 0.045f, k * k);
            body[i].position = a + Vector3.up * (r * 0.7f);
            Vector3 d = b - a;
            if (d.sqrMagnitude > 0.00001f) body[i].rotation = Quaternion.LookRotation(d.normalized);
        }
    }
}
