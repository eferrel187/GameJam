using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Nivel 4 · Trampas y llaves engañosas (se monta sobre Level4Rules):
///  - Llave falsa en la celda izquierda y la verdadera en la derecha.
///  - La emboscada (púas fuera de la celda falsa) solo se prepara si se recoge
///    la llave falsa, y salta al salir de esa celda.
///  - Púas del suelo ocultas con un jarrón roto o una calavera encima.
///  - Púas de los muros escondidas dentro de la pared hasta pasar por su zona.
///  - Salida con animación completa (puerta oscura) hacia el Nivel 5.
/// </summary>
[DefaultExecutionOrder(-50)]
public class Level4Story : MonoBehaviour
{
    Level4Rules rules;
    Transform player;
    GuardPlayerMovement movement;
    bool exiting;
    readonly List<WallSpikeTrap> wallTraps = new List<WallSpikeTrap>();

    void Awake()
    {
        rules = FindFirstObjectByType<Level4Rules>();
        if (!rules) { Debug.LogWarning("Nivel4 historia: no hay Level4Rules."); enabled = false; return; }
        SwapKeys();
        UseRoundArchDoor();
    }

    /// La puerta con llave usa las hojas dobles estilo Nivel 2 que hay en la
    /// escena (antes tapaban la hoja gótica y no dejaban pasar).
    void UseRoundArchDoor()
    {
        GameObject l = null, r = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (root.name == "Doors_RoundArch_L (1)") l = root;
            if (root.name == "Doors_RoundArch_R (1)") r = root;
        }
        ArchDoor door = rules.exitDoor;
        if (!l || !r || !door) return;
        if (door.leftLeaf && door.leftLeaf.gameObject != l) door.leftLeaf.gameObject.SetActive(false);
        if (door.rightLeaf && door.rightLeaf.gameObject != r) door.rightLeaf.gameObject.SetActive(false);
        l.transform.SetParent(door.transform, true);
        r.transform.SetParent(door.transform, true);
        door.leftLeaf = l.transform;
        door.rightLeaf = r.transform;
        door.openTowards = Vector3.forward;
    }

    /// Celdas del este vistas de frente (mirando al este): la izquierda es la del norte (z mayor).
    void SwapKeys()
    {
        GameObject real = rules.realKey, fake = rules.fakeKey;
        if (!real || !fake) return;
        bool realIsLeft = real.transform.position.z > fake.transform.position.z;
        if (!realIsLeft) return;   // ya está como se pide

        // Se intercambian los papeles (y los nombres) de los dos objetos.
        FakeKey fk = fake.GetComponent<FakeKey>();
        if (fk) DestroyImmediate(fk);
        string realName = real.name;
        real.name = fake.name;
        fake.name = realName;
        real.AddComponent<FakeKey>();
        rules.realKey = fake;
        rules.fakeKey = real;

        // El umbral de la celda falsa y sus púas pasan a la celda izquierda.
        float dz = real.transform.position.z - fake.transform.position.z;
        if (rules.fakeCellExit)
        {
            Vector3 p = rules.fakeCellExit.position;
            rules.fakeCellExit.position = new Vector3(p.x, p.y, real.transform.position.z);
        }
        if (rules.fakeKeySpikes != null)
            foreach (RisingSpikes s in rules.fakeKeySpikes)
                if (s) s.Relocate(new Vector3(0f, 0f, dz));
    }

    void Start()
    {
        if (!rules) return;
        GameObject p = GameObject.Find("Player");
        if (p) { player = p.transform; movement = p.GetComponent<GuardPlayerMovement>(); }

        // Púas del suelo: escondidas, con señal encima y un poco más de margen.
        int v = 0;
        if (rules.floorSpikes != null)
            foreach (RisingSpikes s in rules.floorSpikes)
            {
                if (!s) continue;
                bool ambush = rules.fakeKeySpikes != null && rules.fakeKeySpikes.Contains(s);
                if (!ambush && s.warningSeconds < 0.3f) s.warningSeconds = 0.3f;
                StoryKit.Marker(s.FootprintCenter, v++);
            }

        // Púas del muro: dentro de la pared hasta que el jugador pasa por delante.
        foreach (KillZone kz in FindObjectsByType<KillZone>(FindObjectsSortMode.None))
        {
            if (kz.bearTrap || !kz.name.StartsWith("Pinchos_techo_estaticos")) continue;
            Bounds b = StoryKit.Measure(kz.gameObject);
            if (b.center.y > 3f) continue;       // las del techo se quedan como están
            WallSpikeTrap trap = kz.gameObject.AddComponent<WallSpikeTrap>();
            Destroy(kz);
            wallTraps.Add(trap);
        }

        // La salida la controla la escena animada (no el detector de la secuencia).
        if (rules.catSequence) rules.catSequence.externalExit = true;

        GuardHealth.Respawned += OnRespawn;
        Debug.Log("Nivel4 historia: " + v + " señales de trampa, " + wallTraps.Count + " trampas de muro, llave falsa en z=" +
                  (rules.fakeKey ? rules.fakeKey.transform.position.z.ToString("0.0") : "?"));
    }

    void OnDestroy() { GuardHealth.Respawned -= OnRespawn; }

    void OnRespawn(GuardHealth h)
    {
        StoryReset.BearTraps(gameObject.scene);
    }

    void Update()
    {
#if UNITY_EDITOR
        // Pruebas en el editor: 1 celda izquierda (falsa), 2 celda derecha (verdadera),
        // 3 pasillo de las púas del muro, 4 delante de la puerta con llave.
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && player && movement && movement.enabled && !GameState.Narrating)
        {
            Vector3? to = kb.digit1Key.wasPressedThisFrame ? new Vector3(19.2f, 0.05f, 16.2f)
                        : kb.digit2Key.wasPressedThisFrame ? new Vector3(19.2f, 0.05f, 10f)
                        : kb.digit3Key.wasPressedThisFrame ? new Vector3(17f, 0.05f, 19.3f)
                        : kb.digit4Key.wasPressedThisFrame ? new Vector3(0f, 0.05f, 34.5f) : (Vector3?)null;
            if (to.HasValue)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                cc.enabled = false;
                player.position = to.Value;
                cc.enabled = true;
                Physics.SyncTransforms();
            }
        }
#endif
        if (exiting || !rules || !player || !rules.catSequence) return;
        if (!rules.catSequence.Finished || GameState.CinematicCamera || GameState.Narrating) return;
        if (movement && !movement.enabled) return;
        ArchDoor dark = rules.catSequence.darkDoor;
        if (!dark) return;
        Vector3 d = player.position - dark.Center;
        d.y = 0f;
        if (d.magnitude < 2.6f)
        {
            exiting = true;
            StartCoroutine(StoryKit.ExitThroughDoor(dark, dark.openTowards));
        }
    }
}

/// <summary>
/// Púas de muro: esperan escondidas dentro de la pared; cuando el jugador
/// entra en su zona, avisan (ruido) y salen de golpe; luego se esconden.
/// </summary>
public class WallSpikeTrap : MonoBehaviour
{
    public float warningSeconds = 0.3f;
    public float outSeconds = 0.12f;
    public float holdSeconds = 1.1f;
    public float backSeconds = 0.5f;
    public float zoneDepth = 2.6f;
    public float zoneHalfWidth = 1.4f;

    enum Phase { Hidden, Warning, Out, Hold, Back, Cooldown }
    Phase phase;
    float clock;
    Vector3 outPos, hiddenPos, wallNormal;
    Bounds outBounds;
    Renderer[] renderers;
    CharacterController controller;
    GuardHealth health;
    GuardPlayerMovement movement;

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        outPos = transform.position;
        outBounds = StoryKit.Measure(gameObject);
        wallNormal = FindWallNormal(outBounds);
        float depth = Mathf.Abs(Vector3.Dot(outBounds.size, new Vector3(Mathf.Abs(wallNormal.x), 0f, Mathf.Abs(wallNormal.z)))) + 0.12f;
        hiddenPos = outPos - wallNormal * depth;
        transform.position = hiddenPos;
        SetVisible(false);

        GameObject p = GameObject.Find("Player");
        if (p)
        {
            controller = p.GetComponent<CharacterController>();
            health = p.GetComponent<GuardHealth>();
            movement = p.GetComponent<GuardPlayerMovement>();
        }
        GuardHealth.Respawned += OnRespawn;
    }

    void OnDestroy() { GuardHealth.Respawned -= OnRespawn; }

    void OnRespawn(GuardHealth h)
    {
        phase = Phase.Hidden;
        clock = 0f;
        transform.position = hiddenPos;
        SetVisible(false);
    }

    void SetVisible(bool v)
    {
        foreach (Renderer r in renderers) if (r) r.enabled = v;
    }

    /// Normal del muro (hacia el pasillo) = hacia donde apuntan las púas.
    Vector3 FindWallNormal(Bounds b)
    {
        Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        float best = float.MaxValue;
        Vector3 normal = Vector3.left;
        foreach (Vector3 d in dirs)
            foreach (RaycastHit h in Physics.RaycastAll(b.center, d, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (h.transform.IsChildOf(transform) || h.collider.GetComponentInParent<CharacterController>()) continue;
                if (h.distance < best) { best = h.distance; normal = -d; }
            }
        return normal;
    }

    bool PlayerInZone()
    {
        if (!controller) return false;
        Vector3 c = controller.bounds.center;
        Vector3 rel = c - outBounds.center;
        float front = Vector3.Dot(rel, wallNormal);
        Vector3 along = Vector3.Cross(Vector3.up, wallNormal);
        float side = Mathf.Abs(Vector3.Dot(rel, along));
        return front > -0.3f && front < zoneDepth && side < zoneHalfWidth && Mathf.Abs(rel.y) < 2f;
    }

    void Update()
    {
        if (!health || GameState.Narrating || GameState.CinematicCamera || GameState.Paused) return;
        bool inZone = PlayerInZone();
        clock += Time.deltaTime;
        switch (phase)
        {
            case Phase.Hidden:
                if (inZone && movement && movement.enabled && !health.IsKnockedDown)
                {
                    phase = Phase.Warning; clock = 0f;
                    GameAudio.TrampaSuelo(outBounds.center);
                    SetVisible(true);
                }
                break;
            case Phase.Warning:
                // Asoman las puntas: aviso.
                transform.position = Vector3.Lerp(hiddenPos, outPos, 0.12f * Mathf.Clamp01(clock / warningSeconds));
                if (clock >= warningSeconds) { phase = Phase.Out; clock = 0f; }
                break;
            case Phase.Out:
                transform.position = Vector3.Lerp(hiddenPos, outPos, Mathf.SmoothStep(0.12f, 1f, clock / outSeconds));
                if (clock >= outSeconds) { transform.position = outPos; phase = Phase.Hold; clock = 0f; }
                break;
            case Phase.Hold:
                if (clock >= holdSeconds) { phase = Phase.Back; clock = 0f; }
                break;
            case Phase.Back:
                transform.position = Vector3.Lerp(outPos, hiddenPos, Mathf.SmoothStep(0f, 1f, clock / backSeconds));
                if (clock >= backSeconds) { transform.position = hiddenPos; SetVisible(false); phase = Phase.Cooldown; clock = 0f; }
                break;
            case Phase.Cooldown:
                if (clock > 0.8f && !inZone) phase = Phase.Hidden;
                break;
        }

        if (phase == Phase.Out || phase == Phase.Hold)
        {
            Bounds danger = StoryKit.Measure(gameObject);
            danger.Expand(new Vector3(-0.1f, -0.1f, -0.1f));
            if (danger.Intersects(controller.bounds))
            {
                int before = health.Lives;
                health.HitBySpikes();
                if (health.Lives < before) GameAudio.TrampaCepo(danger.center);
            }
        }
    }
}
