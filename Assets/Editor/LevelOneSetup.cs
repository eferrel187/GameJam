using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menú  Juego > Preparar Nivel1 y Nivel2
///
/// Nivel1:
///  - Primera puerta: el marco viejo se sustituye por Wall_ArchRound con una
///    puerta doble Doors_RoundArch_L + _R (se abre con la llave).
///  - Segunda puerta (pared lateral de la sala del gato): mismo arco, detrás
///    oscuridad total. torture_device_01 queda delante; KidnapSequence hace
///    que una mano salga de la oscuridad y se lo lleve.
///  - Llave más pequeña y colisiones en mesa / torture_device_011.
/// Nivel2: añade el jugador y la cámara al inicio del pasillo.
/// Build: Tutorial, Nivel1, Nivel2.  Después reconstruye el Tutorial.
/// Se puede ejecutar varias veces (borra lo que creó antes).
/// </summary>
public static class LevelOneSetup
{
    const string Level1 = "Assets/00_Scenes/Nivel1.unity";
    const string Level2 = "Assets/00_Scenes/Nivel2.unity";
    const string Prefabs = "Assets/04_Prefabs/";

    // Pasillo/sala de Nivel1 (a lo largo de X).
    const float WallZPlus = 1.213f;
    const float WallZMinus = -2.93f;
    const float FirstDoorX = 11.1f;
    const float SecondDoorX = 14.3f;      // posición deseada (se ajusta a las paredes)
    const float ArchScale = 1.035f;       // 4.0 m -> 4.14 m (ancho del pasillo)

    static Scene level;
    static readonly List<string> report = new List<string>();

    [MenuItem("Juego/Preparar Nivel1 y Nivel2")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        report.Clear();

        level = EditorSceneManager.OpenScene(Level1, OpenSceneMode.Single);
        SceneManager.SetActiveScene(level);

        // Restaura las paredes que ocultó una ejecución anterior.
        GameObject prev = FindAny("SecuenciaSecuestro");
        KidnapSequence prevSeq = prev ? prev.GetComponent<KidnapSequence>() : null;
        string[] toRestore = prevSeq != null && prevSeq.hiddenBySetup != null && prevSeq.hiddenBySetup.Length > 0
            ? prevSeq.hiddenBySetup
            : (prev != null ? new[] { "Wall (39)", "Wall (43)", "Wall (44)" } : new string[0]);
        foreach (string n in toRestore)
            {
                GameObject w = FindAny(n);
                if (w != null) w.SetActive(true);
            }

        // Limpia lo creado en ejecuciones anteriores.
        foreach (string n in new[] { "Puerta1", "Puerta2", "SecuenciaSecuestro", "torture_device_01" })
        {
            GameObject old = FindAny(n);
            if (old != null) Object.DestroyImmediate(old);
        }

        GameObject player = FindAny("Player");
        CharacterController cc = player.GetComponent<CharacterController>();
        float ccBottom = player.transform.TransformPoint(cc.center).y - cc.height * player.transform.lossyScale.y * 0.5f;
        float corridorZ = (WallZPlus + WallZMinus) * 0.5f;
        // Suelo REAL (en el editor el jugador está algo por encima; al jugar cae).
        float feet = GroundAt(FirstDoorX - 1f, corridorZ, ccBottom);
        report.Add("Suelo real y=" + feet.ToString("0.00") + " (jugador en editor y=" + ccBottom.ToString("0.00") + ")");

        // ---------------- Primera puerta (con llave)
        foreach (string n in new[] { "Door_Straight", "Wall (10)", "Wall (15)", "Wall (35)" })
            Hide(n);

        GameObject door1 = BuildArchDoor("Puerta1", "Puerta_Llave",
            new Vector3(FirstDoorX, feet, corridorZ), 90f, Vector3.right);

        // ---------------- Segunda puerta (oscura), en la pared lateral +Z de la sala
        float doorX = FitSideDoor(SecondDoorX, out List<GameObject> removed);
        foreach (GameObject w in removed) { w.SetActive(false); report.Add("Ocultado " + w.name); }

        GameObject door2 = BuildArchDoor("Puerta2", "Puerta_Oscura",
            new Vector3(doorX, feet, WallZPlus - 0.06f), 0f, Vector3.forward);
        Bounds opening2 = LeafBounds(door2.transform.Find("Puerta_Oscura"));

        // Oscuridad total detrás (sin colisión, negro sin luz).
        GameObject dark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dark.name = "Oscuridad";
        Object.DestroyImmediate(dark.GetComponent<Collider>());
        dark.transform.SetParent(door2.transform, false);
        dark.transform.position = new Vector3(doorX, feet + 2f, WallZPlus + 0.25f + 1.5f);
        dark.transform.localScale = new Vector3(7f, 6f, 3f);
        dark.GetComponent<Renderer>().sharedMaterial = BlackMaterial();

        // Suelo invisible bajo el arco y detrás (el suelo de la sala acaba antes
        // de la pared: sin esto queda una ranura donde el jugador se cae).
        Solid(door2.transform, "Suelo_Umbral", new Vector3(doorX, feet - 0.3f, WallZPlus + 0.9f),
            new Vector3(opening2.size.x + 0.4f, 0.6f, 3.2f));

        // ---------------- torture_device_01 (lo que se lleva la mano)
        GameObject deviceSource = FindAny("torture_device_011");
        GameObject device = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/05_models/torture_device_011.fbx"), level);
        device.name = "torture_device_01";
        device.transform.rotation = deviceSource.transform.rotation;
        device.transform.localScale = deviceSource.transform.lossyScale;
        Bounds db = Bounds(device);
        device.transform.position += new Vector3(doorX, feet, WallZPlus - 0.95f) -
                                     new Vector3(db.center.x, db.min.y, db.center.z);
        FitCollider(device);

        // ---------------- Mano y secuencia
        GameObject handPrefab = MonsterHandBuilder.Build();
        GameObject hand = (GameObject)PrefabUtility.InstantiatePrefab(handPrefab, level);
        hand.name = "ManoMonstruo";
        hand.transform.localScale = Vector3.one * 1.6f;

        GameObject seqGo = new GameObject("SecuenciaSecuestro");
        KidnapSequence seq = seqGo.AddComponent<KidnapSequence>();
        seq.firstDoor = door1.GetComponentInChildren<ArchDoor>();
        seq.darkDoor = door2.GetComponentInChildren<ArchDoor>();
        seq.hand = hand.transform;
        hand.transform.SetParent(seqGo.transform, true);
        seq.hiddenBySetup = removed.Select(w => w.name).ToArray();
        seq.exitCenter = new Vector3(doorX, feet + 1.2f, WallZPlus + 0.25f);
        seq.exitSize = new Vector3(opening2.size.x + 0.6f, 20f, 0.5f);

        // ---------------- Objetos de la sala: colisiones y llave
        foreach (string n in new[] { "table", "torture_device_011" })
        {
            GameObject g = FindAny(n);
            if (g != null && FitCollider(g)) report.Add("Colisión añadida a " + n);
        }

        GameObject key = FindAny("Key4");
        if (key != null && !key.name.EndsWith("*"))
        {
            // Siempre respecto al tamaño original del prefab (no se acumula).
            GameObject keyAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/05_models/Key4.fbx");
            Vector3 baseScale = PrefabUtility.GetCorrespondingObjectFromSource(key) != null
                ? PrefabUtility.GetCorrespondingObjectFromSource(key).transform.localScale
                : keyAsset.transform.localScale;
            key.transform.localScale = baseScale * 0.7f;
            report.Add("Llave al 70 %");
        }

        EditorSceneManager.MarkSceneDirty(level);
        EditorSceneManager.SaveScene(level);

        // ---------------- Nivel2: jugador y cámara al inicio
        PrepareLevel2(player);

        // ---------------- Build Settings
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != Level2).ToList();
        int i1 = scenes.FindIndex(s => s.path == Level1);
        scenes.Insert(i1 >= 0 ? i1 + 1 : scenes.Count, new EditorBuildSettingsScene(Level2, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        // ---------------- Tutorial con la nueva puerta
        TutorialSceneBuilder.Build();

        // ---------------- Luces de antorchas y ambiente de calabozo
        DungeonLightingSetup.Apply();

        report.Add("Puerta 2 en x=" + doorX.ToString("0.00"));
        System.IO.File.WriteAllLines("informe_nivel1.txt", report);
        Debug.Log("Nivel1 y Nivel2 preparados:\n" + string.Join("\n", report));

        EditorSceneManager.OpenScene(Level1, OpenSceneMode.Single);
    }

    // ------------------------------------------------------------------ puertas

    static GameObject BuildArchDoor(string groupName, string doorName, Vector3 bottomCenter,
        float yaw, Vector3 openTowards)
    {
        GameObject group = new GameObject(groupName);
        SceneManager.MoveGameObjectToScene(group, level);

        // Arco
        GameObject arch = Instance("Wall_ArchRound", group.transform, yaw);
        arch.name = "Arco";
        Place(arch, bottomCenter);

        // Puerta doble
        GameObject door = new GameObject(doorName);
        door.transform.SetParent(group.transform, false);
        GameObject left = Instance("Doors_RoundArch_L", door.transform, yaw);
        GameObject right = Instance("Doors_RoundArch_R", door.transform, yaw);

        // Coloca cada hoja con su lado "de unión" (el de la curva del arco) al centro.
        Bounds ab = Bounds(arch);
        bool alongZ = ab.size.z > ab.size.x;
        float centerAxis = alongZ ? bottomCenter.z : bottomCenter.x;
        PlaceLeaf(left, bottomCenter, alongZ, centerAxis, true);
        PlaceLeaf(right, bottomCenter, alongZ, centerAxis, false);

        // Colisiones de las hojas (se quitan al abrir).
        FitColliderLocal(left);
        FitColliderLocal(right);

        ArchDoor archDoor = door.AddComponent<ArchDoor>();
        archDoor.leftLeaf = left.transform;
        archDoor.rightLeaf = right.transform;
        archDoor.openTowards = openTowards;

        // Colisiones del arco: dos pilares y el dintel (el hueco queda libre).
        Bounds opening = LeafBounds(door.transform);
        float thick = alongZ ? ab.size.x : ab.size.z;
        float aMin = alongZ ? ab.min.z : ab.min.x, aMax = alongZ ? ab.max.z : ab.max.x;
        float oMin = alongZ ? opening.min.z : opening.min.x, oMax = alongZ ? opening.max.z : opening.max.x;
        float h = ab.size.y, cy = ab.min.y + h * 0.5f;
        float across = alongZ ? ab.center.x : ab.center.z;

        System.Func<float, float, float, float, Vector3> V = (along, y, a, b) =>
            alongZ ? new Vector3(across, y, along) : new Vector3(along, y, across);
        System.Func<float, float, Vector3> S = (len, hy) =>
            alongZ ? new Vector3(thick, hy, len) : new Vector3(len, hy, thick);

        Solid(group.transform, "Colision_Arco_A", V((aMin + oMin) * 0.5f, cy, 0, 0), S(oMin - aMin, h));
        Solid(group.transform, "Colision_Arco_B", V((oMax + aMax) * 0.5f, cy, 0, 0), S(aMax - oMax, h));
        float lintelBottom = opening.max.y;
        Solid(group.transform, "Colision_Arco_Dintel",
            V((oMin + oMax) * 0.5f, (lintelBottom + ab.max.y) * 0.5f, 0, 0),
            S(oMax - oMin, ab.max.y - lintelBottom));

        // Marco con Support_Right: cubre los huecos entre las hojas y el arco.
        AddFrame(group.transform, bottomCenter, alongZ, across, thick, (oMax - oMin) * 0.5f, openTowards);

        report.Add(groupName + ": arco " + ab.size.ToString("F2") + ", hueco " + opening.size.ToString("F2"));
        return group;
    }

    /// Dos Support_Right (uno girado 180°) en el lado por el que llega el jugador:
    /// el pilar ocupa la ranura entre hoja y jamba (0.33 m) y la curva sigue el
    /// borde superior de las hojas. Quedan delante del arco, así que no chocan
    /// con las hojas al abrirse (se abren hacia el otro lado).
    static void AddFrame(Transform group, Vector3 bottomCenter, bool alongZ, float across,
        float thick, float leafHalf, Vector3 into)
    {
        const float PillarInner = 1.22f;   // del pivote del Support al borde interior del pilar (m, escala 1)
        const float HalfDepth = 0.33f;     // medio grosor del Support
        into = into.normalized;

        Vector3 plane = alongZ ? new Vector3(across, bottomCenter.y, bottomCenter.z)
                               : new Vector3(bottomCenter.x, bottomCenter.y, across);
        GameObject frame = new GameObject("Marco_Support");
        frame.transform.SetParent(group, false);
        frame.transform.rotation = Quaternion.LookRotation(into);
        frame.transform.position = plane - into * (thick * 0.5f + HalfDepth * ArchScale + 0.004f);
        float sx = (leafHalf - 0.07f) / PillarInner;   // tapa el borde de la hoja también en perspectiva
        frame.transform.localScale = new Vector3(sx, ArchScale, ArchScale);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Support_Right.prefab");
        for (int side = 0; side < 2; side++)
        {
            GameObject sup = (GameObject)PrefabUtility.InstantiatePrefab(prefab, level);
            sup.name = side == 0 ? "Support_Right_Izq" : "Support_Right_Der";
            sup.transform.SetParent(frame.transform, false);
            sup.transform.localRotation = Quaternion.Euler(0f, side == 0 ? 0f : 180f, 0f) * prefab.transform.rotation;
            sup.transform.localScale = prefab.transform.localScale;
            sup.transform.localPosition = new Vector3(side == 0 ? -1.5f : 1.5f, 0f, 0f);
            foreach (MeshFilter mf in sup.GetComponentsInChildren<MeshFilter>())
                if (mf.GetComponent<Collider>() == null)
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
        Bounds fb = Bounds(frame);
        report.Add("  Marco Support_Right " + fb.min.ToString("F2") + " .. " + fb.max.ToString("F2") + " (escala x " + sx.ToString("F3") + ")");
    }

    static GameObject Instance(string prefabName, Transform parent, float yaw)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + prefabName + ".prefab");
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, level);
        go.transform.SetParent(parent, false);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation;
        go.transform.localScale = prefab.transform.localScale * ArchScale;
        return go;
    }

    static void Place(GameObject go, Vector3 bottomCenter)
    {
        Bounds b = Bounds(go);
        go.transform.position += bottomCenter - new Vector3(b.center.x, b.min.y, b.center.z);
    }

    /// Lleva la hoja a su mitad del hueco. El lado de unión es aquel hacia el que
    /// se desplazan los vértices más altos (la curva del arco está por fuera).
    static void PlaceLeaf(GameObject leaf, Vector3 bottomCenter, bool alongZ, float center, bool first)
    {
        Bounds b = Bounds(leaf);
        MeshFilter mf = leaf.GetComponentInChildren<MeshFilter>();
        Vector3[] verts = mf.sharedMesh.vertices;
        float topY = b.max.y - b.size.y * 0.08f;
        float sum = 0f; int count = 0;
        foreach (Vector3 v in verts)
        {
            Vector3 w = mf.transform.TransformPoint(v);
            if (w.y >= topY) { sum += alongZ ? w.z : w.x; count++; }
        }
        float topCenter = count > 0 ? sum / count : (alongZ ? b.center.z : b.center.x);
        float leafCenter = alongZ ? b.center.z : b.center.x;
        bool seamIsMax = topCenter > leafCenter;        // la unión está del lado +

        float width = alongZ ? b.size.z : b.size.x;
        // La primera hoja va al lado "menos", la segunda al "más".
        float targetCenter = first ? center - width * 0.5f : center + width * 0.5f;
        bool needsSeamMax = first;                      // la del lado "menos" une por su lado +
        if (seamIsMax != needsSeamMax)
        {
            // Girarla 180° para que la curva quede por fuera.
            leaf.transform.RotateAround(b.center, Vector3.up, 180f);
            b = Bounds(leaf);
        }

        Vector3 target = alongZ
            ? new Vector3(bottomCenter.x, bottomCenter.y, targetCenter)
            : new Vector3(targetCenter, bottomCenter.y, bottomCenter.z);
        leaf.transform.position += target - new Vector3(b.center.x, b.min.y, b.center.z);
    }

    /// Busca dónde poner la puerta lateral sin dejar huecos entre arco y paredes.
    static float FitSideDoor(float preferred, out List<GameObject> removed)
    {
        List<GameObject> walls = new List<GameObject>();
        foreach (GameObject root in level.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.gameObject.activeInHierarchy && t.name.StartsWith("Wall") && t.parent == null)
                {
                    Bounds b = Bounds(t.gameObject);
                    if (Mathf.Abs(b.center.z - WallZPlus) < 0.45f && b.size.x > b.size.z &&
                        b.max.x > FirstDoorX + 0.2f && b.center.y < 2.5f)
                        walls.Add(t.gameObject);
                }

        float roomMin = walls.Min(w => Bounds(w).min.x);
        float roomMax = walls.Max(w => Bounds(w).max.x);
        const float archHalf = 4.004f * ArchScale * 0.5f;
        const float openHalf = 1.146f * ArchScale;

        float best = preferred; float bestScore = float.MaxValue;
        List<GameObject> bestRemoved = new List<GameObject>();
        for (float x = roomMin + archHalf; x <= roomMax - archHalf; x += 0.05f)
        {
            // Quita las paredes que invaden el hueco.
            List<GameObject> rem = walls.Where(w =>
            {
                Bounds b = Bounds(w);
                return b.max.x > x - openHalf + 0.02f && b.min.x < x + openHalf - 0.02f;
            }).ToList();

            // ¿Quedan huecos sin cubrir entre roomMin y roomMax?
            var spans = walls.Except(rem).Select(w => { Bounds b = Bounds(w); return new Vector2(b.min.x, b.max.x); }).ToList();
            spans.Add(new Vector2(x - archHalf, x + archHalf));
            spans.Sort((a, b) => a.x.CompareTo(b.x));
            float covered = roomMin, gap = 0f;
            foreach (Vector2 s in spans)
            {
                if (s.x > covered) gap += s.x - covered;
                covered = Mathf.Max(covered, s.y);
            }
            if (covered < roomMax) gap += roomMax - covered;

            float score = gap * 10f + Mathf.Abs(x - preferred);
            if (score < bestScore) { bestScore = score; best = x; bestRemoved = rem; }
        }

        removed = bestRemoved;
        return best;
    }

    // ------------------------------------------------------------------ Nivel2

    static void PrepareLevel2(GameObject sourcePlayer)
    {
        Scene l2 = EditorSceneManager.OpenScene(Level2, OpenSceneMode.Additive);

        // Quita jugador/cámara que este mismo script puso antes.
        foreach (GameObject root in l2.GetRootGameObjects())
            if (root.name == "Player" ||
                (root.name == "Main Camera" && root.GetComponent<CameraIntroFollow>() != null))
                Object.DestroyImmediate(root);

        // La cámara original de Nivel2 (sin seguimiento) se desactiva.
        foreach (GameObject root in l2.GetRootGameObjects())
            if (root.name == "Main Camera")
            {
                root.name = "Main Camera (Nivel2 original)";
                root.SetActive(false);
            }

        SceneManager.SetActiveScene(l2);

        // Nivel2 no tenía colisiones: suelos sólidos (como en Nivel1).
        int floors = 0;
        foreach (GameObject root in l2.GetRootGameObjects())
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                bool isFloor = false;
                for (Transform t = mf.transform; t != null; t = t.parent)
                    if (t.name.StartsWith("Floor_Modular")) { isFloor = true; break; }
                if (!isFloor || mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;

                BoxCollider box = mf.gameObject.AddComponent<BoxCollider>();
                box.center = mf.sharedMesh.bounds.center;
                box.size = mf.sharedMesh.bounds.size;
                floors++;
            }
        report.Add("Nivel2: colisión añadida a " + floors + " suelos");

        GameObject player = Object.Instantiate(sourcePlayer);
        player.name = "Player";
        SceneManager.MoveGameObjectToScene(player, l2);
        player.transform.SetPositionAndRotation(
            new Vector3(-7f, sourcePlayer.transform.position.y, -0.98f), sourcePlayer.transform.rotation);
        player.transform.localScale = sourcePlayer.transform.lossyScale;

        GameObject sourceCam = level.GetRootGameObjects().First(r => r.name == "Main Camera");
        GameObject cam = Object.Instantiate(sourceCam);
        cam.name = "Main Camera";
        cam.tag = "MainCamera";
        SceneManager.MoveGameObjectToScene(cam, l2);
        CameraIntroFollow follow = cam.GetComponent<CameraIntroFollow>();
        if (follow != null)
        {
            SerializedObject so = new SerializedObject(follow);
            so.FindProperty("player").objectReferenceValue = player.transform;
            so.FindProperty("routePoints").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(l2);
        EditorSceneManager.SaveScene(l2);
        EditorSceneManager.CloseScene(l2, true);
        SceneManager.SetActiveScene(level);
        report.Add("Nivel2: jugador en (-7, " + sourcePlayer.transform.position.y.ToString("0.00") + ", -0.98)");
    }

    // ------------------------------------------------------------------ utilidades

    /// Altura del piso (Floor_Modular) en x,z.
    static float GroundAt(float x, float z, float fallback)
    {
        Physics.SyncTransforms();
        float best = float.NegativeInfinity;
        foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, fallback + 4f, z), Vector3.down, 10f,
                     Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            for (Transform t = hit.collider.transform; t != null; t = t.parent)
                if (t.name.StartsWith("Floor_Modular")) { best = Mathf.Max(best, hit.point.y); break; }
        }
        return float.IsNegativeInfinity(best) ? fallback : best;
    }

    static GameObject FindAny(string name)
    {
        foreach (GameObject root in level.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }

    static void Hide(string name)
    {
        GameObject g = FindAny(name);
        if (g != null && g.activeSelf) { g.SetActive(false); report.Add("Ocultado " + name); }
    }

    static Bounds Bounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Bounds LeafBounds(Transform door)
    {
        return Bounds(door.gameObject);
    }

    static GameObject Solid(Transform parent, string name, Vector3 center, Vector3 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.AddComponent<BoxCollider>().size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        return go;
    }

    /// Caja ajustada a la malla, en el espacio del propio objeto.
    static void FitColliderLocal(GameObject go)
    {
        foreach (Collider c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.center = mf.sharedMesh.bounds.center;
        box.size = mf.sharedMesh.bounds.size;
    }

    /// Añade una caja (alineada a los ejes) si el objeto no tiene colisión.
    static bool FitCollider(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>() != null) return false;
        Bounds b = Bounds(go);
        GameObject col = new GameObject("Colision_" + go.name);
        col.transform.SetParent(go.transform, true);
        col.transform.position = b.center;
        col.transform.rotation = Quaternion.identity;
        Vector3 ls = go.transform.lossyScale;
        col.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
        col.AddComponent<BoxCollider>().size = b.size;
        return true;
    }

    static Material BlackMaterial()
    {
        if (!AssetDatabase.IsValidFolder("Assets/04_Prefabs/MonsterHand"))
            AssetDatabase.CreateFolder("Assets/04_Prefabs", "MonsterHand");
        string path = "Assets/04_Prefabs/MonsterHand/Oscuridad.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", Color.black);
        EditorUtility.SetDirty(m);
        return m;
    }
}
