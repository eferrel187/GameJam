using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Trampas de los niveles 2 y 3 (lo crea LevelSetup):
///  - Hazard_Cylinder + (1): pilar de púas del pasillo. Gira siempre; cuando
///    el jugador va a salir del cuarto con la llave, va y vuelve por el pasillo.
///  - Hazard_Cylinder (2): rodillo del cuarto. Gira siempre; al coger la llave
///    recorre todo el cuarto de punta a punta (hay que agacharse o esquivarlo).
///  - Hazard_Cylinder (3) y (4): rodillo del pasillo. Gira siempre; se mueve
///    cuando el jugador ya salió del cuarto con la llave y pisa el suelo del medio.
///  - Suelo trampa -> suelo normal; púas y cepos fijos quitan vida.
///  - Puerta de salida: se abre al llegar (con la puerta de la llave abierta)
///    y al cruzarla se pasa al siguiente nivel.
///  - Al perder una vida, las trampas vuelven a su sitio.
/// </summary>
public class LevelRules : MonoBehaviour
{
    public GameObject breakableWall;

    Transform player;
    CharacterController controller;
    GuardHealth health;
    int lastLives = -1;

    readonly List<HazardCylinder> hazards = new List<HazardCylinder>();
    Bounds middleFloor;
    bool hasMiddleFloor;
    Vector3 roomDir;            // del pasillo hacia el cuarto
    Vector3 wallCenter;
    bool hasRoom;
    bool wasInRoomWithKey, leftRoomWithKey;

    ArchDoor keyDoor, exitDoor;
    bool leaving;
    string hint = "";
    float hintUntil;
    bool hintedRollers;

    bool isLevel3;
    GameObject pit;                 // foso de púas (como en el Nivel 1)
    Vector3 pitHome;
    readonly List<GameObject> openedFloors = new List<GameObject>();
    GameObject floorIzq;            // Nivel 2: se hunde al pisarlo con la llave
    GameObject spikesSafe, spikesDeadly;   // Nivel 3: púas del suelo que se invierten
    readonly Dictionary<GameObject, Vector3> spikeHome = new Dictionary<GameObject, Vector3>();
    bool spikesInverted;
    readonly HashSet<GameObject> spikeRaised = new HashSet<GameObject>();
    readonly Dictionary<GameObject, Bounds> spikeSector = new Dictionary<GameObject, Bounds>();

    const float CrouchClearance = 1.05f;   // altura libre sobre el suelo para pasar agachado

    void Start()
    {
        GameObject p = GameObject.Find("Player");
        if (p == null) { enabled = false; return; }
        player = p.transform;
        controller = p.GetComponent<CharacterController>();
        health = p.GetComponent<GuardHealth>();
        if (health != null) lastLives = health.Lives;

        List<GameObject> roots = gameObject.scene.GetRootGameObjects().ToList();
        GameObject Find(string n) => roots.FirstOrDefault(r => r.name == n && r.activeInHierarchy);

        // Restos copiados del Nivel1 (secuestro sin puertas): la celda
        // tapaba la salida y la secuencia no puede funcionar aquí.
        foreach (KidnapSequence k in FindObjectsByType<KidnapSequence>(FindObjectsSortMode.None))
            if (k.gameObject.scene == gameObject.scene && k.darkDoor == null)
            {
                k.gameObject.SetActive(false);
                GameObject cage = Find(k.targetName);
                if (cage) cage.SetActive(false);
            }

        isLevel3 = roots.Any(r => r.name == "Llave_Falsa");
        pit = roots.FirstOrDefault(r => r.name.StartsWith("Spikes_Platform") && LevelSetup.BoundsOf(r).max.y < -1f);
        if (pit != null)
        {
            pitHome = pit.transform.position;
            if (pit.GetComponentInChildren<Collider>() == null)
            {
                // Que el cuerpo caiga sobre las púas (y no al vacío).
                MeshFilter mf = pit.GetComponentInChildren<MeshFilter>();
                if (mf && mf.sharedMesh)
                {
                    BoxCollider box = mf.gameObject.AddComponent<BoxCollider>();
                    box.center = mf.sharedMesh.bounds.center;
                    box.size = mf.sharedMesh.bounds.size;
                }
            }
        }

        if (isLevel3) CleanLevel3(roots);

        SolidTrapFloor(roots);
        StaticKillers(roots);
        SetupBreakableWall(roots);
        if (isLevel3) SetupFloorSpikes(roots);
        // (Nivel 2: los suelos del medio ya no se hunden; activan el rodillo.)

        // El cuarto (aunque su rodillo se quite en el Nivel 3) marca la dirección.
        GameObject room = roots.FirstOrDefault(r => r.name == "Hazard_Cylinder (2)");
        if (breakableWall != null && room != null)
        {
            hasRoom = true;
            wallCenter = LevelSetup.Center(breakableWall);
            roomDir = LevelSetup.Flat(LevelSetup.Center(room) - wallCenter);
            // Solo el eje principal (el cuarto sale recto del pasillo).
            roomDir = Mathf.Abs(roomDir.x) > Mathf.Abs(roomDir.z)
                ? new Vector3(Mathf.Sign(roomDir.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(roomDir.z));
        }
        if (isLevel3 && room != null) { room.SetActive(false); room = null; }
        Vector3 corridor = Vector3.Cross(Vector3.up, roomDir).normalized;
        if (!hasRoom) corridor = Vector3.right;
        corridor = new Vector3(Mathf.Abs(corridor.x), 0, Mathf.Abs(corridor.z));

        // ---- Pilar del pasillo (dos mitades que se mueven juntas)
        foreach (string n in new[] { "Hazard_Cylinder", "Hazard_Cylinder (1)" })
        {
            GameObject g = Find(n);
            if (g == null) continue;
            HazardCylinder h = Add(g, 200f);
            h.moveAxis = corridor;
            h.moveSpeed = 1.8f;
            h.startDirection = -1f;
            h.activateWhen = () => GuardDoorAndKey.HasKey && InRoom() && DistanceToWall() < 3.5f;
            h.playSound = n == "Hazard_Cylinder";
        }
        LimitTravel(hazards.Where(h => h.name.StartsWith("Hazard_Cylinder") && !h.name.Contains("(2)") && !h.name.Contains("(3)") && !h.name.Contains("(4)")).ToList(),
            corridor, -5.5f, 0f);

        // ---- Rodillo del cuarto
        if (room != null)
        {
            HazardCylinder h = Add(room, 260f);
            FitRoller(h);
            h.moveAxis = roomDir;
            h.moveSpeed = 2.0f;
            h.startDirection = -1f;   // primero hacia la salida (se aleja de la llave)
            h.activateWhen = () => GuardDoorAndKey.HasKey;
            LimitTravel(new List<HazardCylinder> { h }, roomDir, -12f, 3f);
        }

        // ---- Rodillo del pasillo (dos mitades)
        List<HazardCylinder> pair = new List<HazardCylinder>();
        foreach (string n in new[] { "Hazard_Cylinder (3)", "Hazard_Cylinder (4)" })
        {
            GameObject g = Find(n);
            if (g == null) continue;
            HazardCylinder h = Add(g, 260f);
            // Al pisar trap floorDer o trap floorIzq: va y vuelve por el pasillo.
            h.moveAxis = corridor;
            h.moveSpeed = 2.4f;
            h.startDirection = -1f;
            h.activateWhen = () => OnMiddleFloor();
            h.playSound = n == "Hazard_Cylinder (3)";
            pair.Add(h);
        }
        if (pair.Count > 0)
        {
            RaiseForCrouch(pair);
            // Da la vuelta antes de la puerta (para poder abrirla de pie).
            LimitTravel(pair, corridor, -4.2f, 1.4f);
        }

        GuardDoorAndKey.KeyPicked += OnKeyPicked;
        GuardDoorAndKey.FakeDoorTried += OnFakeDoor;

        // ---- Puertas
        GameObject kd = Find("Puerta_Llave");
        GameObject ed = Find("Puerta_Salida");
        if (kd) keyDoor = kd.GetComponent<ArchDoor>();
        if (ed)
        {
            exitDoor = ed.GetComponent<ArchDoor>();
            BuildExit(ed);
        }
    }

    void OnDestroy()
    {
        GuardDoorAndKey.KeyPicked -= OnKeyPicked;
        GuardDoorAndKey.FakeDoorTried -= OnFakeDoor;
    }

    void OnKeyPicked()
    {
        // Nivel 3: con la llave verdadera la trampa cambia de lado (sin avisar).
        if (spikesSafe != null && !spikesInverted)
        {
            spikesInverted = true;
            HideSpikes();
        }
    }

    void Hint(string text)
    {
        hint = text;
        hintUntil = Time.time + 3.5f;
    }

    void OnGUI()
    {
        if (GameState.HudHidden || Time.time > hintUntil) return;
        float scale = Mathf.Clamp(Screen.width / 1280f, .65f, 2f);
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = Mathf.RoundToInt(19f * scale);
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = new Color(1f, .78f, .32f);
        GUI.Box(new Rect(Screen.width / 2f - 300f * scale, 290f * scale, 600f * scale, 45f * scale), hint, style);
    }

    // ------------------------------------------------------------ estado

    bool InRoom()
    {
        if (!hasRoom) return false;
        return Vector3.Dot(player.position - wallCenter, roomDir) > 0.4f;
    }

    float DistanceToWall() => hasRoom ? Mathf.Abs(Vector3.Dot(player.position - wallCenter, roomDir)) : 99f;

    bool OnMiddleFloor()
    {
        if (!hasMiddleFloor) return true;
        Vector3 p = player.position;
        return p.x > middleFloor.min.x - 0.2f && p.x < middleFloor.max.x + 0.2f &&
               p.z > middleFloor.min.z - 0.2f && p.z < middleFloor.max.z + 0.2f;
    }

    void Update()
    {
        if (player == null) return;

        // Perder una vida: trampas a su sitio.
        if (health != null)
        {
            if (lastLives >= 0 && health.Lives < lastLives)
            {
                foreach (HazardCylinder h in hazards) h.ResetHazard();
                wasInRoomWithKey = leftRoomWithKey = false;
                ClosePit();
                if (spikesSafe != null) ResetSpikes();
            }
            lastLives = health.Lives;
        }

        if (!hintedRollers && hazards.Any(h => h.name.Contains("(3)") && h.Active))
        {
            hintedRollers = true;
            Hint("¡Rodillo! Agáchate (C)");
        }

        // Nivel 2: con la llave, el suelo izquierdo se hunde al pisarlo.
        if (floorIzq != null && floorIzq.activeSelf && GuardDoorAndKey.HasKey && health != null && !health.IsKnockedDown)
        {
            Bounds b = LevelSetup.BoundsOf(floorIzq);
            Vector3 p = player.position;
            if (p.x > b.min.x + 0.15f && p.x < b.max.x - 0.15f && p.z > b.min.z + 0.15f && p.z < b.max.z - 0.15f)
                OpenPit(new List<GameObject> { floorIzq }, b.center);
        }

        // Nivel 3: las púas salen del suelo al pisar su sector.
        if (spikesSafe != null && health != null && !health.IsKnockedDown)
        {
            GameObject trap = spikesInverted ? spikesSafe : spikesDeadly;
            if (!spikeRaised.Contains(trap) && InSector(trap))
            {
                spikeRaised.Add(trap);
                StartCoroutine(MoveSpike(trap, true));
                GameAudio.TrampaCepo(spikeHome[trap]);
            }
        }

        if (GuardDoorAndKey.HasKey)
        {
            if (InRoom()) wasInRoomWithKey = true;
            else if (wasInRoomWithKey || !hasRoom) leftRoomWithKey = true;
        }

        // Puerta de salida: se abre al acercarse (con la primera ya abierta).
        if (exitDoor != null && !exitDoor.IsOpen && (keyDoor == null || keyDoor.IsOpen))
        {
            Vector3 d = player.position - exitDoor.Center; d.y = 0f;
            if (d.magnitude < 2.8f) exitDoor.Open();
        }

        if (exitDoor != null && exitDoor.IsOpen && !leaving)
        {
            Vector3 rel = player.position - exitDoor.Center; rel.y = 0f;
            Vector3 into = exitDoor.openTowards.normalized;
            float along = Vector3.Dot(rel, into);
            float lateral = (rel - into * along).magnitude;
            if (along > 0.45f && lateral < 1.4f)
            {
                leaving = true;
                GameFlow.LoadNextScene();
            }
        }
    }

    // ------------------------------------------------------------ montaje

    HazardCylinder Add(GameObject g, float spin)
    {
        HazardCylinder h = g.GetComponent<HazardCylinder>();
        if (h == null) h = g.AddComponent<HazardCylinder>();
        h.spinSpeed = spin;
        h.MeasureShape();
        hazards.Add(h);
        return h;
    }

    static float FloorBelow(Vector3 p)
    {
        foreach (RaycastHit hit in Physics.RaycastAll(p + Vector3.up * 0.5f, Vector3.down, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                     .OrderBy(h => h.distance))
            if (hit.collider.GetComponentInParent<CharacterController>() == null) return hit.point.y;
        return 0.23f;
    }

    float WallDistance(Vector3 from, Vector3 dir, float max)
    {
        foreach (RaycastHit hit in Physics.RaycastAll(from, dir, max, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                     .OrderBy(h => h.distance))
        {
            if (hit.transform == player || hit.transform.IsChildOf(player)) continue;
            if (hit.transform.name.StartsWith("Torch")) continue;   // las antorchas sobresalen de la pared
            return hit.distance;
        }
        return max;
    }

    /// El rodillo del cuarto ocupa todo el ancho y queda a la altura justa para
    /// pasar agachado.
    void FitRoller(HazardCylinder h)
    {
        Vector3 axis = h.Axis;
        // Un punto seguro dentro del cuarto: a la altura del hueco de la pared.
        Vector3 probe = h.Center;
        if (hasRoom) probe += axis * Vector3.Dot(wallCenter - probe, axis);
        float a = WallDistance(probe, axis, 8f);
        float b = WallDistance(probe, -axis, 8f);
        if (a >= 8f || b >= 8f) return;

        float width = a + b;
        Vector3 mid = probe + axis * (a - b) * 0.5f;
        float floor = FloorBelow(mid);
        mid.y = floor + CrouchClearance + h.Radius;
        if (isLevel3) h.FitLength(width - 0.12f, mid);
        else h.FitSegmentedLength(width - 0.12f, mid);
    }

    void RaiseForCrouch(List<HazardCylinder> list)
    {
        float floor = FloorBelow(list[0].Center);
        float lowest = list.Min(h => h.Center.y - h.Radius);
        float lift = floor + CrouchClearance - lowest;
        if (lift > 0f)
            foreach (HazardCylinder h in list) h.SetRestCenter(h.Center + Vector3.up * lift);
    }

    /// Recorrido (ida y vuelta) recortado por las paredes.
    void LimitTravel(List<HazardCylinder> list, Vector3 axis, float min, float max)
    {
        if (list.Count == 0) return;
        float extentAlong = list.Max(h => Mathf.Abs(Vector3.Dot(h.Axis, axis)) * h.HalfLength + h.Radius);
        Vector3 c = list.Aggregate(Vector3.zero, (s, h) => s + h.Center) / list.Count;
        c.y = FloorBelow(c) + 0.6f;
        float back = WallDistance(c, -axis, -min + extentAlong + 0.2f) - extentAlong - 0.15f;
        float fwd = WallDistance(c, axis, max + extentAlong + 0.2f) - extentAlong - 0.15f;
        foreach (HazardCylinder h in list)
        {
            h.minOffset = Mathf.Max(min, -Mathf.Max(0f, back));
            h.maxOffset = Mathf.Min(max, Mathf.Max(0f, fwd));
        }
    }

    void SolidTrapFloor(List<GameObject> roots)
    {
        bool any = false;
        foreach (GameObject r in roots.Where(r => r.name.StartsWith("trap floor")))
        {
            if (r.GetComponent<Collider>() == null)
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                BoxCollider box = r.AddComponent<BoxCollider>();
                if (mf && mf.sharedMesh) { box.center = mf.sharedMesh.bounds.center; box.size = mf.sharedMesh.bounds.size; }
            }
            Bounds b = LevelSetup.BoundsOf(r);
            if (!any) middleFloor = b; else middleFloor.Encapsulate(b);
            any = true;
        }
        hasMiddleFloor = any;

        // El foso de púas que había debajo ya no hace falta.
        foreach (GameObject r in roots.Where(r => r.name.StartsWith("Spikes_Platform")))
            if (LevelSetup.BoundsOf(r).max.y < -1f) r.SetActive(false);
    }

    void StaticKillers(List<GameObject> roots)
    {
        foreach (GameObject r in roots)
        {
            bool spikes = r.name.StartsWith("Spikes_Platform");
            bool trap = r.name.StartsWith("BearTrap");
            if ((!spikes && !trap) || !r.activeInHierarchy) continue;
            if (r.GetComponentInChildren<SpikeDamage>() != null || r.GetComponent<KillZone>() != null) continue;
            r.AddComponent<KillZone>().bearTrap = trap;
        }
    }

    void SetupBreakableWall(List<GameObject> roots)
    {
        if (breakableWall == null) return;
        Vector3 c = LevelSetup.Center(breakableWall);
        GameObject Near(string n) => roots.FirstOrDefault(r => r.name == n && Vector3.Distance(LevelSetup.Center(r), c) < 1.5f);

        // El componente va en un objeto aparte (la pared se oculta al romperse).
        GameObject holder = new GameObject("ParedRompible");
        holder.transform.position = c;
        BreakableWall bw = holder.AddComponent<BreakableWall>();
        bw.intact = breakableWall;
        if (!isLevel3)
        {
            // Cuatro mallas y una quinta fase sin pared: una E nueva por fase.
            // El resto bajo tiene su centro mas abajo: se busca por nombre,
            // no por la distancia vertical al centro de la pared completa.
            GameObject Phase(string n) => roots.FirstOrDefault(r => r.name == n);
            bw.stages = new[] {
                breakableWall, Phase("Wall_Double_Hole"), Phase("Wall_Hole"), Phase("Wall_Double_Broken")
            };
            bw.rubble = roots.Where(r => r.name.StartsWith("Bricks")
                && Vector3.Distance(LevelSetup.Center(r), c) < 3.5f).ToArray();
            return;
        }
        // 1º se abre un boquete (Wall_Hole); 2º cae entera y queda el paso libre
        // (Wall_Double_Broken deja un muro bajo que no se puede cruzar: no se usa).
        bw.partial = Near("Wall_Hole") ?? Near("Wall_Double_Hole");
        bw.broken = null;
        bw.hideAlways = new[] { Near("Wall_Double_Hole"), Near("Wall_Double_Broken") }
            .Where(g => g != null && g != bw.partial).ToArray();
    }

    // ------------------------------------------------------------ foso y púas

    /// Quita el suelo indicado y pone el foso de púas debajo (trampa del Nivel 1).
    void OpenPit(List<GameObject> floors, Vector3 center)
    {
        foreach (GameObject f in floors) { f.SetActive(false); openedFloors.Add(f); }
        GameAudio.TrampaSuelo(center);
        if (pit == null) return;
        Bounds pb = LevelSetup.BoundsOf(pit);
        pit.transform.position += new Vector3(center.x - pb.center.x, 0f, center.z - pb.center.z);
        pit.SetActive(true);
        KillZone k = pit.GetComponent<KillZone>();
        if (k == null) k = pit.AddComponent<KillZone>();
        k.armed = true;
    }

    void ClosePit()
    {
        foreach (GameObject f in openedFloors) if (f) f.SetActive(true);
        openedFloors.Clear();
        if (pit != null)
        {
            pit.SetActive(false);
            pit.transform.position = pitHome;
        }
    }

    /// Nivel 3: al intentar abrir con la llave falsa, se abre el suelo bajo los pies.
    void OnFakeDoor()
    {
        Vector3 p = player.position;
        List<GameObject> under = gameObject.scene.GetRootGameObjects()
            .Where(r => r.activeInHierarchy && r.name.StartsWith("Floor"))
            .Where(r =>
            {
                Bounds b = LevelSetup.BoundsOf(r);
                return p.x > b.min.x - 0.35f && p.x < b.max.x + 0.35f && p.z > b.min.z - 0.35f && p.z < b.max.z + 0.35f;
            }).ToList();
        if (under.Count > 0) OpenPit(under, new Vector3(p.x, 0f, p.z));
    }

    /// Nivel 3: menos púas y trampas (quedan solo las del suelo con su truco).
    void CleanLevel3(List<GameObject> roots)
    {
        foreach (GameObject r in roots)
        {
            if (r.name.StartsWith("BearTrap")) continue;
            if (!r.name.StartsWith("Spikes_Platform") || r == pit) continue;
            Bounds b = LevelSetup.BoundsOf(r);
            if (b.min.y > 1.0f) r.SetActive(false);           // techo y paredes
        }

        // Cepos: ninguno en el lado derecho (llave verdadera); en el izquierdo
        // solo dos, los más alejados de la llave falsa.
        GameObject fake = roots.FirstOrDefault(r => r.name == "Llave_Falsa");
        List<GameObject> traps = roots.Where(r => r.name.StartsWith("BearTrap")).ToList();
        foreach (GameObject t in traps.Where(t => LevelSetup.Center(t).x > 0f)) t.SetActive(false);
        if (fake != null)
        {
            Vector3 k = LevelSetup.Center(fake);
            foreach (GameObject t in traps.Where(t => LevelSetup.Center(t).x <= 0f)
                         .OrderByDescending(t => Vector3.Distance(LevelSetup.Center(t), k)).Skip(2))
                t.SetActive(false);
        }

        // Copias exactas en el mismo sitio que (17): sobran.
        GameObject deadly = roots.FirstOrDefault(r => r.name == "Spikes_Platform (17)");
        if (deadly != null)
        {
            Vector3 c = LevelSetup.Center(deadly);
            foreach (GameObject r in roots.Where(r => r != deadly && r.name.StartsWith("Spikes_Platform") && r.activeSelf))
                if (Vector3.Distance(LevelSetup.Center(r), c) < 0.1f) r.SetActive(false);
        }
    }

    void SetupFloorSpikes(List<GameObject> roots)
    {
        spikesSafe = roots.FirstOrDefault(r => r.name == "Spikes_Platform (2)" && r.activeSelf);
        spikesDeadly = roots.FirstOrDefault(r => r.name == "Spikes_Platform (17)" && r.activeSelf);
        if (spikesSafe == null || spikesDeadly == null) { spikesSafe = spikesDeadly = null; return; }

        foreach (GameObject g in new[] { spikesSafe, spikesDeadly })
        {
            spikeHome[g] = g.transform.position;
            spikeSector[g] = LevelSetup.BoundsOf(g);
            if (g.GetComponent<KillZone>() == null) g.AddComponent<KillZone>();
        }
        // Al empezar, todas escondidas bajo el suelo: no se ve dónde están.
        HideSpikes();
    }

    void ResetSpikes()
    {
        spikesInverted = false;
        HideSpikes();
    }

    void HideSpikes()
    {
        spikeRaised.Clear();
        SetSpikeInstant(spikesSafe, false);
        SetSpikeInstant(spikesDeadly, false);
    }

    bool InSector(GameObject g)
    {
        Bounds b = spikeSector[g];
        Vector3 p = player.position;
        return p.x > b.min.x + 0.1f && p.x < b.max.x - 0.1f && p.z > b.min.z + 0.1f && p.z < b.max.z - 0.1f;
    }

    const float SpikeDrop = 1.3f;

    void SetSpikeInstant(GameObject g, bool up)
    {
        if (g == null) return;
        g.transform.position = spikeHome[g] - (up ? Vector3.zero : Vector3.up * SpikeDrop);
        g.GetComponent<KillZone>().armed = up;
    }

    System.Collections.IEnumerator MoveSpike(GameObject g, bool up)
    {
        if (g == null) yield break;
        KillZone k = g.GetComponent<KillZone>();
        k.armed = up;   // al subir, pinchan desde que asoman
        Vector3 from = g.transform.position;
        Vector3 to = spikeHome[g] - (up ? Vector3.zero : Vector3.up * SpikeDrop);
        const float time = 0.18f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float e = 1f - (1f - t / time) * (1f - t / time);   // salen de golpe y frenan
            g.transform.position = Vector3.Lerp(from, to, e);
            yield return null;
        }
        g.transform.position = to;
    }

    void BuildExit(GameObject door)
    {
        ArchDoor d = door.GetComponent<ArchDoor>();
        Vector3 c = d.Center;
        Vector3 into = d.openTowards.normalized;
        float floor = FloorBelow(c - into * 0.8f);

        // Suelo y oscuridad detrás de la puerta (antes no había nada).
        GameObject ground = new GameObject("Suelo_Salida");
        ground.transform.position = new Vector3(c.x, floor - 0.2f, c.z) + into * 1.6f;
        ground.transform.rotation = Quaternion.LookRotation(into);
        ground.AddComponent<BoxCollider>().size = new Vector3(3f, 0.4f, 3.2f);

        GameObject dark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dark.name = "Oscuridad_Salida";
        Object.Destroy(dark.GetComponent<Collider>());
        dark.transform.position = new Vector3(c.x, floor + 2f, c.z) + into * 2.2f;
        dark.transform.rotation = Quaternion.LookRotation(into);
        dark.transform.localScale = new Vector3(5f, 5f, 3f);
        Renderer rd = dark.GetComponent<Renderer>();
        Material m = new Material(rd.sharedMaterial);
        m.color = Color.black;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.black);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
        rd.sharedMaterial = m;
        rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
