using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Nivel2: completa -> agujero pequeño -> agujero grande -> resto -> paso libre.
/// Cada cambio exige soltar E y volver a pulsarla; todos sueltan escombros.
/// Nivel3 conserva su derrumbe automático original. Lo configura LevelRules.
/// </summary>
public class BreakableWall : MonoBehaviour
{
    public GameObject intact;
    public GameObject partial;
    public GameObject broken;
    public GameObject[] hideAlways;
    public float secondStageDelay = 1f;
    public float reach = 1.8f;

    [Header("Nivel2: cuatro mallas, después paso libre")]
    public GameObject[] stages;
    public GameObject[] rubble;
    [Min(0f)] public float pressCooldown = 0.2f;
    public int CurrentStage { get; private set; }

    public bool IsBroken { get; private set; }
    bool started;
    Transform player;
    CharacterController controller;
    GuardHealth health;
    Bounds bounds;
    BoxCollider passageBlocker;
    bool ready;
    float nextPress;
    bool Staged => stages != null && stages.Length > 0;

    void Start()
    {
        GameObject p = GameObject.Find("Player");
        if (p)
        {
            player = p.transform;
            controller = p.GetComponent<CharacterController>();
            health = p.GetComponent<GuardHealth>();
        }
        if (!intact) { Debug.LogError("Falta la pared completa.", this); enabled = false; return; }
        bounds = Measure(intact);

        if (Staged)
        {
            if (stages.Length != 4 || System.Array.Exists(stages, g => !g))
            {
                Debug.LogError("Falta una fase: Wall, Wall_Double_Hole, Wall_Hole, Wall_Double_Broken.", this);
                enabled = false;
                return;
            }
            PrepareStages();
        }
        else
        {
            if (hideAlways != null)
                foreach (GameObject g in hideAlways) if (g) g.SetActive(false);
            if (partial) this.partial.SetActive(false);
            if (broken) broken.SetActive(false);
        }
        ready = true;
    }

    static Bounds Measure(GameObject g)
    {
        Renderer[] rs = g.GetComponentsInChildren<Renderer>(true);
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(g.transform.position, Vector3.one);
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    bool PlayerNear()
    {
        if (!ready || !player || started || IsBroken || GameState.Paused || GameState.HudHidden
            || GameState.CinematicCamera || (health && (health.IsKnockedDown || health.Lives <= 0))) return false;
        var mv = player.GetComponent<GuardPlayerMovement>();
        if (mv && !mv.enabled) return false;
        Vector3 c = controller ? controller.bounds.center : player.position + Vector3.up;
        return Vector3.Distance(c, bounds.ClosestPoint(c)) <= reach;
    }

    void Update()
    {
        if (PlayerNear() && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (Staged)
            {
                if (Time.time < nextPress) return;
                nextPress = Time.time + pressCooldown;
                AdvanceStage();
            }
            else StartCoroutine(Collapse());
        }
    }

    void PrepareStages()
    {
        foreach (GameObject stage in stages)
        {
            // Solo la piedra visible colisiona; el bloqueo común se quita al final.
            foreach (Collider c in stage.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (MeshFilter mf in stage.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;
                MeshCollider c = mf.GetComponent<MeshCollider>();
                if (!c) c = mf.gameObject.AddComponent<MeshCollider>();
                c.sharedMesh = mf.sharedMesh;
                c.convex = false;
                c.enabled = true;
            }
        }
        GameObject blocker = new GameObject("BloqueoHastaUltimaFase");
        blocker.transform.SetParent(transform, false);
        blocker.transform.position = bounds.center;
        passageBlocker = blocker.AddComponent<BoxCollider>();
        passageBlocker.size = bounds.size;

        if (rubble != null)
            foreach (GameObject pile in rubble)
                if (pile)
                    foreach (Collider c in pile.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        ApplyStage();
        CreateFirstBrick();
    }

    void AdvanceStage()
    {
        if (IsBroken) return;
        CurrentStage++;
        IsBroken = CurrentStage >= stages.Length;
        ApplyStage();
        GameAudio.TrampaSuelo(bounds.center);
        // La misma caída de piedras de antes, ahora en cada pulsación.
        Debris(IsBroken ? 18 : 5 + CurrentStage * 3);
    }

    void ApplyStage()
    {
        for (int i = 0; i < stages.Length; i++) stages[i].SetActive(i == CurrentStage);
        if (rubble != null)
            foreach (GameObject pile in rubble) if (pile) pile.SetActive(CurrentStage >= 2);
        if (passageBlocker) passageBlocker.enabled = !IsBroken;
    }

    void CreateFirstBrick()
    {
        // Un ladrillo suelto al empezar; la pila original aparece al abrir más.
        GameObject brick = GameObject.CreatePrimitive(PrimitiveType.Cube);
        brick.name = "PrimerLadrillo";
        brick.layer = 2;
        brick.GetComponent<Collider>().enabled = false;
        brick.transform.SetParent(transform, false);
        brick.transform.localScale = new Vector3(0.35f, 0.18f, 0.22f);

        Vector3 normal = bounds.size.x < bounds.size.z ? Vector3.right : Vector3.forward;
        Vector3 tangent = Vector3.Cross(Vector3.up, normal);
        Vector3 anchor = bounds.center - normal * 0.4f + tangent * 0.35f;
        float floor = bounds.min.y;
        if (rubble != null)
            foreach (GameObject pile in rubble)
                if (pile) { floor = Measure(pile).min.y; break; }
        // Sitúalo en la cara desde la que llega el jugador.
        if (player && Vector3.Dot(player.position - bounds.center, normal) > 0f)
            anchor += normal * 0.8f;
        brick.transform.position = new Vector3(anchor.x, floor + 0.09f, anchor.z);
        brick.transform.rotation = Quaternion.Euler(0f, 22f, 0f);
        Renderer source = intact.GetComponentInChildren<Renderer>(true);
        if (source) brick.GetComponent<Renderer>().sharedMaterial = source.sharedMaterial;
    }

    IEnumerator Collapse()
    {
        started = true;
        Vector3 at = bounds.center;

        // 1. Se cae una parte.
        GameAudio.TrampaSuelo(at);
        Swap(intact, partial);
        yield return new WaitForSeconds(secondStageDelay);

        // 2. Se derrumba por completo.
        GameAudio.TrampaSuelo(at);
        Swap(partial, broken);
        Debris(18);
        IsBroken = true;
    }

    /// Piedras que caen al derrumbarse (con la textura de la pared).
    void Debris(int count)
    {
        Renderer source = intact ? intact.GetComponentInChildren<Renderer>(true) : null;
        for (int i = 0; i < count; i++)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = "Escombro";
            rock.transform.SetParent(transform, true);
            float size = Random.Range(0.12f, 0.3f);
            rock.transform.localScale = new Vector3(size * Random.Range(1f, 1.8f), size, size * Random.Range(0.8f, 1.3f));
            rock.transform.SetPositionAndRotation(
                new Vector3(Random.Range(bounds.min.x + 0.2f, bounds.max.x - 0.2f),
                            Random.Range(bounds.min.y + 0.8f, bounds.max.y - 0.5f),
                            Random.Range(bounds.min.z, bounds.max.z)),
                Random.rotation);
            if (source) rock.GetComponent<Renderer>().sharedMaterial = source.sharedMaterial;
            // Ni la cámara ni el jugador chocan con los escombros.
            rock.layer = 2;   // Ignore Raycast
            if (controller) Physics.IgnoreCollision(rock.GetComponent<Collider>(), controller);
            if (passageBlocker) Physics.IgnoreCollision(rock.GetComponent<Collider>(), passageBlocker);
            Rigidbody rb = rock.AddComponent<Rigidbody>();
            rb.mass = 5f;
            rb.linearVelocity = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1.5f, 1.5f));
            StartCoroutine(Settle(rock, rb));
        }
    }

    static IEnumerator Settle(GameObject rock, Rigidbody rb)
    {
        yield return new WaitForSeconds(3f);
        if (rb) { rb.isKinematic = true; }
        // El jugador y la cámara no tropiezan con los escombros.
        if (rock) rock.GetComponent<Collider>().enabled = false;
    }

    static void Swap(GameObject from, GameObject to)
    {
        if (from) from.SetActive(false);
        if (!to) return;
        to.SetActive(true);
        foreach (MeshFilter mf in to.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh && mf.GetComponent<Collider>() == null)
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
    }

    void OnGUI()
    {
        if (GameState.HudHidden || !PlayerNear()) return;
        float scale = Mathf.Clamp(Screen.width / 1280f, .65f, 2f);
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = Mathf.RoundToInt(19f * scale);
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;
        float height = Staged ? 66f : 45f;
        string prompt = Staged ? "[E] Romper la pared\nFase " + (CurrentStage + 1) + "/5"
            : "[E] Empujar la pared";
        GUI.Box(new Rect(Screen.width / 2f - 230f * scale, Screen.height - 110f * scale,
            460f * scale, height * scale), prompt, style);
    }
}
