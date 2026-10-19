using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Epílogo · De vuelta a casa.
/// Fundido a negro, la cabaña en el campo al amanecer (colores cálidos,
/// sonidos de naturaleza y música tranquila), el protagonista se sienta,
/// el gatito se frota contra él y se acomoda a su lado; la cámara se aleja
/// con el texto final, un maullido suave, "Fin…" y la pantalla de victoria.
/// Se construye por código lejos de la mazmorra, dentro del Nivel 6.
/// </summary>
public class Epilogue : MonoBehaviour
{
    static readonly Vector3 O = new Vector3(0f, 0f, 700f);

    StoryKit.Puppet pp;
    GameObject cat;
    Transform root;
    readonly List<AudioSource> loops = new List<AudioSource>();

    public void Play(StoryKit.Puppet puppet, GameObject catObject)
    {
        pp = puppet;
        cat = catObject;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        StoryNarrator n = StoryNarrator.Instance;
        StoryAssets a = StoryAssets.Get;
        if (n) yield return n.FadeToBlack(1.6f);
        GameFlow.HideDeathPanel();

        // Silencio de la mazmorra.
        foreach (AudioSource s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (s.clip && (s.clip.name.Contains("ambiente_mazmorra") || s.clip.name.Contains("antorcha") || s.clip.name.Contains("musica_baile")))
                s.Stop();

        BuildMeadow();
        cat.transform.SetParent(root, true);
        HideDungeon();

        // Llegan juntos por el camino.
        Camera cam = Camera.main;
        Destroy(cat.GetComponent<CatFollower>());
        Vector3 seat = O + new Vector3(0f, 0f, 3.1f);
        Vector3 start = O + new Vector3(-1.6f, 0f, -7.5f);
        pp.Teleport(start, Quaternion.LookRotation(seat - start));
        cat.transform.position = start - (seat - start).normalized * 1.0f;
        CatFollower f = cat.AddComponent<CatFollower>();
        f.leader = pp.player;
        f.distance = 0.85f;

        cam.transform.SetPositionAndRotation(O + new Vector3(3.2f, 1.5f, -10.5f), Quaternion.LookRotation(start - (O + new Vector3(3.2f, 1.5f, -10.5f)) + Vector3.up * 0.5f));
        loops.Add(StoryKit.Loop(a.ambienteCampo, null, 0f));
        loops.Add(StoryKit.Loop(a.musicaTranquila, null, 0f));
        StartCoroutine(FadeLoops(0.55f, 0.45f, 3f));
        if (n) StartCoroutine(n.FadeFromBlack(2.2f));

        Vector3 before = seat + Vector3.back * 0.6f;
        StartCoroutine(FollowCam(cam, 7.5f));
        yield return pp.WalkTo(before, 1.4f);
        yield return pp.TurnTo(Vector3.back, 0.6f);

        // Se sienta frente a la cabaña.
        yield return Sit(seat);

        // El gatito se acerca, se frota y se acomoda a su lado.
        Destroy(cat.GetComponent<CatFollower>());
        yield return CatComesAndRubs(seat);

        // La cámara se aleja despacio mientras aparece el texto.
        StartCoroutine(PullBack(cam, seat, 16f));
        yield return new WaitForSeconds(1.5f);
        if (n) yield return n.Caption(StoryText.Epilogue, 38, new Color(1f, .96f, .88f), 2.2f, 8.5f, 1.8f);
        yield return new WaitForSeconds(0.6f);
        StoryKit.One(a.maullidoSuave, null, 0.9f, 0f);
        yield return new WaitForSeconds(0.9f);
        if (n) yield return n.Caption(StoryText.TheEnd, 54, new Color(1f, .8f, .4f), 1.6f, 3.5f, 0f, false);
        yield return new WaitForSeconds(0.5f);
        if (n)
        {
            n.HideCaption();
            n.ShowVictory();
        }
    }

    IEnumerator FadeLoops(float ambient, float music, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (loops[0]) loops[0].volume = Mathf.Lerp(0f, ambient, t / seconds);
            if (loops[1]) loops[1].volume = Mathf.Lerp(0f, music, t / seconds);
            yield return null;
        }
    }

    IEnumerator FollowCam(Camera cam, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            Vector3 target = pp.player.position + Vector3.up * 1.0f;
            Vector3 want = target + new Vector3(2.4f, 0.6f, -3.6f);
            cam.transform.position = Vector3.Lerp(cam.transform.position, want, 1.5f * Time.deltaTime);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(target - cam.transform.position), 3f * Time.deltaTime);
            yield return null;
        }
    }

    IEnumerator PullBack(Camera cam, Vector3 seat, float seconds)
    {
        Vector3 look = seat + Vector3.up * 0.7f;
        Vector3 from = seat + new Vector3(1.0f, 1.2f, -2.4f);
        Vector3 to = seat + new Vector3(3.5f, 4.8f, -15f);
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            Vector3 p = Vector3.Lerp(from, to, k);
            cam.transform.position = Vector3.Lerp(cam.transform.position, p, 2f * Time.deltaTime);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation,
                Quaternion.LookRotation(look + Vector3.up * k * 1.2f - cam.transform.position), 2.5f * Time.deltaTime);
            yield return null;
        }
    }

    // ------------------------------------------------------------ sentarse

    IEnumerator Sit(Vector3 seat)
    {
        Animator anim = pp.animator;
        if (!anim) yield break;
        pp.Walk(false);
        yield return new WaitForSeconds(0.45f);
        anim.enabled = false;

        Transform P = pp.player;
        Vector3 right = P.right;
        HumanBodyBones[] bones =
        {
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.Spine, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
        };
        var from = new Dictionary<Transform, Quaternion>();
        foreach (HumanBodyBones b in bones)
        {
            Transform t = anim.GetBoneTransform(b);
            if (t) from[t] = t.localRotation;
        }

        // Pose final calculada sobre la pose actual (de pie).
        Rotate(anim, HumanBodyBones.LeftUpperLeg, -88f, right);
        Rotate(anim, HumanBodyBones.RightUpperLeg, -88f, right);
        Rotate(anim, HumanBodyBones.LeftLowerLeg, 88f, right);
        Rotate(anim, HumanBodyBones.RightLowerLeg, 88f, right);
        Rotate(anim, HumanBodyBones.Spine, 10f, right);
        Rotate(anim, HumanBodyBones.LeftUpperArm, -12f, right);
        Rotate(anim, HumanBodyBones.RightUpperArm, -12f, right);
        Rotate(anim, HumanBodyBones.LeftLowerArm, -55f, right);
        Rotate(anim, HumanBodyBones.RightLowerArm, -55f, right);
        var to = new Dictionary<Transform, Quaternion>();
        foreach (Transform t in from.Keys) to[t] = t.localRotation;
        foreach (var kv in from) kv.Key.localRotation = kv.Value;

        // Altura: la cadera sobre el tronco.
        Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        float hipsStand = hips ? hips.position.y - P.position.y : 0.95f;
        float seatTop = seat.y + 0.46f;
        Vector3 p0 = P.position;
        Vector3 p1 = new Vector3(seat.x, seatTop - (hipsStand * 0.5f) - 0.02f, seat.z);
        // (las piernas dobladas acortan la altura de la cadera a ~la mitad)
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.2f);
            foreach (Transform b in from.Keys) b.localRotation = Quaternion.Slerp(from[b], to[b], k);
            P.position = Vector3.Lerp(p0, p1, k);
            yield return null;
        }
        foreach (Transform b in from.Keys) b.localRotation = to[b];
        // Ajuste fino: la cadera justo encima del asiento.
        if (hips)
        {
            float dy = seatTop + 0.08f - hips.position.y;
            P.position += Vector3.up * dy;
        }
    }

    static void Rotate(Animator anim, HumanBodyBones b, float deg, Vector3 axis)
    {
        Transform t = anim.GetBoneTransform(b);
        if (t) t.rotation = Quaternion.AngleAxis(deg, axis) * t.rotation;
    }

    IEnumerator CatComesAndRubs(Vector3 seat)
    {
        StoryAssets a = StoryAssets.Get;
        Transform c = cat.transform;
        float baseY = O.y;
        Vector3 side = seat + new Vector3(0.55f, 0f, -0.25f);
        side.y = baseY;
        // Camina hasta su lado.
        Vector3 from = c.position;
        float dist = StoryKit.Flat(side - from).magnitude;
        Quaternion look = Quaternion.LookRotation(StoryKit.Flat(side - from).sqrMagnitude > 0.001f ? StoryKit.Flat(side - from) : Vector3.back);
        for (float d = 0f; d < dist; d += 1.1f * Time.deltaTime)
        {
            Vector3 p = Vector3.Lerp(from, side, d / dist);
            p.y = baseY + Mathf.Abs(Mathf.Sin(d * 9f)) * 0.06f;
            c.position = p;
            c.rotation = Quaternion.Slerp(c.rotation, look, 8f * Time.deltaTime);
            yield return null;
        }
        // Se frota contra la pierna (de lado a lado) ronroneando.
        AudioSource purr = StoryKit.Loop(a.ronroneo, side + Vector3.up * 0.3f, 0.9f, 12f, root);
        StoryKit.One(a.maullidoSuave, side, 0.7f, 0f);
        Vector3 along = Vector3.right;
        Quaternion rub = Quaternion.LookRotation(Vector3.left);
        for (float t = 0f; t < 3.2f; t += Time.deltaTime)
        {
            float s = Mathf.Sin(t * 2.6f);
            c.position = side + along * s * 0.12f + Vector3.up * 0.0f;
            c.rotation = Quaternion.Slerp(c.rotation, rub * Quaternion.Euler(0f, s * 20f, s * 10f), 6f * Time.deltaTime);
            yield return null;
        }
        // Se acomoda a su lado (se tumba hecho una bola, mirando a la cámara).
        Vector3 lieAt = seat + new Vector3(0.5f, 0.46f, 0.05f);
        Vector3 p0 = c.position;
        Quaternion r0 = c.rotation;
        Quaternion r1 = Quaternion.LookRotation(Vector3.back) * Quaternion.Euler(0f, -25f, 0f);
        Vector3 s0 = c.localScale;
        Vector3 s1 = new Vector3(s0.x * 1.08f, s0.y * 0.72f, s0.z * 1.05f);
        for (float t = 0f; t < 1.0f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            Vector3 p = Vector3.Lerp(p0, lieAt, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.25f;
            c.SetPositionAndRotation(p, Quaternion.Slerp(r0, r1, k));
            c.localScale = Vector3.Lerp(s0, s1, k);
            yield return null;
        }
        // Respira despacio.
        cat.AddComponent<CatBreath>();
    }

    // ------------------------------------------------------------ escena

    void HideDungeon()
    {
        UnityEngine.SceneManagement.Scene scene = gameObject.scene;
        Transform playerRoot = pp.player.root;
        Transform camRoot = Camera.main ? Camera.main.transform.root : null;
        foreach (GameObject g in scene.GetRootGameObjects())
        {
            Transform t = g.transform;
            if (t == root || t == playerRoot || t == camRoot || t == transform.root) continue;
            if (g.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) ||
                g.GetComponentInChildren<GameFlow>(true) || g.GetComponentInChildren<StoryNarrator>(true) ||
                g.GetComponentInChildren<GameAudio>(true) || g.GetComponentInChildren<GuardDoorAndKey>(true)) continue;
            g.SetActive(false);
        }
    }

    void BuildMeadow()
    {
        StoryAssets a = StoryAssets.Get;
        root = new GameObject("Epilogo_Cabana").transform;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root.gameObject, gameObject.scene);
        root.position = O;

        // Amanecer: cielo, niebla y luz cálidos.
        // Cielo de amanecer: color cálido liso que se funde con la niebla, y un sol bajo.
        Camera cam = Camera.main;
        Color dawn = new Color(1f, .74f, .55f);
        if (cam)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = dawn;
        }
        Material sunMat = StoryKit.Mat(new Color(1f, .85f, .55f), 0f, new Color(1f, .7f, .35f) * 3f);
        GameObject sunDisc = StoryKit.Prim(PrimitiveType.Sphere, "Sol", O + new Vector3(70f, 9f, 110f), Vector3.one * 14f, sunMat, root);
        sunDisc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (cam) cam.farClipPlane = Mathf.Max(cam.farClipPlane, 400f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.95f, .72f, .55f);
        RenderSettings.ambientEquatorColor = new Color(.72f, .55f, .45f);
        RenderSettings.ambientGroundColor = new Color(.28f, .24f, .18f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = dawn;
        RenderSettings.fogDensity = 0.011f;

        GameObject sun = new GameObject("Sol_Amanecer");
        sun.transform.SetParent(root, false);
        // Sol bajo al este (a la derecha de la cámara): cielo naranja en el horizonte.
        sun.transform.rotation = Quaternion.LookRotation(-(new Vector3(70f, 9f, 110f)).normalized);
        Light l = sun.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = new Color(1f, .74f, .48f);
        l.intensity = 1.5f;
        l.shadows = LightShadows.Soft;
        RenderSettings.sun = l;

        Material grass = StoryKit.Mat(new Color(.36f, .52f, .22f), 0.1f);
        Material dirt = StoryKit.Mat(new Color(.45f, .34f, .22f), 0.05f);
        Material stone = StoryKit.Mat(new Color(.45f, .43f, .4f), 0.1f);
        Material wood = a.wood ? a.wood : StoryKit.Mat(new Color(.55f, .38f, .22f));
        Material darkWood = a.darkWood ? a.darkWood : StoryKit.Mat(new Color(.3f, .2f, .12f));
        Material glow = StoryKit.Mat(new Color(1f, .82f, .45f), 0.3f, new Color(1f, .65f, .25f) * 2f);
        Material roof = StoryKit.Mat(new Color(.42f, .2f, .14f), 0.2f);

        GameObject ground = StoryKit.Prim(PrimitiveType.Plane, "Prado", O, new Vector3(30f, 1f, 30f), grass, root);
        ground.AddComponent<MeshCollider>();
        StoryKit.Box("Camino", O + new Vector3(-0.8f, 0.01f, -10f), new Vector3(1.6f, 0.02f, 24f), dirt, root, Quaternion.Euler(0f, 4f, 0f));

        // Cabaña (cumbrera de delante a atrás: el hastial mira al camino).
        Vector3 h = O + new Vector3(0f, 0f, 7.2f);
        const float W = 5.2f, D = 4.4f, Hgt = 2.7f;
        StoryKit.Box("Cimiento", h + Vector3.up * 0.15f, new Vector3(W + 0.3f, 0.3f, D + 0.3f), stone, root);
        StoryKit.Box("Paredes", h + Vector3.up * (0.3f + Hgt * 0.5f), new Vector3(W, Hgt, D), wood, root);
        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                StoryKit.Box("Esquina", h + new Vector3(sx * W * 0.5f, 0.3f + Hgt * 0.5f, sz * D * 0.5f), new Vector3(0.22f, Hgt, 0.22f), darkWood, root);
        float topY = 0.3f + Hgt;
        float half = W * 0.5f;
        // Hastiales (rombos que se esconden en la pared por abajo).
        foreach (int sz in new[] { -1, 1 })
            StoryKit.Box("Hastial", h + new Vector3(0f, topY, sz * (D * 0.5f - 0.02f)), new Vector3(half * 1.414f, half * 1.414f, 0.12f),
                wood, root, Quaternion.Euler(0f, 0f, 45f));
        // Tejado a dos aguas.
        float slope = 40f;
        float len = half / Mathf.Cos(slope * Mathf.Deg2Rad) + 0.45f;
        float rise = Mathf.Tan(slope * Mathf.Deg2Rad) * half;
        foreach (int sx in new[] { -1, 1 })
        {
            Vector3 mid = h + new Vector3(sx * half * 0.5f, topY + rise * 0.5f + 0.12f, 0f);
            StoryKit.Box("Tejado", mid, new Vector3(len, 0.16f, D + 0.7f), roof, root, Quaternion.Euler(0f, 0f, -sx * slope));
        }
        StoryKit.Box("Chimenea", h + new Vector3(1.4f, topY + rise + 0.3f, 1.0f), new Vector3(0.6f, 1.6f, 0.6f), stone, root);
        // Puerta, ventanas y porche.
        Vector3 front = h + Vector3.back * (D * 0.5f + 0.02f);
        StoryKit.Box("Puerta", front + new Vector3(0f, 0.3f + 1.0f, 0f), new Vector3(1.0f, 2.0f, 0.08f), darkWood, root);
        StoryKit.Box("Pomo", front + new Vector3(0.35f, 1.3f, -0.07f), new Vector3(0.08f, 0.08f, 0.06f), glow, root);
        foreach (int sx in new[] { -1, 1 })
        {
            StoryKit.Box("Marco", front + new Vector3(sx * 1.6f, 1.7f, -0.01f), new Vector3(0.9f, 0.8f, 0.06f), darkWood, root);
            StoryKit.Box("Ventana", front + new Vector3(sx * 1.6f, 1.7f, -0.04f), new Vector3(0.7f, 0.6f, 0.04f), glow, root);
        }
        StoryKit.Box("Porche", front + new Vector3(0f, 0.1f, -0.6f), new Vector3(2.6f, 0.2f, 1.2f), darkWood, root);
        GameObject warm = new GameObject("Luz_Hogar");
        warm.transform.SetParent(root, false);
        warm.transform.position = front + new Vector3(0f, 1.8f, -0.8f);
        Light wl = warm.AddComponent<Light>();
        wl.type = LightType.Point; wl.color = new Color(1f, .7f, .35f); wl.range = 6f; wl.intensity = 1.2f;

        // El tronco donde se sientan (frente a la cabaña).
        Vector3 seat = O + new Vector3(0f, 0f, 3.1f);
        StoryKit.Prim(PrimitiveType.Cylinder, "Tronco", seat + Vector3.up * 0.23f, new Vector3(0.46f, 1.0f, 0.46f), darkWood, root,
            Quaternion.Euler(0f, 0f, 90f));
        // Valla.
        for (int i = -4; i <= 4; i++)
        {
            if (Mathf.Abs(i) < 1) continue;
            Vector3 fp = O + new Vector3(i * 1.6f, 0f, -1.5f);
            StoryKit.Box("Poste", fp + Vector3.up * 0.5f, new Vector3(0.12f, 1.0f, 0.12f), darkWood, root);
            if (Mathf.Abs(i) < 4)
                StoryKit.Box("Travesano", fp + new Vector3(Mathf.Sign(i) * 0.8f, 0.7f, 0f), new Vector3(1.6f, 0.08f, 0.06f), darkWood, root);
        }

        // Árboles, arbustos, hierba y flores.
        Random.State rs = Random.state;
        Random.InitState(1234);
        if (a.trees != null && a.trees.Length > 0)
            for (int i = 0; i < 16; i++)
            {
                float ang = i / 16f * 360f + Random.Range(-10f, 10f);
                float r = Random.Range(13f, 26f);
                Vector3 p = O + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * r;
                if (p.z < O.z - 6f && Mathf.Abs(p.x - O.x) < 9f) continue;   // deja libre la vista de la cámara
                GameObject tr = StoryKit.Place(a.trees[i % a.trees.Length], p, Random.Range(5f, 8f), Random.Range(0f, 360f), root);
            }
        if (a.bushes != null && a.bushes.Length > 0)
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = O + new Vector3(Random.Range(-12f, 12f), 0f, Random.Range(-2f, 14f));
                if (Mathf.Abs(p.x - O.x) < 3.4f && p.z < O.z + 10f) continue;
                StoryKit.Place(a.bushes[i % a.bushes.Length], p, Random.Range(0.7f, 1.3f), Random.Range(0f, 360f), root);
            }
        if (a.grass)
            for (int i = 0; i < 70; i++)
            {
                Vector3 p = O + new Vector3(Random.Range(-14f, 14f), 0f, Random.Range(-12f, 14f));
                if (Mathf.Abs(p.x - O.x + 0.8f) < 1.2f && p.z < O.z + 4.5f) continue;   // camino
                if (Mathf.Abs(p.x - O.x) < 3.2f && p.z > O.z + 4.6f && p.z < O.z + 9.8f) continue;   // cabaña
                StoryKit.Place(a.grass, p, Random.Range(0.25f, 0.45f), Random.Range(0f, 360f), root);
            }
        Color[] petals = { new Color(1f, .9f, .3f), new Color(1f, .45f, .5f), Color.white, new Color(.7f, .5f, 1f) };
        Material stem = StoryKit.Mat(new Color(.2f, .45f, .15f));
        for (int i = 0; i < 40; i++)
        {
            Vector3 p = O + new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-9f, 12f));
            if (Mathf.Abs(p.x - O.x + 0.8f) < 1.1f) continue;
            if (Mathf.Abs(p.x - O.x) < 3.2f && p.z > O.z + 4.6f && p.z < O.z + 9.8f) continue;
            StoryKit.Prim(PrimitiveType.Cylinder, "Tallo", p + Vector3.up * 0.12f, new Vector3(0.02f, 0.12f, 0.02f), stem, root);
            StoryKit.Prim(PrimitiveType.Sphere, "Flor", p + Vector3.up * 0.26f, Vector3.one * 0.09f,
                StoryKit.Mat(petals[i % petals.Length], 0.2f), root);
        }
        Random.state = rs;
    }
}

/// <summary>El gatito respira despacio mientras duerme.</summary>
public class CatBreath : MonoBehaviour
{
    Vector3 baseScale;
    void Start() { baseScale = transform.localScale; }
    void Update()
    {
        float k = 1f + Mathf.Sin(Time.time * 2.2f) * 0.025f;
        transform.localScale = new Vector3(baseScale.x * k, baseScale.y * (2f - k), baseScale.z);
    }
}
