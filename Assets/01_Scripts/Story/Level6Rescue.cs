using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Nivel 6 · El rescate final.
///  - Tres llaves repartidas (contador 0/3) y una jaula con tres cerraduras.
///  - Cuenta atrás de 02:00 que empieza al terminar el contexto; una mano sale
///    poco a poco de una puerta al ritmo del tiempo (luz roja, zumbido y latidos
///    cada vez más fuertes). A 00:00 la mano se lleva al gatito y se reinicia.
///  - Púas ocultas con señales, cepos, caída de los puentes = muerte inmediata.
///  - Cuatro serpientes que persiguen y muerden (controles invertidos 5 s).
///  - Rescate: la jaula se rompe, el gatito baila con música, salen juntos por
///    la puerta y empieza el epílogo.
/// </summary>
public class Level6Rescue : MonoBehaviour
{
    [Header("Ajustes")]
    public float timeLimit = 120f;
    [Tooltip("Segundos de controles invertidos por cada mordida de serpiente.")]
    public float invertSeconds = 5f;
    public float fallDeathY = -0.9f;

    enum State { Waiting, Running, Rescue, Defeat }
    State state = State.Waiting;

    // Posiciones del mapa (Nivel6.unity).
    static readonly Vector3 CagePos = new Vector3(3.0f, 0f, 37.0f);
    static readonly Vector3 HandDoorPos = new Vector3(4.92f, 0f, 37.0f);
    static readonly Vector3[] KeyPositions =
    {
        new Vector3(-14.0f, 0f, 19.8f),   // plataforma izquierda
        new Vector3(14.0f, 0f, 18.0f),    // plataforma derecha
        new Vector3(-10.0f, 0f, 6.0f),    // final del puente del oeste
    };

    Transform player;
    CharacterController controller;
    GuardPlayerMovement movement;
    GuardHealth health;
    Vector3 spawn;

    ArchDoor entranceDoor, exitDoor, handDoor;
    readonly List<GameObject> keys = new List<GameObject>();
    readonly List<Snake> snakes = new List<Snake>();
    readonly List<RisingSpikes> floorSpikes = new List<RisingSpikes>();
    int keyCount;
    float timeLeft;

    // Jaula y gatito
    GameObject cageRoot;
    readonly List<Transform> bars = new List<Transform>();
    readonly List<GameObject> locks = new List<GameObject>();
    GameObject cat;
    Bounds cageBounds;

    // Mano
    Transform hand, palm;
    readonly List<Transform> joints = new List<Transform>();
    readonly List<Quaternion> jointRest = new List<Quaternion>();
    Light threatLight;
    AudioSource drone;
    float nextBeat, lastTick;
    Vector3 handHidden, handAbove;
    Quaternion handFace;

    bool fallingDeath;
    float fallSpeed;
    string prompt = "", message = "";
    float messageUntil;

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
        PrepareTraps(all);
        BuildCage();
        BuildHand();
        BuildKeys();
        BuildSnakes();
        ResetLevel();

        GuardHealth.Damaged += OnDamaged;
        GuardHealth.Respawned += OnRespawn;
        StoryNarrator.ContextFinished += OnContextFinished;
        if (entranceDoor) StoryNarrator.EntranceCutscene = () => StoryKit.EnterThroughDoor(entranceDoor, Vector3.forward, spawn);
    }

    void OnDestroy()
    {
        GuardHealth.Damaged -= OnDamaged;
        GuardHealth.Respawned -= OnRespawn;
        StoryNarrator.ContextFinished -= OnContextFinished;
        Snake.Frozen = false;
    }

    void BuildDoors(Transform[] all)
    {
        Transform inDoor = all.FirstOrDefault(t => t.name == "Puerta_entrada_estatica");
        Transform outDoor = all.FirstOrDefault(t => t.name == "Puerta_salida_estatica");
        if (inDoor)
        {
            Bounds b = StoryKit.Measure(inDoor.gameObject);
            inDoor.gameObject.SetActive(false);
            entranceDoor = StoryKit.BuildNivel2Door("Puerta_Entrada_N6", new Vector3(b.center.x, 0f, b.center.z), Vector3.forward, 1f);
        }
        if (outDoor)
        {
            Bounds b = StoryKit.Measure(outDoor.gameObject);
            outDoor.gameObject.SetActive(false);
            exitDoor = StoryKit.BuildNivel2Door("Puerta_Salida_N6", new Vector3(b.center.x, 0f, b.center.z), Vector3.forward, 1f);
        }
        // Puerta de la que sale la mano (al borde este de la plataforma de la jaula).
        handDoor = StoryKit.BuildNivel2Door("Puerta_Mano_N6", HandDoorPos, Vector3.right, 1f);
        StoryAssets a = StoryAssets.Get;
        GameObject dark = StoryKit.Box("Oscuridad_Mano", HandDoorPos + new Vector3(1.3f, 2.2f, 0f), new Vector3(2.4f, 4.6f, 3.4f),
            a.darkness ? a.darkness : StoryKit.Mat(Color.black), null);
        dark.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void PrepareTraps(Transform[] all)
    {
        int v = 0;
        foreach (Transform s in all.Where(t => t.name.StartsWith("Pinchos_suelo_estaticos") && t.GetComponentInChildren<MeshFilter>()))
        {
            foreach (Collider c in s.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            Bounds b = StoryKit.Measure(s.gameObject);
            if (b.min.y < -2f)
            {
                // Púas del fondo del abismo: siempre a la vista, matan al caer.
                KillZone kz = s.GetComponent<KillZone>();
                if (kz == null) kz = s.gameObject.AddComponent<KillZone>();
                kz.bearTrap = false;
                kz.armed = true;
                continue;
            }
            float ground = StoryKit.GroundY(b.center, 0f, 0.5f, 3f);
            s.position += Vector3.up * (ground + 0.005f - b.min.y);
            RisingSpikes rs = s.gameObject.AddComponent<RisingSpikes>();
            rs.activateOnStep = true;
            rs.warningSeconds = 0.3f;
            rs.raisedSeconds = 1.4f;
            floorSpikes.Add(rs);
            StoryKit.Marker(new Vector3(b.center.x, ground, b.center.z), v++);
        }
        foreach (Transform t in all.Where(t => t.name.StartsWith("Trampa_oso_estatica")))
        {
            KillZone kz = t.GetComponent<KillZone>();
            if (kz == null) kz = t.gameObject.AddComponent<KillZone>();
            kz.bearTrap = true;
            kz.armed = true;
            kz.inset = 0.06f;
            foreach (Collider c in t.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }
    }

    void BuildCage()
    {
        if (cageRoot) Destroy(cageRoot);
        bars.Clear();
        locks.Clear();
        float ground = StoryKit.GroundY(CagePos, 0f);
        Vector3 c = new Vector3(CagePos.x, ground, CagePos.z);
        cageRoot = new GameObject("Jaula_Gatito");
        cageRoot.transform.position = c;
        Material iron = StoryKit.Mat(new Color(.16f, .15f, .14f), 0.55f);
        Material rust = StoryKit.Mat(new Color(.3f, .18f, .1f), 0.3f);
        Material gold = StoryKit.Mat(new Color(.95f, .72f, .25f), 0.7f, new Color(.6f, .4f, .1f));
        const float R = 0.62f, H = 1.25f;
        StoryKit.Prim(PrimitiveType.Cylinder, "Base", c + Vector3.up * 0.05f, new Vector3(R * 2.25f, 0.05f, R * 2.25f), iron, cageRoot.transform);
        bars.Add(StoryKit.Prim(PrimitiveType.Cylinder, "Tapa", c + Vector3.up * (H + 0.06f), new Vector3(R * 2.2f, 0.04f, R * 2.2f), iron, cageRoot.transform).transform);
        bars.Add(StoryKit.Prim(PrimitiveType.Sphere, "Remate", c + Vector3.up * (H + 0.16f), new Vector3(0.18f, 0.18f, 0.18f), rust, cageRoot.transform).transform);
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f;
            Vector3 at = c + new Vector3(Mathf.Cos(a) * R, H * 0.5f + 0.08f, Mathf.Sin(a) * R);
            bars.Add(StoryKit.Prim(PrimitiveType.Cylinder, "Barrote", at, new Vector3(0.05f, H * 0.5f, 0.05f), iron, cageRoot.transform).transform);
        }
        bars.Add(StoryKit.Prim(PrimitiveType.Cylinder, "Aro", c + Vector3.up * 0.65f, new Vector3(R * 2.1f, 0.02f, R * 2.1f), rust, cageRoot.transform).transform);
        // Tres cerraduras en el lado que mira al jugador (oeste).
        for (int i = 0; i < 3; i++)
        {
            Vector3 at = c + new Vector3(-R - 0.05f, 0.4f + i * 0.28f, 0f);
            locks.Add(StoryKit.Box("Cerradura", at, new Vector3(0.08f, 0.16f, 0.14f), gold, cageRoot.transform));
        }
        BoxCollider col = cageRoot.AddComponent<BoxCollider>();
        col.center = Vector3.up * (H * 0.5f + 0.1f);
        col.size = new Vector3(R * 2.1f, H + 0.2f, R * 2.1f);
        cageBounds = new Bounds(c + Vector3.up * (H * 0.5f), new Vector3(R * 2f, H, R * 2f));

        cat = StoryKit.MakeCat(c + Vector3.up * 0.09f, Vector3.left, 0.42f, cageRoot.transform);
    }

    void BuildHand()
    {
        StoryAssets a = StoryAssets.Get;
        if (!a.monsterHand) return;
        GameObject h = Instantiate(a.monsterHand);
        h.name = "Mano_Nivel6";
        h.transform.localScale = Vector3.one * 1.7f;
        foreach (Collider c in h.GetComponentsInChildren<Collider>(true)) Destroy(c);
        hand = h.transform;
        foreach (Transform tr in hand.GetComponentsInChildren<Transform>(true))
        {
            if (tr.name.StartsWith("Dedo")) { joints.Add(tr); jointRest.Add(tr.localRotation); }
            if (tr.name == "Palma") palm = tr;
        }
        Vector3 into = Vector3.right;
        Vector3 doorC = HandDoorPos + Vector3.up * 1.1f;
        handHidden = doorC + into * 2.4f;
        Vector3 grab = new Vector3(cageBounds.center.x, cageBounds.max.y + 0.1f, cageBounds.center.z);
        handAbove = grab + Vector3.up * 0.5f + into * 0.3f;
        handFace = Quaternion.LookRotation(-into);

        GameObject lg = new GameObject("Luz_Amenaza");
        lg.transform.position = HandDoorPos + new Vector3(-0.6f, 2.2f, 0f);
        threatLight = lg.AddComponent<Light>();
        threatLight.type = LightType.Point;
        threatLight.color = new Color(1f, .12f, .08f);
        threatLight.range = 9f;
        threatLight.intensity = 0f;
    }

    void BuildKeys()
    {
        StoryAssets a = StoryAssets.Get;
        foreach (Vector3 k in KeyPositions)
        {
            float g = StoryKit.GroundY(k, 0f);
            // Pivote en el centro de la llave para que gire sobre sí misma.
            GameObject key = new GameObject("Llave_Jaula");
            key.transform.position = new Vector3(k.x, g + 1.0f, k.z);
            GameObject model;
            if (a.key) model = StoryKit.Place(a.key, new Vector3(k.x, g + 0.75f, k.z), 0.5f, 0f);
            else model = StoryKit.Box("Llave", new Vector3(k.x, g + 1f, k.z), new Vector3(0.1f, 0.4f, 0.05f), StoryKit.Mat(Color.yellow), null);
            model.transform.position += key.transform.position - StoryKit.Measure(model).center;
            model.transform.SetParent(key.transform, true);
            foreach (Renderer r in key.GetComponentsInChildren<Renderer>())
                foreach (Material m in r.materials)
                    if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(.5f, .35f, .05f)); }
            GameObject glow = new GameObject("Brillo");
            glow.transform.SetParent(key.transform, false);
            glow.transform.position = StoryKit.Measure(key).center;
            Light l = glow.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, .8f, .35f); l.range = 2.5f; l.intensity = 1.6f;
            keys.Add(key);
        }
    }

    void BuildSnakes()
    {
        var zones = new[]
        {
            (home: new Vector3(-9f, 0f, 17f), min: new Vector2(-14.4f, 13.5f), max: new Vector2(-5.6f, 20.5f)),
            (home: new Vector3(11f, 0f, 21f), min: new Vector2(7.6f, 17.5f), max: new Vector2(14.4f, 26.5f)),
            (home: new Vector3(-2.5f, 0f, 36.2f), min: new Vector2(-4.5f, 35.5f), max: new Vector2(1.2f, 40.5f)),
            (home: new Vector3(1f, 0f, 24f), min: new Vector2(-0.4f, 11f), max: new Vector2(2.4f, 32f)),
        };
        foreach (var z in zones)
        {
            GameObject go = new GameObject("Serpiente");
            go.transform.position = z.home;
            Snake s = go.AddComponent<Snake>();
            s.home = z.home;
            s.areaMin = z.min;
            s.areaMax = z.max;
            s.invertSeconds = invertSeconds;
            snakes.Add(s);
        }
    }

    // ------------------------------------------------------------ reinicio

    void ResetLevel()
    {
        StopAllCoroutines();
        state = State.Waiting;
        timeLeft = timeLimit;
        keyCount = 0;
        fallingDeath = false;
        Snake.Frozen = false;
        foreach (GameObject k in keys) if (k) k.SetActive(true);
        foreach (RisingSpikes rs in floorSpikes) if (rs) rs.ResetTrap();
        StoryReset.BearTraps(gameObject.scene);
        foreach (Snake s in snakes) if (s && s.isActiveAndEnabled) s.ResetSnake();
        if (movement) movement.RestoreControls();
        if (!cageRoot || !cageRoot.activeSelf || cageRoot.transform.parent != null || !cat ||
            cat.transform.parent != cageRoot.transform || bars.Any(b => !b || !b.gameObject.activeSelf))
            BuildCage();
        if (hand)
        {
            hand.SetParent(null, true);
            hand.SetPositionAndRotation(handHidden, handFace);
            SetCurl(-0.2f);
            hand.gameObject.SetActive(false);
        }
        if (threatLight) threatLight.intensity = 0f;
        if (drone) { Destroy(drone.gameObject); drone = null; }
        if (handDoor) handDoor.ResetClosed();
    }

    void OnRespawn(GuardHealth h) => ResetLevel();

    void OnDamaged(GuardHealth h)
    {
        if (state == State.Running) state = State.Waiting;
        if (drone) drone.volume *= 0.3f;
    }

    void OnContextFinished()
    {
        if (state != State.Waiting) return;
        state = State.Running;
        timeLeft = timeLimit;
        lastTick = Mathf.Ceil(timeLeft);
        if (handDoor && !handDoor.IsOpen) handDoor.Open();
        if (hand) hand.gameObject.SetActive(true);
        StoryAssets a = StoryAssets.Get;
        if (!drone) drone = StoryKit.Loop(a.zumbidoMano, HandDoorPos + Vector3.up, 0.1f, 30f);
        GameAudio.ManoSale(HandDoorPos);
        Say("¡La mano empieza a salir! Reúne las 3 llaves.", 3f);
    }

    // ------------------------------------------------------------ bucle

    void Update()
    {
        prompt = "";
        if (!player) return;
        UpdateFallDeath();
        if (state != State.Running) return;

        float dt = Time.deltaTime;
        if (!health.IsKnockedDown) timeLeft -= dt;
        UpdateThreat();

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            StartCoroutine(Defeat());
            return;
        }

        if (health.IsKnockedDown || !movement.enabled || GameState.CinematicCamera || GameState.Narrating) return;

#if UNITY_EDITOR
        // Pruebas en el editor: 1 junto a la jaula, 2 las tres llaves, 3 quedan 8 s, 4 plataforma izquierda.
        Keyboard dk = Keyboard.current;
        if (dk != null)
        {
            Vector3? to = dk.digit1Key.wasPressedThisFrame ? new Vector3(1.4f, 0.05f, 37f)
                        : dk.digit4Key.wasPressedThisFrame ? new Vector3(-9f, 0.05f, 15f) : (Vector3?)null;
            if (to.HasValue)
            {
                controller.enabled = false;
                player.position = to.Value;
                controller.enabled = true;
                Physics.SyncTransforms();
            }
            if (dk.digit2Key.wasPressedThisFrame) { foreach (GameObject k in keys) if (k) k.SetActive(false); keyCount = 3; }
            if (dk.digit3Key.wasPressedThisFrame) timeLeft = 8f;
        }
#endif

        // Llaves
        Vector3 c = controller.bounds.center;
        foreach (GameObject k in keys)
        {
            if (!k || !k.activeSelf) continue;
            Bounds kb = StoryKit.Measure(k);
            if (Vector3.Distance(c, kb.ClosestPoint(c)) < 1.2f)
            {
                k.SetActive(false);
                keyCount++;
                GameAudio.Llave();
                Say("Llave " + keyCount + " de 3", 2f);
            }
        }

        // Jaula
        float dCage = StoryKit.Flat(player.position - cageBounds.center).magnitude;
        if (dCage < 1.9f)
        {
            if (keyCount >= 3)
            {
                prompt = "[E] Abrir la jaula";
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    StartCoroutine(Rescue());
            }
            else prompt = "La jaula tiene 3 cerraduras · llaves " + keyCount + "/3";
        }
    }

    void LateUpdate()
    {
        // Las llaves flotan y giran.
        foreach (GameObject k in keys)
            if (k && k.activeSelf) k.transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
    }

    void UpdateFallDeath()
    {
        if (!health) return;
        if (!health.IsKnockedDown && movement.enabled && player.position.y < fallDeathY &&
            state != State.Rescue && state != State.Defeat)
        {
            // Caer de un puente: muerte inmediata (sigue cayendo hasta el fondo).
            int before = health.Lives;
            health.Hit();
            if (health.Lives < before) { fallingDeath = true; fallSpeed = 6f; }
        }
        if (fallingDeath && health.IsKnockedDown && controller)
        {
            fallSpeed = Mathf.Min(30f, fallSpeed + 30f * Time.deltaTime);
            float ground = StoryKit.GroundY(player.position, -4.5f, 0.2f, 20f);
            float y = Mathf.Max(ground, player.position.y - fallSpeed * Time.deltaTime);
            player.position = new Vector3(player.position.x, y, player.position.z);
            if (y <= ground + 0.001f) fallingDeath = false;
        }
        else if (!health.IsKnockedDown) fallingDeath = false;
    }

    void UpdateThreat()
    {
        float p = 1f - timeLeft / timeLimit;
        // La mano sale al ritmo de la cuenta atrás.
        if (hand)
        {
            float e = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 1f, p));
            Vector3 side = Vector3.forward;
            float shake = Mathf.Lerp(0.01f, 0.06f, p * p);
            Vector3 wobble = side * Mathf.Sin(Time.time * 6f) * shake + Vector3.up * Mathf.Sin(Time.time * 4.3f) * shake;
            hand.SetPositionAndRotation(Vector3.Lerp(handHidden, handAbove, e) + wobble,
                Quaternion.Slerp(handFace, handFace * Quaternion.Euler(32f, 0f, 0f), e));
            SetCurl(Mathf.Lerp(-0.2f, -0.45f, e) + Mathf.Sin(Time.time * Mathf.Lerp(3f, 11f, p)) * Mathf.Lerp(0.04f, 0.12f, p));
        }
        if (threatLight)
            threatLight.intensity = Mathf.Lerp(0.3f, 7f, p * p) * (0.85f + 0.15f * Mathf.Sin(Time.time * 13f));
        if (drone)
        {
            drone.volume = Mathf.Lerp(0.12f, 0.95f, p);
            drone.pitch = Mathf.Lerp(0.8f, 1.15f, p);
        }
        StoryAssets a = StoryAssets.Get;
        // Latidos cada vez más rápidos en la segunda mitad.
        if (p > 0.45f && Time.time >= nextBeat)
        {
            nextBeat = Time.time + Mathf.Lerp(1.3f, 0.38f, Mathf.InverseLerp(0.45f, 1f, p));
            StoryKit.One(a.latido, null, Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(0.45f, 1f, p)), 0f);
        }
        // Tic de reloj en los últimos 10 segundos.
        if (timeLeft <= 10f && Mathf.Ceil(timeLeft) < lastTick)
        {
            lastTick = Mathf.Ceil(timeLeft);
            StoryKit.One(a.tic, null, 0.9f, 0f);
        }
    }

    void SetCurl(float amount)
    {
        for (int i = 0; i < joints.Count; i++)
        {
            bool thumb = joints[i].name.StartsWith("Dedo0");
            float angle = amount * (thumb ? 45f : 62f);
            joints[i].localRotation = jointRest[i] * Quaternion.Euler(angle, 0f, 0f);
        }
    }

    void Say(string text, float seconds)
    {
        message = text;
        messageUntil = Time.time + seconds;
    }

    // ------------------------------------------------------------ derrota

    IEnumerator Defeat()
    {
        state = State.Defeat;
        Snake.Frozen = true;
        StoryKit.Puppet pp = StoryKit.Puppet.Take();
        StoryKit.BeginCinematic();
        Camera cam = Camera.main;
        StoryAssets a = StoryAssets.Get;
        Vector3 cc = cageBounds.center;
        yield return StoryKit.MoveCamera(cam, cc + new Vector3(-3.2f, 1.6f, -2.2f), cc + Vector3.up * 0.3f, 0.8f);

        if (hand)
        {
            Vector3 grab = new Vector3(cc.x, cageBounds.max.y + 0.05f, cc.z);
            Vector3 low = grab + Vector3.up * 0.12f + Vector3.right * 0.1f;
            Vector3 from = hand.position;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.8f);
                hand.position = Vector3.Lerp(from, low, k);
                SetCurl(Mathf.Lerp(-0.45f, 1f, k));
                yield return null;
            }
            SetCurl(1f);
            GameAudio.ManoAgarra(grab);
            StoryKit.One(a.maullidoAsustado, cc, 1f, 0.02f);
            Collider col = cageRoot.GetComponent<Collider>();
            if (col) col.enabled = false;
            cageRoot.transform.SetParent(palm ? palm : hand, true);
            yield return new WaitForSeconds(0.6f);
            GameAudio.ManoArrastra(grab);
            Vector3 start = hand.position;
            Quaternion startR = hand.rotation;
            for (float t = 0f; t < 1.8f; t += Time.deltaTime)
            {
                float k = t / 1.8f;
                float e = k * k * k;
                hand.SetPositionAndRotation(Vector3.Lerp(start, handHidden + Vector3.up * 0.3f, e) + Random.insideUnitSphere * 0.015f * (1f - k),
                    Quaternion.Slerp(startR, handFace, e));
                yield return null;
            }
            cageRoot.SetActive(false);
            hand.gameObject.SetActive(false);
        }
        if (drone) { Destroy(drone.gameObject); drone = null; }
        if (threatLight) threatLight.intensity = 0f;
        yield return new WaitForSeconds(0.7f);

        // Derrota: se pierde una vida y se reinicia el nivel con su contexto.
        cageRoot.transform.SetParent(null, true);
        pp?.Release(true);
        StoryKit.EndCinematic();
        state = State.Waiting;
        Snake.Frozen = false;
        health.ForceHit();
    }

    // ------------------------------------------------------------ rescate

    IEnumerator Rescue()
    {
        state = State.Rescue;
        Snake.Frozen = true;
        movement.RestoreControls();
        StoryKit.Puppet pp = StoryKit.Puppet.Take();
        StoryKit.BeginCinematic();
        Camera cam = Camera.main;
        StoryAssets a = StoryAssets.Get;
        Vector3 cc = cageBounds.center;
        Vector3 floorC = new Vector3(cc.x, cageBounds.min.y, cc.z);

        // La amenaza se retira.
        StartCoroutine(HandRetreat());
        foreach (Snake s in snakes) if (s) StartCoroutine(SinkSnake(s));

        Vector3 standAt = floorC + Vector3.left * 1.05f;
        standAt.y = player.position.y;
        yield return StoryKit.MoveCamera(cam, floorC + new Vector3(-2.6f, 1.7f, -2.3f), cc, 0.8f);
        yield return pp.WalkTo(standAt, 1.5f);
        yield return pp.TurnTo(Vector3.right);

        // Tres llaves, tres cerraduras.
        foreach (GameObject l in locks)
        {
            GameAudio.Llave();
            Vector3 p0 = l.transform.position;
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                l.transform.position = p0 + Vector3.down * (t * t * 4f);
                yield return null;
            }
            l.SetActive(false);
        }
        yield return new WaitForSeconds(0.2f);

        // La jaula se rompe: los barrotes salen volando y desaparecen.
        StoryKit.One(a.jaulaRompe, cc, 1f, 0f);
        Collider col = cageRoot.GetComponent<Collider>();
        if (col) col.enabled = false;
        yield return BreakBars(floorC);

        // El gatito baila de lado a lado.
        StoryKit.One(a.maullido, cc, 1f, 0f);
        yield return new WaitForSeconds(0.5f);
        cat.transform.SetParent(null, true);
        GameObject spot = new GameObject("Luz_Baile");
        spot.transform.position = cc + Vector3.up * 1.6f + Vector3.back * 0.6f;
        Light sl = spot.AddComponent<Light>();
        sl.type = LightType.Point; sl.color = new Color(1f, .85f, .6f); sl.range = 4f; sl.intensity = 2.2f;
        spot.transform.SetParent(cat.transform, true);
        AudioSource music = StoryKit.Loop(a.musicaBaile, null, 0.75f);
        yield return CatDance(cam, 9.6f);
        if (music) StartCoroutine(FadeOut(music, 1.2f));

        // Salen juntos por la puerta.
        yield return ExitTogether(pp, cam);
    }

    IEnumerator HandRetreat()
    {
        if (hand)
        {
            Vector3 from = hand.position;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                hand.position = Vector3.Lerp(from, handHidden + Vector3.right * 1.5f, t / 0.9f);
                SetCurl(Mathf.Lerp(-0.45f, 1f, t / 0.9f));
                yield return null;
            }
            hand.gameObject.SetActive(false);
        }
        if (handDoor) handDoor.Close(1.2f);
        float l0 = threatLight ? threatLight.intensity : 0f;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            if (threatLight) threatLight.intensity = Mathf.Lerp(l0, 0f, t / 1.2f);
            if (drone) drone.volume = Mathf.Lerp(drone.volume, 0f, t / 1.2f);
            yield return null;
        }
        if (drone) { Destroy(drone.gameObject); drone = null; }
    }

    IEnumerator SinkSnake(Snake s)
    {
        Vector3 p0 = s.transform.position;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            foreach (Transform c in s.transform) c.position += Vector3.down * Time.deltaTime * 0.4f;
            yield return null;
        }
        s.gameObject.SetActive(false);
    }

    IEnumerator BreakBars(Vector3 floorC)
    {
        var vel = new Dictionary<Transform, Vector3>();
        var spin = new Dictionary<Transform, Vector3>();
        foreach (Transform b in bars)
        {
            if (!b) continue;
            Vector3 outDir = StoryKit.Flat(b.position - floorC);
            if (outDir.sqrMagnitude < 0.01f) outDir = Random.insideUnitSphere;
            outDir = StoryKit.Flat(outDir).normalized;
            vel[b] = outDir * Random.Range(2.5f, 4.5f) + Vector3.up * Random.Range(2.5f, 5f);
            spin[b] = Random.insideUnitSphere * 540f;
        }
        Vector3[] baseScale = bars.Select(b => b ? b.localScale : Vector3.one).ToArray();
        for (float t = 0f; t < 1.3f; t += Time.deltaTime)
        {
            for (int i = 0; i < bars.Count; i++)
            {
                Transform b = bars[i];
                if (!b) continue;
                vel[b] += Vector3.down * 12f * Time.deltaTime;
                b.position += vel[b] * Time.deltaTime;
                b.Rotate(spin[b] * Time.deltaTime, Space.World);
                b.localScale = baseScale[i] * Mathf.Clamp01(1.3f - t) / 1.3f * 1.3f;
            }
            yield return null;
        }
        foreach (Transform b in bars) if (b) b.gameObject.SetActive(false);
    }

    IEnumerator CatDance(Camera cam, float seconds)
    {
        // Ritmo de la música (125 bpm): balanceo de lado a lado, saltito en cada
        // tiempo y una vuelta completa cada cuatro compases.
        const float beat = 60f / 125f;
        Transform t = cat.transform;
        Vector3 basePos = t.position;
        Quaternion baseRot = Quaternion.LookRotation(Vector3.left);
        Vector3 side = Vector3.forward;
        Vector3 center = basePos + Vector3.up * 0.25f;
        for (float time = 0f; time < seconds; time += Time.deltaTime)
        {
            float b = time / beat;
            float sway = Mathf.Sin(b * Mathf.PI);                    // un lado por tiempo
            float hop = Mathf.Abs(Mathf.Sin(b * Mathf.PI)) * 0.07f;
            float bar = b / 4f;
            float turn = (Mathf.Repeat(bar, 4f) > 3f) ? (Mathf.Repeat(bar, 1f) * 360f) : 0f;
            t.position = basePos + side * sway * 0.14f + Vector3.up * hop;
            t.rotation = baseRot * Quaternion.Euler(0f, turn + sway * 18f, -sway * 16f);

            // La cámara gira despacio alrededor del gatito.
            float ang = 115f + time * 8f;
            Vector3 camPos = center + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * 2.1f + Vector3.up * 0.75f;
            cam.transform.SetPositionAndRotation(Vector3.Lerp(cam.transform.position, camPos, 4f * Time.deltaTime),
                Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(center - cam.transform.position), 5f * Time.deltaTime));
            yield return null;
        }
        t.position = basePos;
        t.rotation = baseRot;
    }

    IEnumerator FadeOut(AudioSource s, float seconds)
    {
        float v0 = s.volume;
        for (float t = 0f; t < seconds && s; t += Time.deltaTime)
        {
            s.volume = Mathf.Lerp(v0, 0f, t / seconds);
            yield return null;
        }
        if (s) Destroy(s.gameObject);
    }

    IEnumerator ExitTogether(StoryKit.Puppet pp, Camera cam)
    {
        if (!exitDoor) yield break;
        Vector3 doorC = exitDoor.Center;
        Vector3 floorDoor = new Vector3(doorC.x, player.position.y, doorC.z);
        Vector3 front = floorDoor - Vector3.forward * 1.6f;

        yield return StoryKit.MoveCamera(cam, floorDoor + new Vector3(2.6f, 2.0f, -5.6f), floorDoor + Vector3.up * 0.9f, 0.9f);
        CatFollower follower = cat.AddComponent<CatFollower>();
        follower.leader = player;
        Coroutine walk = StartCoroutine(pp.WalkTo(front, 1.45f));
        yield return walk;
        yield return pp.TurnTo(Vector3.forward);
        exitDoor.Open();
        yield return new WaitForSeconds(exitDoor.openSeconds * 0.8f);
        StartCoroutine(pp.WalkTo(floorDoor + Vector3.forward * 2.8f, 1.45f));
        yield return new WaitForSeconds(2.4f);

        // Fundido y epílogo.
        Epilogue ep = gameObject.AddComponent<Epilogue>();
        ep.Play(pp, cat);
    }

    // ------------------------------------------------------------ HUD

    void OnGUI()
    {
        if (GameState.HudHidden || state == State.Rescue || state == State.Defeat) return;
        float scale = Mathf.Clamp(Screen.width / 1280f, .65f, 2f);

        // Temporizador
        int secs = Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
        string clock = (secs / 60).ToString("00") + ":" + (secs % 60).ToString("00");
        GUIStyle big = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(44f * scale),
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };
        Color col = timeLeft > 30f ? new Color(1f, .9f, .7f) : timeLeft > 10f ? new Color(1f, .55f, .25f) : new Color(1f, .2f, .15f);
        if (timeLeft <= 10f && state == State.Running) col.a = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
        Rect r = new Rect(Screen.width / 2f - 150f * scale, 12f * scale, 300f * scale, 60f * scale);
        GUI.color = new Color(0f, 0f, 0f, .55f);
        GUI.DrawTexture(new Rect(r.x + 40f * scale, r.y + 4f * scale, r.width - 80f * scale, r.height - 6f * scale), Texture2D.whiteTexture);
        GUI.color = Color.white;
        big.normal.textColor = new Color(0f, 0f, 0f, .8f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), clock, big);
        big.normal.textColor = col;
        GUI.Label(r, clock, big);

        // Llaves
        GUIStyle box = new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(20f * scale), alignment = TextAnchor.MiddleCenter };
        box.normal.textColor = new Color(1f, .85f, .45f);
        GUI.Box(new Rect(Screen.width / 2f - 95f * scale, 76f * scale, 190f * scale, 36f * scale), "Llaves  " + keyCount + "/3", box);

        // Controles invertidos (mordida)
        if (movement && movement.ControlsInverted)
        {
            float k = 0.16f + 0.06f * Mathf.Sin(Time.time * 8f);
            GUI.color = new Color(.45f, .9f, .2f, k);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUIStyle warn = new GUIStyle(box) { fontSize = Mathf.RoundToInt(22f * scale) };
            warn.normal.textColor = new Color(.7f, 1f, .4f);
            GUI.Box(new Rect(Screen.width / 2f - 230f * scale, 120f * scale, 460f * scale, 42f * scale),
                "¡Mordida! Controles invertidos  " + movement.InvertedRemaining.ToString("0.0") + " s", warn);
        }

        // Rojo en los bordes cuando queda poco tiempo
        if (state == State.Running && timeLeft < 25f)
        {
            float k = Mathf.InverseLerp(25f, 0f, timeLeft) * (0.18f + 0.08f * Mathf.Sin(Time.time * 7f));
            GUI.color = new Color(.8f, 0f, 0f, k);
            float w = 60f * scale;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, w), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - w, Screen.width, w), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, w, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width - w, 0, w, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        if (!string.IsNullOrEmpty(prompt))
            GUI.Box(new Rect(Screen.width / 2f - 280f * scale, Screen.height - 100f * scale, 560f * scale, 45f * scale), prompt, box);
        if (Time.time < messageUntil)
            GUI.Box(new Rect(Screen.width / 2f - 300f * scale, 170f * scale, 600f * scale, 44f * scale), message, box);
    }
}

/// <summary>El gatito sigue al protagonista dando saltitos alegres.</summary>
public class CatFollower : MonoBehaviour
{
    public Transform leader;
    public float distance = 0.9f;
    float hopPhase;
    float baseY;

    void Start() { baseY = transform.position.y; }

    void Update()
    {
        if (!leader) return;
        Vector3 to = StoryKit.Flat(leader.position - transform.position);
        float d = to.magnitude;
        Vector3 pos = transform.position;
        if (d > distance)
        {
            Vector3 step = to.normalized * Mathf.Min(d - distance, 2.2f * Time.deltaTime);
            pos += step;
            hopPhase += Time.deltaTime * 11f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), 10f * Time.deltaTime);
        }
        else hopPhase = Mathf.MoveTowards(hopPhase, Mathf.Ceil(hopPhase / Mathf.PI) * Mathf.PI, Time.deltaTime * 8f);
        pos.y = baseY + Mathf.Abs(Mathf.Sin(hopPhase)) * 0.12f;
        transform.position = pos;
    }
}
