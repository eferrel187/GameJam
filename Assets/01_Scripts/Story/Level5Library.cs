using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Nivel 5 · La biblioteca y el acertijo de las cuatro estatuas.
///  - Estanterías en las dos salas laterales (E para revisarlas). Tres esconden
///    un papel con una pista; las demás, una trampa (suelo que se abre, púas
///    que salen del suelo o un rotor con púas que da una vuelta a la sala).
///  - Pistas releíbles con Tab. Las estanterías revisadas quedan vacías.
///  - Cuatro estatuas (1 a 4, de izquierda a derecha) que giran 90° con E,
///    con una flecha que indica hacia dónde miran y una rosa de los vientos.
///  - Solución: Este, Norte, Sur, Oeste. Al resolverlo se abre la salida.
///  - Cinco púas del suelo escondidas, marcadas con jarrones rotos o calaveras.
/// Norte = hacia la salida (+z). Este = derecha (+x).
/// </summary>
public class Level5Library : MonoBehaviour
{
    // Hacia dónde mira cada modelo tal y como está colocado en la escena (grados, 0 = norte).
    public static float FoxModelYaw = 180f;
    public static float StagModelYaw = 180f;
    // Frente de las estanterías del paquete (0 = el modelo mira a +z).
    public static float BookcaseFrontYaw = 0f;

    static readonly string[] DirNames = { "Norte", "Este", "Sur", "Oeste" };
    static readonly int[] Solution = { 1, 0, 2, 3 };       // E, N, S, O
    static readonly int[] StartDirs = { 2, 1, 3, 0 };      // ninguna correcta al principio

    enum TrapKind { None, Pit, Spikes, Rotor }

    class Shelf
    {
        public GameObject full, empty;
        public Vector3 wallPoint, normal, front;
        public Vector3 homePos; public Quaternion homeRot;
        public int clue = -1;
        public TrapKind trap;
        public bool done;
        public Bounds bounds;
        public Room room;
    }

    class Room
    {
        public float xMin, xMax, zMin, zMax;   // interior
    }

    class Statue
    {
        public Transform pivot;
        public Transform arrow;
        public Vector3 center;
        public float modelYaw;
        public int dir;
        public bool turning;
        public int index;
    }

    readonly List<Shelf> shelves = new List<Shelf>();
    readonly List<Statue> statues = new List<Statue>();
    readonly List<RisingSpikes> floorSpikes = new List<RisingSpikes>();
    readonly bool[] found = new bool[3];
    readonly List<GameObject> temporary = new List<GameObject>();
    readonly List<System.Action> restores = new List<System.Action>();

    Transform player;
    CharacterController controller;
    GuardPlayerMovement movement;
    GuardHealth health;
    GameObject spikeTemplate;
    ArchDoor entranceDoor, exitDoor;
    Vector3 spawn;
    bool solved, exiting, busy;
    string prompt = "", message = "";
    float messageUntil;
    RotorTrap rotor;

    // ------------------------------------------------------------ montaje

    void Start()
    {
        GameObject p = GameObject.Find("Player");
        if (!p) { enabled = false; return; }
        player = p.transform;
        controller = p.GetComponent<CharacterController>();
        movement = p.GetComponent<GuardPlayerMovement>();
        health = p.GetComponent<GuardHealth>();
        spawn = player.position;

        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.gameObject.scene == gameObject.scene).ToArray();

        BuildDoors(all);
        PrepareFloorSpikes(all);
        PrepareBearTraps(all);
        HideStaticRollers(all);
        BuildShelves();
        BuildStatues(all);
        BuildCompass(all);

        GuardHealth.Respawned += OnRespawn;
        if (entranceDoor) StoryNarrator.EntranceCutscene = () => StoryKit.EnterThroughDoor(entranceDoor, Vector3.forward, spawn);
    }

    void OnDestroy() { GuardHealth.Respawned -= OnRespawn; }

    void BuildDoors(Transform[] all)
    {
        Transform inDoor = all.FirstOrDefault(t => t.name == "Puerta_entrada_estatica");
        Transform outDoor = all.FirstOrDefault(t => t.name == "Puerta_salida_estatica");
        if (inDoor)
        {
            Bounds b = StoryKit.Measure(inDoor.gameObject);
            inDoor.gameObject.SetActive(false);
            entranceDoor = StoryKit.BuildNivel2Door("Puerta_Entrada_N5", new Vector3(b.center.x, 0f, b.center.z),
                Vector3.forward, Mathf.Clamp(b.size.x / 3f, 1f, 1.6f));
        }
        if (outDoor)
        {
            Bounds b = StoryKit.Measure(outDoor.gameObject);
            outDoor.gameObject.SetActive(false);
            exitDoor = StoryKit.BuildNivel2Door("Puerta_Salida_N5", new Vector3(b.center.x, 0f, b.center.z),
                Vector3.forward, Mathf.Clamp(b.size.x / 3f, 1f, 1.6f));
        }
    }

    void PrepareFloorSpikes(Transform[] all)
    {
        List<Transform> spikes = all.Where(t => t.name.StartsWith("Pinchos_suelo_estaticos") &&
                                                t.GetComponentInChildren<MeshFilter>()).ToList();
        // La sexta se guarda como molde de las púas de las estanterías.
        Transform spare = spikes.OrderByDescending(t => t.position.z).ThenByDescending(t => t.position.x).FirstOrDefault();
        int v = 0;
        foreach (Transform s in spikes)
        {
            foreach (Collider c in s.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            Bounds b = StoryKit.Measure(s.gameObject);
            float ground = StoryKit.GroundY(b.center, 0f, 0.5f, 3f);
            s.position += Vector3.up * (ground + 0.005f - b.min.y);
            if (s == spare && spikes.Count > 5)
            {
                spikeTemplate = s.gameObject;
                s.gameObject.SetActive(false);
                continue;
            }
            RisingSpikes rs = s.gameObject.AddComponent<RisingSpikes>();
            rs.activateOnStep = true;
            rs.warningSeconds = 0.3f;
            rs.raisedSeconds = 1.4f;
            floorSpikes.Add(rs);
            StoryKit.Marker(new Vector3(b.center.x, ground, b.center.z), v++);
        }
    }

    void PrepareBearTraps(Transform[] all)
    {
        foreach (Transform t in all.Where(t => t.name.StartsWith("Trampa_oso_estatica")))
        {
            // Los cepos de la sala de las estatuas estorbaban el acertijo: se quitan.
            if (t.position.z > 39f) { t.gameObject.SetActive(false); continue; }
            KillZone kz = t.GetComponent<KillZone>();
            if (kz == null) kz = t.gameObject.AddComponent<KillZone>();
            kz.bearTrap = true;
            kz.armed = true;
            kz.inset = 0.06f;
            foreach (Collider c in t.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }
    }

    void HideStaticRollers(Transform[] all)
    {
        // Los rodillos de adorno de las salas laterales tapaban el paso: el rotor los sustituye.
        foreach (Transform t in all.Where(t => (t.name.StartsWith("Rodillo_estatico") || t.name.StartsWith("Soporte_rodillo")) &&
                                               Mathf.Abs(t.position.x) > 8f))
            t.gameObject.SetActive(false);
        // Las velas de la plataforma central dejan sitio a la rosa de los vientos.
        foreach (Transform t in all.Where(t => t.name.StartsWith("Candles") && Mathf.Abs(t.position.z - 44f) < 1.2f && Mathf.Abs(t.position.x) < 2f))
            t.position = new Vector3(Mathf.Sign(t.position.x) * 1.6f, t.position.y, 42.5f);
    }

    // ------------------------------------------------------------ estanterías

    void BuildShelves()
    {
        Room left = new Room { xMin = -16.85f, xMax = -9.05f, zMin = 19.27f, zMax = 32.97f };
        Room right = new Room { xMin = 9.05f, xMax = 16.85f, zMin = 19.27f, zMax = 32.97f };
        // (sala, punto en la pared, normal hacia la sala, pista, trampa)
        AddShelf(left, new Vector3(-16.85f, 0f, 21.0f), Vector3.right, -1, TrapKind.Pit);
        AddShelf(left, new Vector3(-16.85f, 0f, 25.0f), Vector3.right, 0, TrapKind.None);
        AddShelf(left, new Vector3(-16.85f, 0f, 28.2f), Vector3.right, -1, TrapKind.Rotor);
        AddShelf(left, new Vector3(-12.6f, 0f, 32.97f), Vector3.back, -1, TrapKind.Spikes);
        AddShelf(right, new Vector3(16.85f, 0f, 21.0f), Vector3.left, 2, TrapKind.None);
        AddShelf(right, new Vector3(16.85f, 0f, 25.0f), Vector3.left, -1, TrapKind.Pit);
        AddShelf(right, new Vector3(16.85f, 0f, 28.2f), Vector3.left, 1, TrapKind.None);
        AddShelf(right, new Vector3(12.6f, 0f, 32.97f), Vector3.back, -1, TrapKind.Rotor);
    }

    void AddShelf(Room room, Vector3 wallPoint, Vector3 normal, int clue, TrapKind trap)
    {
        StoryAssets a = StoryAssets.Get;
        if (!a.bookcaseFull) return;
        float ground = StoryKit.GroundY(wallPoint + normal * 1f, 0f);
        float yaw = Quaternion.LookRotation(normal).eulerAngles.y + BookcaseFrontYaw;
        Vector3 feet = new Vector3(wallPoint.x, ground, wallPoint.z);
        GameObject full = StoryKit.Place(a.bookcaseFull, feet, 2.5f, yaw);
        Bounds b = StoryKit.Measure(full);
        float halfDepth = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(normal.x), 0f, Mathf.Abs(normal.z))));
        full.transform.position += normal * (halfDepth + 0.04f);
        full.name = "Estanteria" + (clue >= 0 ? "_Pista" : "");
        StoryKit.AddBoxColliders(full);
        GameObject empty = null;
        if (a.bookcaseEmpty)
        {
            empty = StoryKit.Place(a.bookcaseEmpty, feet, 2.5f, yaw);
            Bounds be = StoryKit.Measure(empty);
            empty.transform.position += StoryKit.Flat(StoryKit.Measure(full).center - be.center);
            empty.name = "Estanteria_Revisada";
            StoryKit.AddBoxColliders(empty);
            empty.SetActive(false);
        }
        Shelf s = new Shelf
        {
            full = full, empty = empty, wallPoint = wallPoint, normal = normal,
            clue = clue, trap = trap, room = room,
            bounds = StoryKit.Measure(full),
            homePos = full.transform.position, homeRot = full.transform.rotation,
        };
        s.front = wallPoint + normal * (halfDepth * 2f + 0.15f);
        shelves.Add(s);
    }

    Shelf NearestShelf()
    {
        Vector3 c = controller ? controller.bounds.center : player.position;
        Shelf best = null;
        float bestD = 1.9f;
        foreach (Shelf s in shelves)
        {
            Vector3 d = StoryKit.Flat(c - s.front);
            float along = Mathf.Abs(Vector3.Dot(d, Vector3.Cross(Vector3.up, s.normal)));
            float outD = Vector3.Dot(c - s.front, s.normal);
            if (along > 1.3f || outD > 1.9f || outD < -0.6f) continue;
            float dist = d.magnitude;
            if (dist < bestD) { bestD = dist; best = s; }
        }
        return best;
    }

    IEnumerator Search(Shelf s)
    {
        busy = true;
        StoryKit.One(StoryAssets.Get.libros, s.bounds.center, 1f);
        // Se gira hacia la estantería.
        Vector3 look = StoryKit.Flat(-s.normal);
        Quaternion from = player.rotation, to = Quaternion.LookRotation(look);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            player.rotation = Quaternion.Slerp(from, to, Mathf.Clamp01(t / 0.25f));
            yield return null;
        }
        s.done = true;
        SetChecked(s, true);

        if (s.clue >= 0)
        {
            found[s.clue] = true;
            int count = found.Count(f => f);
            busy = false;
            yield return StoryNarrator.Instance.ShowPaper("Pista " + (s.clue + 1) + " · encontraste " + count + " de 3",
                "«" + StoryText.Clues[s.clue] + "»");
            if (count == 3) Say("Ya tienes las tres pistas. Gira las estatuas del fondo (E).", 4f);
            yield break;
        }

        busy = false;
        switch (s.trap)
        {
            case TrapKind.Pit:
                Say("Solo libros viejos… ¡el suelo cruje!", 2.5f);
                StartCoroutine(PitTrap(s));
                break;
            case TrapKind.Spikes:
                Say("Solo libros viejos… ¡algo se mueve bajo tus pies!", 2.5f);
                SpikeTrap();
                break;
            case TrapKind.Rotor:
                Say("Solo libros viejos… ¡la pared tiembla! ¡Corre al centro!", 3f);
                StartRotor(s);
                break;
            default:
                Say("Solo libros viejos y polvo.", 2f);
                break;
        }
    }

    void SetChecked(Shelf s, bool isChecked)
    {
        if (s.empty)
        {
            s.full.SetActive(!isChecked);
            s.empty.SetActive(isChecked);
        }
    }

    // ------------------------------------------------------------ trampas de las estanterías

    IEnumerator PitTrap(Shelf s)
    {
        // Losas bajo el jugador (todas las que pisa, aunque esté en un borde).
        Vector3 c = player.position;
        List<Collider> tiles = Physics.OverlapBox(c + Vector3.down * 0.12f, new Vector3(0.45f, 0.1f, 0.45f),
                Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            .Where(col => col.name.StartsWith("Suelo_")).ToList();
        if (tiles.Count == 0) { SpikeTrap(); yield break; }

        StoryAssets a = StoryAssets.Get;
        var homes = tiles.Select(t => t.transform.position).ToList();
        StoryKit.One(a.crujido, c, 1f);

        // Aviso: las losas tiemblan.
        for (float t = 0f; t < 0.65f; t += Time.deltaTime)
        {
            for (int i = 0; i < tiles.Count; i++)
                tiles[i].transform.position = homes[i] + new Vector3(Random.Range(-1f, 1f), Random.Range(-0.5f, 0.5f), Random.Range(-1f, 1f)) * 0.025f;
            yield return null;
        }
        GameAudio.TrampaSuelo(c);
        bool shelfFalls = false;
        for (int i = 0; i < tiles.Count; i++)
        {
            Collider tile = tiles[i];
            Transform tileT = tile.transform;
            Vector3 home = homes[i];
            tileT.position = home;
            Renderer[] rends = tileT.GetComponentsInChildren<Renderer>();
            Bounds tb = tile.bounds;
            temporary.Add(BuildPit(tb));
            foreach (Renderer r in rends) r.enabled = false;
            tile.enabled = false;
            shelfFalls |= s.bounds.center.x > tb.min.x && s.bounds.center.x < tb.max.x &&
                          s.bounds.center.z > tb.min.z && s.bounds.center.z < tb.max.z;
            restores.Add(() =>
            {
                tileT.position = home;
                foreach (Renderer r in rends) if (r) r.enabled = true;
                if (tile) tile.enabled = true;
            });
        }
        if (shelfFalls) StartCoroutine(FallShelf(s));
    }

    IEnumerator FallShelf(Shelf s)
    {
        GameObject g = s.empty && s.empty.activeSelf ? s.empty : s.full;
        Vector3 p0 = g.transform.position;
        Quaternion r0 = g.transform.rotation;
        restores.Add(() => { g.transform.SetPositionAndRotation(p0, r0); g.SetActive(true); });
        float v = 0f;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            v += 9.8f * Time.deltaTime;
            g.transform.position += Vector3.down * v * Time.deltaTime;
            g.transform.rotation = Quaternion.AngleAxis(25f * Time.deltaTime, Vector3.Cross(Vector3.up, s.normal)) * g.transform.rotation;
            yield return null;
        }
        g.SetActive(false);
    }

    GameObject BuildPit(Bounds tile)
    {
        StoryAssets a = StoryAssets.Get;
        GameObject root = new GameObject("Foso_Estanteria");
        Material dark = StoryKit.Mat(new Color(.07f, .06f, .06f));
        float top = tile.max.y - 0.25f, depth = 3.2f, w = tile.size.x, d = tile.size.z;
        Vector3 c = new Vector3(tile.center.x, top - depth * 0.5f, tile.center.z);
        StoryKit.Box("Pared", c + Vector3.right * w * 0.5f, new Vector3(0.1f, depth, d), dark, root.transform);
        StoryKit.Box("Pared", c - Vector3.right * w * 0.5f, new Vector3(0.1f, depth, d), dark, root.transform);
        StoryKit.Box("Pared", c + Vector3.forward * d * 0.5f, new Vector3(w, depth, 0.1f), dark, root.transform);
        StoryKit.Box("Pared", c - Vector3.forward * d * 0.5f, new Vector3(w, depth, 0.1f), dark, root.transform);
        GameObject floor = StoryKit.Box("Fondo", new Vector3(c.x, top - depth, c.z), new Vector3(w, 0.2f, d), dark, root.transform, null, true);
        Vector3 bottom = new Vector3(c.x, top - depth + 0.1f, c.z);
        if (a.spikesPlatform)
        {
            GameObject sp = StoryKit.Place(a.spikesPlatform, bottom, 0f, 0f, root.transform);
            Bounds b = StoryKit.Measure(sp);
            float k = (Mathf.Min(w, d) - 0.15f) / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            sp.transform.localScale *= k;
            b = StoryKit.Measure(sp);
            sp.transform.position += bottom - new Vector3(b.center.x, b.min.y, b.center.z);
            KillZone kz = sp.AddComponent<KillZone>();
            kz.bearTrap = false;
            kz.inset = 0.05f;
        }
        GameObject lightGo = new GameObject("LuzFoso");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.position = new Vector3(c.x, top - depth + 1.4f, c.z);
        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, .35f, .2f);
        l.range = 4f;
        l.intensity = 2.5f;
        // Si cae, se muere al llegar abajo aunque no toque las púas.
        root.AddComponent<PitBottomKill>().bottomY = top - depth + 0.6f;
        return root;
    }

    void SpikeTrap()
    {
        if (!spikeTemplate) return;
        float ground = StoryKit.GroundY(player.position, player.position.y);
        GameObject g = Instantiate(spikeTemplate);
        g.SetActive(true);
        Bounds b = StoryKit.Measure(g);
        g.transform.position += new Vector3(player.position.x - b.center.x, ground + 0.005f - b.min.y, player.position.z - b.center.z);
        RisingSpikes rs = g.AddComponent<RisingSpikes>();
        rs.activateOnStep = false;
        rs.warningSeconds = 0.5f;
        rs.riseSeconds = 0.16f;
        rs.raisedSeconds = 1.3f;
        rs.TriggerTrap();
        temporary.Add(g);
    }

    void StartRotor(Shelf s)
    {
        if (rotor) Destroy(rotor.gameObject);
        GameObject go = new GameObject("Rotor_Pinchos");
        temporary.Add(go);
        rotor = go.AddComponent<RotorTrap>();
        Room r = s.room;
        const float inset = 1.5f;
        rotor.xMin = r.xMin + inset; rotor.xMax = r.xMax - inset;
        rotor.zMin = r.zMin + inset; rotor.zMax = r.zMax - inset;
        Vector3 along = Vector3.Cross(Vector3.up, s.normal);
        Vector3 wallSpot = s.wallPoint + along * 1.6f;
        rotor.hiddenPoint = wallSpot - s.normal * 0.7f;
        rotor.ground = StoryKit.GroundY(s.front, 0f);
        rotor.Begin(wallSpot + s.normal * inset);
    }

    // ------------------------------------------------------------ estatuas

    void BuildStatues(Transform[] all)
    {
        List<Transform> models = all.Where(t => (t.name.StartsWith("Statue_Fox") || t.name.StartsWith("Statue_Stag")) &&
                                                t.position.z > 39f && t.position.z < 47f)
            .OrderBy(t => t.position.x).ToList();
        for (int i = 0; i < models.Count && i < 4; i++)
        {
            Transform m = models[i];
            Bounds b = StoryKit.Measure(m.gameObject);
            Statue st = new Statue
            {
                index = i,
                center = new Vector3(b.center.x, b.min.y, b.center.z),
                modelYaw = m.name.StartsWith("Statue_Fox") ? FoxModelYaw : StagModelYaw,
                dir = StartDirs[i],
            };
            GameObject pivot = new GameObject("Giro_Estatua_" + (i + 1));
            pivot.transform.position = st.center;
            m.SetParent(pivot.transform, true);
            st.pivot = pivot.transform;
            foreach (Collider c in m.GetComponentsInChildren<Collider>()) c.enabled = true;
            StoryKit.AddBoxColliders(m.gameObject);

            float floorY = StoryKit.GroundY(new Vector3(b.center.x, 0.5f, b.center.z - 1.6f), 0f);
            st.arrow = BuildArrow(new Vector3(b.center.x, floorY + 0.03f, b.center.z), Mathf.Max(b.extents.x, b.extents.z) + 0.15f);
            // Placa con el número delante (al sur).
            Vector3 plate = new Vector3(b.center.x, floorY, b.center.z - Mathf.Max(b.extents.z, 0.6f) - 0.9f);
            Material stone = StoryKit.Mat(new Color(.18f, .16f, .15f));
            StoryKit.Box("Placa_" + (i + 1), plate + Vector3.up * 0.35f, new Vector3(0.5f, 0.7f, 0.12f), stone, null);
            StoryKit.Label3D((i + 1).ToString(), plate + new Vector3(0f, 0.45f, -0.07f), Quaternion.identity, 0.045f,
                new Color(1f, .8f, .35f));
            ApplyYaw(st, st.dir * 90f);
            statues.Add(st);
        }
    }

    Transform BuildArrow(Vector3 at, float startRadius)
    {
        GameObject root = new GameObject("Flecha_Mirada");
        root.transform.position = at;
        Material gold = StoryKit.Mat(new Color(1f, .75f, .25f), 0.6f, new Color(1f, .6f, .15f) * 1.6f);
        float len = 0.9f;
        StoryKit.Box("Asta", at + Vector3.forward * (startRadius + len * 0.5f), new Vector3(0.12f, 0.03f, len), gold, root.transform);
        Vector3 tip = at + Vector3.forward * (startRadius + len + 0.12f);
        StoryKit.Box("Punta", tip + new Vector3(-0.13f, 0f, -0.12f), new Vector3(0.1f, 0.03f, 0.42f), gold, root.transform, Quaternion.Euler(0f, 38f, 0f));
        StoryKit.Box("Punta", tip + new Vector3(0.13f, 0f, -0.12f), new Vector3(0.1f, 0.03f, 0.42f), gold, root.transform, Quaternion.Euler(0f, -38f, 0f));
        return root.transform;
    }

    void ApplyYaw(Statue st, float yaw)
    {
        st.pivot.rotation = Quaternion.Euler(0f, yaw - st.modelYaw, 0f);
        st.arrow.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    Statue NearestStatue()
    {
        Vector3 c = player.position;
        Statue best = null;
        float bestD = 2.2f;
        foreach (Statue s in statues)
        {
            float d = StoryKit.Flat(c - s.center).magnitude;
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }

    IEnumerator Turn(Statue s)
    {
        s.turning = true;
        float from = s.dir * 90f;
        s.dir = (s.dir + 1) % 4;
        float to = from + 90f;
        StoryKit.One(StoryAssets.Get.estatuaGira, s.center + Vector3.up, 1f, 0.06f);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            ApplyYaw(s, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 0.6f)));
            yield return null;
        }
        ApplyYaw(s, to);
        s.turning = false;
        CheckSolved();
    }

    void CheckSolved()
    {
        if (solved || statues.Count < 4) return;
        for (int i = 0; i < 4; i++) if (statues[i].dir != Solution[i]) return;
        solved = true;
        StartCoroutine(OpenExit());
    }

    IEnumerator OpenExit()
    {
        StoryKit.One(StoryAssets.Get.acierto, null, 0.9f, 0f);
        yield return new WaitForSeconds(0.6f);
        if (!exitDoor) yield break;
        // Plano de la puerta abriéndose.
        StoryKit.Puppet pp = StoryKit.Puppet.Take();
        StoryKit.BeginCinematic();
        Camera cam = Camera.main;
        Vector3 doorC = exitDoor.Center;
        Vector3 back = cam.transform.position;
        Quaternion backR = cam.transform.rotation;
        yield return StoryKit.MoveCamera(cam, new Vector3(doorC.x + 1.5f, 3.4f, doorC.z - 7.5f), doorC, 1.0f);
        exitDoor.Open();
        yield return new WaitForSeconds(exitDoor.openSeconds + 0.6f);
        yield return StoryKit.MoveCamera(cam, back, back + backR * Vector3.forward, 0.6f);
        pp?.Release(true);
        StoryKit.EndCinematic();
        Say("¡La puerta del fondo se abrió!", 3f);
    }

    // ------------------------------------------------------------ bucle

    void Update()
    {
        prompt = "";
        if (!player || busy || GameState.Narrating || GameState.CinematicCamera || GameState.Paused) return;
        if (health && health.IsKnockedDown) return;
        if (movement && !movement.enabled) return;
        Keyboard kb = Keyboard.current;
#if UNITY_EDITOR
        // Pruebas en el editor: 1 estatuas, 2 sala izquierda, 3 sala derecha,
        // 4 acertijo casi resuelto (falta girar la 4), 5 las tres pistas.
        if (kb != null)
        {
            Vector3? to = kb.digit1Key.wasPressedThisFrame ? new Vector3(0f, 0.05f, 40.5f)
                        : kb.digit2Key.wasPressedThisFrame ? new Vector3(-12.9f, 0.05f, 26f)
                        : kb.digit3Key.wasPressedThisFrame ? new Vector3(12.9f, 0.05f, 26f) : (Vector3?)null;
            if (to.HasValue)
            {
                controller.enabled = false;
                player.position = to.Value;
                controller.enabled = true;
                Physics.SyncTransforms();
            }
            if (kb.digit4Key.wasPressedThisFrame)
                for (int i = 0; i < statues.Count; i++)
                {
                    statues[i].dir = i == 3 ? (Solution[i] + 3) % 4 : Solution[i];
                    ApplyYaw(statues[i], statues[i].dir * 90f);
                }
            if (kb.digit5Key.wasPressedThisFrame) for (int i = 0; i < 3; i++) found[i] = true;
        }
#endif

        if (kb != null && kb.tabKey.wasPressedThisFrame)
        {
            StartCoroutine(ShowJournal());
            return;
        }

        if (solved && !exiting && exitDoor && exitDoor.IsOpen && !exitDoor.IsMoving)
        {
            if (StoryKit.Flat(player.position - exitDoor.Center).magnitude < 3.2f)
            {
                exiting = true;
                StartCoroutine(StoryKit.ExitThroughDoor(exitDoor, Vector3.forward));
                return;
            }
        }

        Statue st = NearestStatue();
        if (st != null)
        {
            prompt = "[E] Girar la estatua " + (st.index + 1) + "  ·  ahora mira al " + DirNames[st.dir];
            if (solved) prompt = "Estatua " + (st.index + 1) + " · mira al " + DirNames[st.dir];
            else if (kb != null && kb.eKey.wasPressedThisFrame && !st.turning) StartCoroutine(Turn(st));
            return;
        }

        Shelf sh = NearestShelf();
        if (sh != null)
        {
            if (sh.done) prompt = "Estantería revisada";
            else
            {
                prompt = "[E] Revisar la estantería";
                if (kb != null && kb.eKey.wasPressedThisFrame) StartCoroutine(Search(sh));
            }
        }
    }

    IEnumerator ShowJournal()
    {
        busy = true;
        string body = "";
        for (int i = 0; i < 3; i++)
            body += (i > 0 ? "\n\n" : "") + (found[i] ? "«" + StoryText.Clues[i] + "»" : "— Pista " + (i + 1) + ": sin descubrir —");
        string heading = "Pistas encontradas (" + found.Count(f => f) + " de 3)";
        yield return StoryNarrator.Instance.ShowPaper(heading, body, "E · Enter  para cerrar");
        busy = false;
    }

    void Say(string text, float seconds)
    {
        message = text;
        messageUntil = Time.time + seconds;
    }

    // ------------------------------------------------------------ reinicio

    void OnRespawn(GuardHealth h)
    {
        StopAllCoroutines();
        busy = false;
        foreach (System.Action r in restores) r();
        restores.Clear();
        foreach (GameObject g in temporary) if (g) Destroy(g);
        temporary.Clear();
        rotor = null;
        foreach (RisingSpikes rs in floorSpikes) if (rs) rs.ResetTrap();
        StoryReset.BearTraps(gameObject.scene);
        foreach (Statue s in statues) { s.turning = false; ApplyYaw(s, s.dir * 90f); }
        message = "";
    }

    // ------------------------------------------------------------ HUD

    void OnGUI()
    {
        if (GameState.HudHidden) return;
        float scale = Mathf.Clamp(Screen.width / 1280f, .65f, 2f);
        GUIStyle box = new GUIStyle(GUI.skin.box)
        {
            fontSize = Mathf.RoundToInt(18f * scale),
            alignment = TextAnchor.MiddleCenter,
        };
        box.normal.textColor = Color.white;

        int count = found.Count(f => f);
        string hud = "Pistas " + count + "/3   ·   [Tab] releer";
        GUI.Box(new Rect(Screen.width - 330f * scale, 18f * scale, 312f * scale, 40f * scale), hud, box);

        if (!string.IsNullOrEmpty(prompt))
            GUI.Box(new Rect(Screen.width / 2f - 300f * scale, Screen.height - 100f * scale, 600f * scale, 45f * scale), prompt, box);

        if (Time.time < messageUntil && !string.IsNullOrEmpty(message))
        {
            GUIStyle m = new GUIStyle(box) { fontSize = Mathf.RoundToInt(21f * scale) };
            m.normal.textColor = new Color(1f, .85f, .5f);
            GUI.Box(new Rect(Screen.width / 2f - 340f * scale, 90f * scale, 680f * scale, 50f * scale), message, m);
        }
    }

    // ------------------------------------------------------------ rosa de los vientos

    void BuildCompass(Transform[] all)
    {
        Transform plat = all.FirstOrDefault(t => t.name == "Plataforma_central_sin_eventos");
        Vector3 c = plat ? StoryKit.Measure(plat.gameObject).center : new Vector3(0f, 0.25f, 44f);
        float top = plat ? StoryKit.Measure(plat.gameObject).max.y : 0.25f;
        Vector3 at = new Vector3(c.x, top + 0.012f, c.z);
        Material stone = StoryKit.Mat(new Color(.12f, .11f, .1f));
        Material gold = StoryKit.Mat(new Color(1f, .75f, .25f), 0.6f, new Color(1f, .6f, .15f) * 1.2f);
        Material silver = StoryKit.Mat(new Color(.75f, .75f, .78f), 0.6f, new Color(.4f, .4f, .45f));
        StoryKit.Prim(PrimitiveType.Cylinder, "Rosa_Base", at, new Vector3(2.6f, 0.01f, 2.6f), stone, null);
        for (int k = 0; k < 4; k++)
        {
            Quaternion q = Quaternion.Euler(0f, k * 90f, 0f);
            Material m = k == 0 ? gold : silver;
            StoryKit.Box("Rosa_Punta", at + Vector3.up * 0.01f + q * Vector3.forward * 0.55f,
                new Vector3(0.16f, 0.02f, 1.1f), m, null, q);
            StoryKit.Box("Rosa_Punta", at + Vector3.up * 0.012f + q * Vector3.forward * 1.0f,
                new Vector3(0.28f, 0.02f, 0.28f), m, null, q * Quaternion.Euler(0f, 45f, 0f));
            Vector3 lp = at + Vector3.up * 0.03f + q * Vector3.forward * 1.45f;
            StoryKit.Label3D(DirNames[k].Substring(0, 1), lp, Quaternion.Euler(90f, 0f, 0f), 0.05f,
                k == 0 ? new Color(1f, .8f, .35f) : new Color(.9f, .9f, .92f));
        }
        // Letras grandes en los muros de la sala de las estatuas.
        Color wallC = new Color(1f, .78f, .35f);
        StoryKit.Label3D("N", new Vector3(0f, 5.3f, 47.7f), Quaternion.identity, 0.1f, wallC);
        StoryKit.Label3D("E", new Vector3(12.75f, 3.6f, 43f), Quaternion.Euler(0f, 90f, 0f), 0.1f, wallC);
        StoryKit.Label3D("O", new Vector3(-12.75f, 3.6f, 43f), Quaternion.Euler(0f, -90f, 0f), 0.1f, wallC);
    }
}

/// <summary>Muerte segura al llegar al fondo de un foso.</summary>
public class PitBottomKill : MonoBehaviour
{
    public float bottomY;
    GuardHealth health;
    Transform player;

    void Start()
    {
        GameObject p = GameObject.Find("Player");
        if (p) { player = p.transform; health = p.GetComponent<GuardHealth>(); }
    }

    void Update()
    {
        if (player && health && player.position.y < bottomY && !health.IsKnockedDown) health.HitBySpikes();
    }
}

/// <summary>
/// Rotor con púas que sale de la pared, da una vuelta completa a la sala y
/// vuelve a esconderse. Mata al tocarlo.
/// </summary>
public class RotorTrap : MonoBehaviour
{
    public float xMin, xMax, zMin, zMax, ground;
    public Vector3 hiddenPoint;
    public float speed = 3.1f;
    public float radius = 0.45f;
    public float height = 1.7f;

    Transform spinner;
    AudioSource loop;
    CharacterController controller;
    GuardHealth health;

    public void Begin(Vector3 pathStart)
    {
        GameObject p = GameObject.Find("Player");
        if (p) { controller = p.GetComponent<CharacterController>(); health = p.GetComponent<GuardHealth>(); }
        BuildModel();
        StartCoroutine(Run(pathStart));
    }

    void BuildModel()
    {
        StoryAssets a = StoryAssets.Get;
        spinner = new GameObject("Giro").transform;
        spinner.SetParent(transform, false);
        if (a.hazardCylinder)
        {
            GameObject m = Instantiate(a.hazardCylinder, spinner);
            foreach (Collider c in m.GetComponentsInChildren<Collider>(true)) Destroy(c);
            Bounds b = StoryKit.Measure(m);
            // Eje largo en vertical.
            if (b.size.x >= b.size.y && b.size.x >= b.size.z) m.transform.rotation = Quaternion.Euler(0f, 0f, 90f) * m.transform.rotation;
            else if (b.size.z >= b.size.y && b.size.z >= b.size.x) m.transform.rotation = Quaternion.Euler(90f, 0f, 0f) * m.transform.rotation;
            b = StoryKit.Measure(m);
            m.transform.localScale *= height / Mathf.Max(0.01f, b.size.y);
            b = StoryKit.Measure(m);
            m.transform.position += spinner.position - b.center;
            b = StoryKit.Measure(m);
            radius = Mathf.Max(b.extents.x, b.extents.z);
        }
        else
        {
            StoryKit.Prim(PrimitiveType.Cylinder, "Rotor", spinner.position, new Vector3(0.8f, height * 0.5f, 0.8f),
                StoryKit.Mat(new Color(.3f, .28f, .26f), .5f), spinner);
        }
    }

    Vector3 Center(Vector3 xz) => new Vector3(xz.x, ground + height * 0.5f + 0.05f, xz.z);

    IEnumerator Run(Vector3 start)
    {
        StoryAssets a = StoryAssets.Get;
        transform.position = Center(hiddenPoint);
        loop = StoryKit.Loop(a.rotor, transform.position, 0.9f, 20f, transform);
        StoryKit.One(a.crujido, transform.position, 1f);

        // Sale de la pared despacio (aviso).
        Vector3 from = Center(hiddenPoint), to = Center(start);
        for (float t = 0f; t < 1.1f; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 1.1f));
            yield return null;
        }

        // Una vuelta completa por la sala.
        float per = 2f * ((xMax - xMin) + (zMax - zMin));
        float s0 = Param(start);
        for (float s = 0f; s < per; s += speed * Time.deltaTime)
        {
            transform.position = Center(PointAt(s0 + s));
            yield return null;
        }

        // Vuelve a la pared.
        from = transform.position;
        to = Center(hiddenPoint);
        for (float t = 0f; t < 1.0f; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(from, to, t / 1.0f);
            yield return null;
        }
        Destroy(gameObject);
    }

    // Recorrido del rectángulo: (xMin,zMin) -> (xMax,zMin) -> (xMax,zMax) -> (xMin,zMax) -> ...
    Vector3 PointAt(float s)
    {
        float w = xMax - xMin, h = zMax - zMin, per = 2f * (w + h);
        s = Mathf.Repeat(s, per);
        if (s < w) return new Vector3(xMin + s, 0f, zMin);
        s -= w;
        if (s < h) return new Vector3(xMax, 0f, zMin + s);
        s -= h;
        if (s < w) return new Vector3(xMax - s, 0f, zMax);
        s -= w;
        return new Vector3(xMin, 0f, zMax - s);
    }

    float Param(Vector3 p)
    {
        float w = xMax - xMin, h = zMax - zMin;
        float px = Mathf.Clamp(p.x, xMin, xMax), pz = Mathf.Clamp(p.z, zMin, zMax);
        float dB = Mathf.Abs(pz - zMin), dR = Mathf.Abs(px - xMax), dT = Mathf.Abs(pz - zMax), dL = Mathf.Abs(px - xMin);
        float m = Mathf.Min(dB, dR, dT, dL);
        if (m == dB) return px - xMin;
        if (m == dR) return w + (pz - zMin);
        if (m == dT) return w + h + (xMax - px);
        return 2f * w + h + (zMax - pz);
    }

    void Update()
    {
        if (spinner) spinner.Rotate(0f, 540f * Time.deltaTime, 0f, Space.World);
        if (!controller || !health || GameState.Narrating || GameState.CinematicCamera) return;
        Vector3 c = controller.bounds.center;
        Vector3 d = c - transform.position;
        float flat = new Vector2(d.x, d.z).magnitude;
        if (flat < radius + controller.radius * controller.transform.lossyScale.x + 0.05f &&
            Mathf.Abs(d.y) < height * 0.5f + controller.height * 0.5f * controller.transform.lossyScale.y)
        {
            int before = health.Lives;
            health.HitBySpikes();
            if (health.Lives < before) GameAudio.TrampaCepo(c);
        }
    }
}
