using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utilidades compartidas por la historia: medir y colocar prefabs, puertas
/// estilo Nivel 2, calaveras y señales de trampa, el gatito, sonidos en bucle
/// y el control del personaje durante las escenas (caminar solo, cámara).
/// </summary>
public static class StoryKit
{
    // ------------------------------------------------------------ medidas

    public static Bounds Measure(GameObject g)
    {
        Renderer[] rs = g.GetComponentsInChildren<Renderer>(true);
        Bounds b = new Bounds(g.transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer r in rs)
        {
            if (r is ParticleSystemRenderer) continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    public static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    /// Altura del suelo bajo un punto (ignora al jugador y los triggers).
    public static float GroundY(Vector3 p, float fallback, float above = 1.5f, float depth = 12f)
    {
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * above, Vector3.down, above + depth,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<GuardPlayerMovement>() != null) continue;
            if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
            if (h.point.y > best) best = h.point.y;
        }
        return float.IsNegativeInfinity(best) ? fallback : best;
    }

    static Transform decorRoot;

    public static Transform DecorRoot
    {
        get
        {
            if (!decorRoot)
            {
                decorRoot = new GameObject("Historia_Decoracion").transform;
            }
            return decorRoot;
        }
    }

    /// Instancia un prefab con su base centrada en "feet" y la altura pedida.
    public static GameObject Place(GameObject prefab, Vector3 feet, float height, float yaw,
        Transform parent = null, bool keepColliders = false)
    {
        if (!prefab) return null;
        GameObject g = Object.Instantiate(prefab, parent ? parent : DecorRoot);
        g.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation;
        Bounds b = Measure(g);
        if (height > 0f && b.size.y > 0.0001f)
        {
            g.transform.localScale *= height / b.size.y;
            b = Measure(g);
        }
        g.transform.position += feet - new Vector3(b.center.x, b.min.y, b.center.z);
        if (!keepColliders)
            foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        return g;
    }

    public static void RemoveColliders(GameObject g)
    {
        foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
    }

    public static void AddBoxColliders(GameObject g)
    {
        foreach (MeshFilter mf in g.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.sharedMesh || mf.GetComponent<Collider>()) continue;
            BoxCollider box = mf.gameObject.AddComponent<BoxCollider>();
            box.center = mf.sharedMesh.bounds.center;
            box.size = mf.sharedMesh.bounds.size;
        }
    }

    // ------------------------------------------------------------ calaveras y señales

    static readonly List<GameObject> skulls = new List<GameObject>();

    /// Calavera decorativa donde murió el jugador (no bloquea el paso).
    public static void SkullAt(Vector3 at)
    {
        StoryAssets a = StoryAssets.Get;
        if (!a.skull) return;
        float y = GroundY(at + Vector3.up * 0.3f, at.y, 0.6f, 8f);
        Vector3 feet = new Vector3(at.x, y + 0.005f, at.z) + Random.insideUnitSphere.normalized * 0.12f;
        feet.y = y + 0.005f;
        GameObject s = Place(a.skull, feet, 0.27f, Random.Range(0f, 360f));
        if (!s) return;
        s.name = "Calavera_Muerte";
        foreach (Renderer r in s.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        skulls.Add(s);
    }

    /// Jarrón roto o calavera encima de una trampa escondida.
    public static GameObject Marker(Vector3 footprintCenter, int variant, float radius = 0.35f)
    {
        StoryAssets a = StoryAssets.Get;
        GameObject prefab = null;
        float h = 0.34f;
        if (variant % 3 == 2 && a.skull) { prefab = a.skull; h = 0.27f; }
        else if (a.brokenPots != null && a.brokenPots.Length > 0)
            prefab = a.brokenPots[variant % a.brokenPots.Length];
        if (!prefab) prefab = a.skull;
        float y = GroundY(footprintCenter + Vector3.up * 0.5f, footprintCenter.y, 1.5f, 3f);
        float ang = variant * 137f;
        Vector3 off = Quaternion.Euler(0f, ang, 0f) * Vector3.forward * radius;
        GameObject g = Place(prefab, new Vector3(footprintCenter.x, y + 0.004f, footprintCenter.z) + off, h, ang);
        if (g) g.name = "Senal_Trampa";
        return g;
    }

    // ------------------------------------------------------------ texto 3D

    static Font font;
    static Material textMat;

    public static TextMesh Label3D(string text, Vector3 pos, Quaternion rot, float charSize, Color color,
        Transform parent = null)
    {
        if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject go = new GameObject("Texto3D_" + text);
        go.transform.SetParent(parent ? parent : DecorRoot, false);
        go.transform.SetPositionAndRotation(pos, rot);
        TextMesh tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.text = text;
        tm.fontSize = 96;
        tm.characterSize = charSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.fontStyle = FontStyle.Bold;
        if (!textMat)
        {
            Shader sh = Shader.Find("Historia/Texto3D");
            textMat = sh ? new Material(sh) { mainTexture = font.material.mainTexture } : font.material;
        }
        go.GetComponent<MeshRenderer>().sharedMaterial = textMat;
        return tm;
    }

    // ------------------------------------------------------------ materiales simples

    static Shader litShader;

    public static Material Mat(Color c, float smooth = 0.2f, Color? emission = null)
    {
        if (!litShader) litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (!litShader) litShader = Shader.Find("Standard");
        Material m = new Material(litShader);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission.Value);
        }
        return m;
    }

    public static GameObject Box(string name, Vector3 center, Vector3 size, Material m, Transform parent,
        Quaternion? rot = null, bool collider = false)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent ? parent : DecorRoot, false);
        g.transform.SetPositionAndRotation(center, rot ?? Quaternion.identity);
        g.transform.localScale = size;
        if (m) g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.Destroy(g.GetComponent<Collider>());
        return g;
    }

    public static GameObject Prim(PrimitiveType type, string name, Vector3 center, Vector3 scale, Material m,
        Transform parent, Quaternion? rot = null)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent ? parent : DecorRoot, false);
        g.transform.SetPositionAndRotation(center, rot ?? Quaternion.identity);
        g.transform.localScale = scale;
        if (m) g.GetComponent<Renderer>().sharedMaterial = m;
        Object.Destroy(g.GetComponent<Collider>());
        return g;
    }

    // ------------------------------------------------------------ puertas estilo Nivel 2

    /// Puerta doble Doors_RoundArch con su marco (como la del Nivel 2).
    /// "bottomCenter" = centro del hueco a ras de suelo; "openTowards" = hacia dónde se abren las hojas.
    public static ArchDoor BuildNivel2Door(string name, Vector3 bottomCenter, Vector3 openTowards, float scale)
    {
        StoryAssets a = StoryAssets.Get;
        if (!a.doorLeft || !a.doorRight) return null;
        openTowards = Flat(openTowards).normalized;
        Vector3 along = Vector3.Cross(Vector3.up, openTowards).normalized;
        // Igual que en el Nivel 2: las dos hojas y los marcos comparten giro.
        float yaw = Quaternion.LookRotation(-openTowards).eulerAngles.y + 180f;
        Quaternion rot = Quaternion.Euler(-90f, yaw, 0f);
        Vector3 sideL = rot * Vector3.right;
        sideL.y = 0f; sideL.Normalize();

        GameObject group = new GameObject(name);
        group.transform.position = bottomCenter;

        GameObject Leaf(GameObject prefab, Vector3 side, Quaternion r, string n, float depthBias)
        {
            GameObject g = Object.Instantiate(prefab, group.transform);
            g.name = n;
            g.transform.rotation = r;
            g.transform.localScale = prefab.transform.localScale * scale;
            Bounds b = Measure(g);
            float w = Mathf.Abs(Vector3.Dot(b.size, new Vector3(Mathf.Abs(side.x), 0f, Mathf.Abs(side.z))));
            Vector3 want = bottomCenter + side * (w * 0.5f) + openTowards * depthBias;
            g.transform.position += new Vector3(want.x - b.center.x, bottomCenter.y + 0.02f - b.min.y, want.z - b.center.z);
            return g;
        }

        GameObject left = Leaf(a.doorLeft, sideL, rot, "Hoja_L", 0f);
        GameObject right = Leaf(a.doorRight, -sideL, rot, "Hoja_R", 0f);
        foreach (GameObject leaf in new[] { left, right })
        {
            foreach (Collider c in leaf.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            AddBoxColliders(leaf);
        }
        if (a.doorFrame)
        {
            GameObject fl = Leaf(a.doorFrame, sideL, rot, "Marco_Izq", 0f);
            GameObject fr = Leaf(a.doorFrame, -sideL, Quaternion.Euler(-90f, yaw + 180f, 0f), "Marco_Der", 0f);
            foreach (GameObject f in new[] { fl, fr })
                foreach (MeshFilter mf in f.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh && !mf.GetComponent<Collider>())
                        mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }

        ArchDoor door = group.AddComponent<ArchDoor>();
        door.leftLeaf = left.transform;
        door.rightLeaf = right.transform;
        door.openTowards = openTowards;
        door.openSeconds = 1.4f;
        return door;
    }

    // ------------------------------------------------------------ gatito

    /// Gatito (maxwell) con su pivote en las patas, mirando a "facing".
    public static GameObject MakeCat(Vector3 feet, Vector3 facing, float height, Transform parent = null)
    {
        LevelVisualAssets assets = LevelVisualAssets.Load();
        GameObject root = new GameObject("Gatito");
        root.transform.SetParent(parent ? parent : DecorRoot, false);
        root.transform.position = feet;
        if (!assets || !assets.catModel)
        {
            // Sin modelo: un gatito simple de primitivas.
            Material fur = Mat(new Color(.12f, .11f, .1f));
            Prim(PrimitiveType.Capsule, "Cuerpo", feet + Vector3.up * .16f, new Vector3(.22f, .2f, .22f), fur, root.transform, Quaternion.Euler(90, 0, 0));
            Prim(PrimitiveType.Sphere, "Cabeza", feet + new Vector3(0, .3f, .2f), Vector3.one * .18f, fur, root.transform);
        }
        else
        {
            GameObject cat = Object.Instantiate(assets.catModel, root.transform);
            cat.name = "maxwell";
            foreach (Collider c in cat.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (Renderer r in cat.GetComponentsInChildren<Renderer>(true))
            {
                bool whiskers = r.name.IndexOf("whisk", System.StringComparison.OrdinalIgnoreCase) >= 0;
                Material m = whiskers ? assets.catWhiskers : assets.catBody;
                if (m) r.sharedMaterial = m;
            }
            Bounds b = Measure(cat);
            if (b.size.y > 0.0001f) cat.transform.localScale *= height / b.size.y;
            b = Measure(cat);
            cat.transform.position += feet - new Vector3(b.center.x, b.min.y, b.center.z);
        }
        Vector3 f = Flat(facing);
        if (f.sqrMagnitude > 0.001f) root.transform.rotation = Quaternion.LookRotation(f.normalized);
        return root;
    }

    // ------------------------------------------------------------ sonido

    public static AudioSource Loop(AudioClip clip, Vector3? at, float volume, float maxDistance = 25f, Transform parent = null)
    {
        if (!clip) return null;
        GameObject go = new GameObject("Bucle_" + clip.name);
        if (parent) go.transform.SetParent(parent, false);
        if (at.HasValue) go.transform.position = at.Value;
        AudioSource s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.volume = volume;
        s.playOnAwake = false;
        s.dopplerLevel = 0f;
        if (at.HasValue)
        {
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 1.5f;
            s.maxDistance = maxDistance;
        }
        s.Play();
        return s;
    }

    public static void One(AudioClip clip, Vector3? at = null, float volume = 1f, float jitter = 0.04f, float maxDistance = 22f)
    {
        if (clip) GameAudio.Play(clip, at, volume, jitter, maxDistance);
    }

    // ------------------------------------------------------------ control del personaje en escenas

    public class Puppet
    {
        public Transform player;
        public GuardPlayerMovement movement;
        public GuardWalkAnimation walkAnim;
        public CharacterController controller;
        public Animator animator;
        bool walking;
        static readonly int SceneWalk = Animator.StringToHash("Caminar_Escena");
        static readonly int IdleState = Animator.StringToHash("Idle");

        public static Puppet Take()
        {
            GameObject p = GameObject.Find("Player");
            if (!p) return null;
            Puppet pp = new Puppet
            {
                player = p.transform,
                movement = p.GetComponent<GuardPlayerMovement>(),
                walkAnim = p.GetComponent<GuardWalkAnimation>(),
                controller = p.GetComponent<CharacterController>(),
                animator = p.GetComponentInChildren<Animator>(),
            };
            if (pp.movement) pp.movement.enabled = false;
            if (pp.walkAnim) pp.walkAnim.enabled = false;
            if (pp.controller) pp.controller.enabled = false;
            if (pp.animator) pp.animator.speed = 1f;
            return pp;
        }

        public void Walk(bool on)
        {
            if (!animator || walking == on) return;
            walking = on;
            if (animator.HasState(0, SceneWalk))
                animator.CrossFade(on ? SceneWalk : IdleState, 0.2f);
            else
                animator.SetFloat("Speed", on ? 0.5f : 0f);
        }

        public IEnumerator WalkTo(Vector3 target, float speed, bool keepHeight = true)
        {
            Walk(true);
            Vector3 start = player.position;
            Vector3 flat = Flat(target - start);
            if (flat.sqrMagnitude > 0.0004f)
            {
                Quaternion want = Quaternion.LookRotation(flat.normalized);
                float dist = flat.magnitude;
                for (float d = 0f; d < dist; d += speed * Time.deltaTime)
                {
                    Vector3 p = Vector3.MoveTowards(Flat(player.position), Flat(target), speed * Time.deltaTime);
                    float y = keepHeight ? start.y : Mathf.Lerp(start.y, target.y, d / dist);
                    player.position = new Vector3(p.x, y, p.z);
                    player.rotation = Quaternion.RotateTowards(player.rotation, want, 360f * Time.deltaTime);
                    yield return null;
                }
            }
            player.position = new Vector3(target.x, keepHeight ? start.y : target.y, target.z);
            Walk(false);
        }

        public IEnumerator TurnTo(Vector3 dir, float seconds = 0.35f)
        {
            dir = Flat(dir);
            if (dir.sqrMagnitude < 0.0001f) yield break;
            Quaternion from = player.rotation, to = Quaternion.LookRotation(dir.normalized);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                player.rotation = Quaternion.Slerp(from, to, t / seconds);
                yield return null;
            }
            player.rotation = to;
        }

        public void Teleport(Vector3 pos, Quaternion rot)
        {
            player.SetPositionAndRotation(pos, rot);
            Physics.SyncTransforms();
        }

        public void Release(bool giveControl)
        {
            Walk(false);
            if (controller) controller.enabled = true;
            Physics.SyncTransforms();
            if (walkAnim) walkAnim.enabled = true;
            if (movement) movement.enabled = giveControl;
            if (animator && animator.HasState(0, IdleState)) animator.CrossFade(IdleState, 0.15f);
        }
    }

    public static IEnumerator MoveCamera(Camera cam, Vector3 pos, Vector3 lookAt, float seconds)
    {
        if (!cam) yield break;
        Vector3 fromP = cam.transform.position;
        Quaternion fromR = cam.transform.rotation;
        Quaternion toR = Quaternion.LookRotation(lookAt - pos);
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            cam.transform.SetPositionAndRotation(Vector3.Lerp(fromP, pos, k), Quaternion.Slerp(fromR, toR, k));
            yield return null;
        }
        cam.transform.SetPositionAndRotation(pos, toR);
    }

    public static void BeginCinematic()
    {
        GameState.CinematicCamera = true;
        GameState.HudHidden = true;
    }

    public static void EndCinematic()
    {
        GameState.CinematicCamera = false;
        GameState.HudHidden = false;
        CameraIntroFollow follow = Camera.main ? Camera.main.GetComponent<CameraIntroFollow>() : null;
        if (follow) follow.SnapBehindPlayer();
    }

    // ------------------------------------------------------------ entrar / salir por una puerta

    /// Escena de entrada: la puerta se abre, el personaje entra y se cierra detrás.
    public static IEnumerator EnterThroughDoor(ArchDoor door, Vector3 into, Vector3 standPoint)
    {
        Puppet pp = Puppet.Take();
        Camera cam = Camera.main;
        if (pp == null || !door || !cam) { pp?.Release(true); yield break; }
        BeginCinematic();
        into = Flat(into).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, into);
        Vector3 doorC = door.Center;
        Vector3 outside = new Vector3(doorC.x, standPoint.y, doorC.z) - into * 1.6f;
        pp.Teleport(outside, Quaternion.LookRotation(into));

        Vector3 camPos = standPoint + into * 3.4f + side * 1.6f + Vector3.up * 1.7f;
        cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(doorC + Vector3.up * 0.1f - camPos));
        yield return new WaitForSeconds(0.5f);
        door.Open();
        yield return new WaitForSeconds(door.openSeconds * 0.6f);
        Coroutine walk = door.StartCoroutine(pp.WalkTo(standPoint, 1.45f));
        // La cámara retrocede despacio mientras entra.
        yield return MoveCamera(cam, standPoint + into * 4.2f + side * 1.1f + Vector3.up * 2.0f,
            standPoint + Vector3.up * 1.1f, Vector3.Distance(outside, standPoint) / 1.45f);
        yield return walk;
        door.Close();
        StoryKit.One(StoryAssets.Get.golpePuerta, doorC, 0.8f);
        // Detrás del personaje, mirando hacia dentro.
        yield return pp.TurnTo(into, 0.2f);
        Vector3 behind = standPoint - into * 2.5f + Vector3.up * 1.8f;
        yield return MoveCamera(cam, behind, standPoint + into * 2f + Vector3.up * 1.1f, 0.7f);
        pp.Release(true);
        EndCinematic();
    }

    /// Escena de salida: (abre la puerta) el personaje la cruza y se pasa al siguiente nivel.
    public static IEnumerator ExitThroughDoor(ArchDoor door, Vector3 into, System.Action onFinish = null,
        float walkBeyond = 2.6f)
    {
        Puppet pp = Puppet.Take();
        Camera cam = Camera.main;
        if (pp == null || !door || !cam) { GameFlow.LoadNextScene(); yield break; }
        BeginCinematic();
        into = Flat(into).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, into);
        Vector3 doorC = door.Center;
        Vector3 floorC = new Vector3(doorC.x, pp.player.position.y, doorC.z);
        Vector3 front = floorC - into * 1.3f;

        yield return MoveCamera(cam, floorC - into * 4.6f + side * 2.0f + Vector3.up * 2.1f,
            floorC + Vector3.up * 1.1f, 0.8f);
        yield return pp.WalkTo(front, 1.45f);
        yield return pp.TurnTo(into);
        if (!door.IsOpen)
        {
            door.Open();
            yield return new WaitForSeconds(door.openSeconds);
        }
        Coroutine walk = door.StartCoroutine(pp.WalkTo(floorC + into * walkBeyond, 1.45f));
        yield return new WaitForSeconds(1.1f);
        if (onFinish != null) onFinish();
        else GameFlow.LoadNextScene();
        yield return walk;
    }
}
