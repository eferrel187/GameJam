using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Menú  Juego > Crear escena Tutorial
///
/// Construye Assets/00_Scenes/Tutorial.unity clonando las piezas de Nivel1
/// (paredes, piso, techo, antorchas, puerta, llave, jugador y cámara) en un
/// pasillo largo con estaciones de práctica, y la pone primera en el Build.
/// Se puede volver a ejecutar: reemplaza la escena Tutorial.
/// </summary>
public static class TutorialSceneBuilder
{
    const string Level1Path = "Assets/00_Scenes/Nivel1.unity";
    const string TutorialPath = "Assets/00_Scenes/Tutorial.unity";

    // Pasillo de Nivel1 a lo largo de X. Paredes en z = 1.213 y z = -2.93.
    const float StartX = -9.58f;
    const float EndX = 40.5f;
    const float WallZPlus = 1.213f;
    const float WallZMinus = -2.93f;

    // Estaciones del tutorial (X).
    const float SpawnX = -7f;
    const float BarrierX = 11f;
    const float LowCeilingFrom = 15.6f;
    const float LowCeilingTo = 19.8f;
    const float TrapsFrom = 22f;
    const float KeyX = 31.5f;
    const float DoorX = 35.5f;
    const float FinishX = 37f;
    const string Prefabs = "Assets/04_Prefabs/";

    static Dictionary<string, GameObject> index;
    static Scene tutorial;
    static int wallCount;

    [MenuItem("Juego/Crear escena Tutorial")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene level = EditorSceneManager.OpenScene(
            Level1Path, OpenSceneMode.Single);

        index = new Dictionary<string, GameObject>();
        foreach (GameObject root in level.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (!index.ContainsKey(t.name))
                    index[t.name] = t.gameObject;

        // --- Iluminación de Nivel1 (ambiente, niebla, cielo)
        var ambientMode = RenderSettings.ambientMode;
        var ambientSky = RenderSettings.ambientSkyColor;
        var ambientEquator = RenderSettings.ambientEquatorColor;
        var ambientGround = RenderSettings.ambientGroundColor;
        var ambientLight = RenderSettings.ambientLight;
        var ambientIntensity = RenderSettings.ambientIntensity;
        var skybox = RenderSettings.skybox;
        var fog = RenderSettings.fog;
        var fogColor = RenderSettings.fogColor;
        var fogMode = RenderSettings.fogMode;
        var fogDensity = RenderSettings.fogDensity;
        var fogStart = RenderSettings.fogStartDistance;
        var fogEnd = RenderSettings.fogEndDistance;
        var reflectionIntensity = RenderSettings.reflectionIntensity;

        tutorial = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(tutorial);

        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
        RenderSettings.ambientLight = ambientLight;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.skybox = skybox;
        RenderSettings.fog = fog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
        RenderSettings.reflectionIntensity = reflectionIntensity;

        wallCount = 0;

        // Luces y volúmenes que hubiera en Nivel1.
        foreach (Light light in Object.FindObjectsByType<Light>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (light.gameObject.scene == level &&
                light.type == LightType.Directional)
                Clone(light.name, Vector3.zero);

        foreach (Volume volume in Object.FindObjectsByType<Volume>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (volume.gameObject.scene == level)
                Clone(volume.name, Vector3.zero);

        Transform geometry = new GameObject("Pasillo").transform;

        // --- Piso: mismas dos filas que Nivel1, repetidas a lo largo de X.
        float floorStep = SizeX("Floor_Modular (3)", 3.96f) * 0.98f;
        float firstFloorX = Pos("Floor_Modular (38)", new Vector3(-8.57f, 0, 0)).x;
        for (float x = firstFloorX; x <= EndX + floorStep; x += floorStep)
        {
            Parent(Clone("Floor_Modular (3)", DX("Floor_Modular (3)", x), "Floor_Modular"), geometry);
            Parent(Clone("Floor_Modular (42)", DX("Floor_Modular (42)", x), "Floor_Modular"), geometry);
        }

        // --- Paredes laterales: el patrón de Nivel1 se repite cada 3.384 m.
        const float wallPeriod = 3.384f;
        string[] wallPattern = { "Wall", "Wall (1)", "Wall (4)", "Wall (7)" };
        for (float x = StartX + 0.6f; x <= EndX; x += wallPeriod)
            foreach (string piece in wallPattern)
                Parent(CloneWall(piece, DX("Wall", x)), geometry);

        // --- Techo
        float ceilingStep = 1.77f;
        for (float x = StartX + 1f; x <= EndX + ceilingStep; x += ceilingStep)
            Parent(CloneWall("Wall (52)", DX("Wall (52)", x)), geometry);

        // --- Paredes de los extremos (inicio y final)
        string[] cap = { "Wall (27)", "Wall (28)", "Wall (29)" };
        foreach (string piece in cap)
        {
            Parent(CloneWall(piece, Vector3.zero), geometry);
            Parent(CloneWall(piece, new Vector3(EndX - StartX, 0, 0)), geometry);
        }

        // --- Antorchas a ambos lados
        for (float x = StartX + 3f; x <= EndX - 2f; x += 7f)
        {
            Parent(Clone("Torch (6)", DX("Torch (6)", x), "Torch"), geometry);
            Parent(Clone("Torch (1)", DX("Torch (1)", x + 3.5f), "Torch"), geometry);
        }

        // --- Jugador
        GameObject sourcePlayer = index.ContainsKey("Player") ? index["Player"] : null;
        if (sourcePlayer == null)
        {
            Debug.LogError("No se encontró Player en Nivel1.");
            return;
        }

        CharacterController cc = sourcePlayer.GetComponent<CharacterController>();
        float scaleY = sourcePlayer.transform.lossyScale.y;
        float standHeight = cc.height * scaleY;
        float crouchHeight = standHeight * 0.6f;
        float feet = sourcePlayer.transform.TransformPoint(cc.center).y -
                     standHeight * 0.5f;
        // Suelo REAL bajo el jugador (en el editor flota un poco; al jugar cae).
        feet = GroundY(sourcePlayer.transform.position.x, sourcePlayer.transform.position.z, feet);

        GameObject player = Clone("Player", DX("Player", SpawnX));
        float corridorZ = (WallZPlus + WallZMinus) * 0.5f;
        float corridorWidth = WallZPlus - WallZMinus;

        Transform props = new GameObject("Obstaculos").transform;
        float zMin = WallZMinus + 0.25f, zMax = WallZPlus - 0.25f;

        // --- SALTAR: fila de cajas de lado a lado (con un barril en cada punta)
        float crateHeight = 0.55f;
        GameObject probe = Prop("Crate", Vector3.zero, 0f, crateHeight, props);
        float crateWidth = probe ? Bounds(probe).size.z : 0.6f;
        if (probe) Object.DestroyImmediate(probe);
        int crates = Mathf.Max(1, Mathf.FloorToInt((zMax - zMin) / crateWidth));
        float rowStart = corridorZ - (crates - 1) * crateWidth * 0.5f;
        for (int i = 0; i < crates; i++)
        {
            float yaw = (i % 2 == 0) ? 0f : 90f;
            Prop("Crate", new Vector3(BarrierX, feet, rowStart + i * crateWidth), yaw, crateHeight, props);
        }
        Solid("Colision_Cajas", new Vector3(BarrierX, feet + crateHeight * 0.5f - 0.05f, corridorZ),
            new Vector3(Mathf.Max(0.5f, crateWidth), crateHeight + 0.1f, corridorWidth), props, true);
        // Separados de las paredes (sin superponerse) y fuera del camino.
        Prop("Barrel", new Vector3(BarrierX - 1.6f, feet, zMax - 0.45f), 0f, 0.9f, props, true);
        Prop("Pot1", new Vector3(BarrierX - 2.4f, feet, zMin + 0.4f), 30f, 0.5f, props, true);

        // --- AGACHARSE: columnas caídas encajadas entre las paredes
        // Justo por encima del jugador agachado: de pie es imposible pasar.
        // Agachado cabe (con holgura por el "skin" del controlador); de pie no.
        // El controlador deja un "skin" bajo los pies y otro de contacto arriba:
        // sin ese margen el jugador agachado roza la viga y avanza a tirones.
        float skin = cc.skinWidth * scaleY;
        float lowBottom = feet + crouchHeight + skin * 2f + 0.12f;
        float lowTop = feet + 4f;
        Quaternion lying = Quaternion.Euler(90f, 0f, 0f);   // eje largo de la columna -> Z
        for (float x = LowCeilingFrom + 0.4f; x <= LowCeilingTo - 0.3f; x += 1.15f)
        {
            GameObject column = PropRotated("Column_Square", lying, props);
            if (column == null) break;
            Bounds b = Bounds(column);
            column.transform.localScale *= (corridorWidth + 0.4f) / Mathf.Max(0.01f, b.size.z);
            b = Bounds(column);
            column.transform.position += new Vector3(x, lowBottom, corridorZ) -
                                         new Vector3(b.center.x, b.min.y, b.center.z);
            Bounds placed = Bounds(column);
            Debug.Log("Column_Square: base visible a " + (placed.min.y - feet).ToString("0.00") +
                      " m del suelo (colisión a " + (lowBottom - feet).ToString("0.00") +
                      " m; jugador de pie " + standHeight.ToString("0.00") +
                      " m, agachado " + crouchHeight.ToString("0.00") + " m)");
        }
        Solid("Colision_Techo_Bajo",
            new Vector3((LowCeilingFrom + LowCeilingTo) * 0.5f, (lowBottom + lowTop) * 0.5f, corridorZ),
            new Vector3(LowCeilingTo - LowCeilingFrom, lowTop - lowBottom, corridorWidth), props, true);
        Prop("Bricks", new Vector3(LowCeilingFrom - 0.6f, feet, zMin + 0.4f), 20f, 0.25f, props);
        Prop("Bricks", new Vector3(LowCeilingTo + 0.5f, feet, zMax - 0.4f), -15f, 0.25f, props);

        // --- TRAMPAS: cepos abiertos en el suelo (quitan una vida)
        Vector3[] traps =
        {
            new Vector3(TrapsFrom + 2.4f, feet, corridorZ + 1.0f),
            new Vector3(TrapsFrom + 4.6f, feet, corridorZ - 1.0f),
            new Vector3(TrapsFrom + 6.8f, feet, corridorZ + 0.3f),
        };
        foreach (Vector3 t in traps)
        {
            GameObject trap = Prop("BearTrap_Open", t, Random.Range(0f, 360f), 0.18f, props);
            if (trap == null) continue;
            Bounds tb = Bounds(trap);
            GameObject zone = Solid("Daño_Cepo", tb.center + Vector3.up * 0.25f,
                new Vector3(tb.size.x * 0.8f, 0.6f, tb.size.z * 0.8f), props, false);
            zone.GetComponent<BoxCollider>().isTrigger = true;
            zone.AddComponent<SpikeDamage>();
        }
        Prop("Skull", new Vector3(TrapsFrom + 4.6f, feet, corridorZ - 1.45f), 140f, 0.22f, props);

        // --- Decoración de la llave: barril a un lado (no tapa el paso),
        //     velas y un cofre pegados a las paredes.
        float keyZ = zMax - 0.75f;
        Prop("Barrel", new Vector3(KeyX, feet, keyZ), 0f, 0.8f, props, true);
        Prop("Candles_1", new Vector3(KeyX + 0.9f, feet, zMax - 0.4f), 0f, 0.45f, props);
        Prop("Chest_Base", new Vector3(KeyX - 1.2f, feet, zMin + 0.5f), 90f, 0.5f, props, true);

        // --- Estación LLAVE Y PUERTA: la misma puerta de arco que Nivel1
        //     (Wall_ArchRound + Doors_RoundArch_L/R, grupo "Puerta1").
        Clone("Key4", DX("Key4", KeyX) +
            new Vector3(0f, 0.1f, keyZ - Pos("Key4", Vector3.zero).z));
        Clone("Puerta1", new Vector3(DoorX - 11.1f, 0f, 0f));

        // --- Cámara
        GameObject cam = Clone("Main Camera", Vector3.zero);
        if (cam != null)
        {
            CameraIntroFollow intro = cam.GetComponent<CameraIntroFollow>();
            float distance = 2.5f, height = 1.8f, look = 1.1f;

            if (intro != null)
            {
                SerializedObject so = new SerializedObject(intro);
                so.FindProperty("player").objectReferenceValue = player.transform;
                so.FindProperty("initialPause").floatValue = 0f;
                so.FindProperty("travelDuration").floatValue = 0.6f;
                so.FindProperty("routePoints").arraySize = 0;
                distance = so.FindProperty("distance").floatValue;
                height = so.FindProperty("cameraHeight").floatValue;
                look = so.FindProperty("lookHeight").floatValue;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Vector3 forward = Vector3.ProjectOnPlane(
                player.transform.forward, Vector3.up).normalized;
            cam.transform.position = player.transform.position -
                forward * distance + Vector3.up * height;
            cam.transform.LookAt(player.transform.position + Vector3.up * look);
        }

        // --- Director del tutorial
        TutorialDirector director =
            new GameObject("TutorialDirector").AddComponent<TutorialDirector>();
        director.steps = new[]
        {
            new TutorialDirector.Step { startX = -999f, title = "CAMINAR Y MIRAR",
                hint = "Usa  W A S D  para caminar y mueve el  RATÓN  para mirar" },
            new TutorialDirector.Step { startX = -3f, title = "CORRER",
                hint = "Mantén  SHIFT  mientras caminas para correr" },
            new TutorialDirector.Step { startX = BarrierX - 4.5f, title = "SALTAR",
                hint = "¡Un obstáculo! Pulsa  ESPACIO  para saltarlo" },
            new TutorialDirector.Step { startX = BarrierX + 1f, title = "AGACHARSE",
                hint = "El techo es bajo: mantén  C  (o CTRL) para pasar agachado" },
            new TutorialDirector.Step { startX = LowCeilingTo + 0.3f, title = "¡CUIDADO, TRAMPAS!",
                hint = "Si pisas un cepo pierdes una vida. Esquívalos por los lados" },
            new TutorialDirector.Step { startX = TrapsFrom + 8f, title = "LLAVE Y PUERTA",
                hint = "Recoge la llave y pulsa  E  junto a la puerta para abrirla" },
        };
        director.finishX = FinishX;
        director.checkpointZ = corridorZ;
        // Puntos de reaparición seguros (lejos de trampas y obstáculos).
        float[] checkpoints = { float.NaN, -3f, BarrierX - 4f, LowCeilingFrom - 1.6f,
                                LowCeilingTo + 3.4f, TrapsFrom + 8.3f };
        for (int i = 0; i < director.steps.Length && i < checkpoints.Length; i++)
        {
            director.steps[i].useCheckpoint = !float.IsNaN(checkpoints[i]);
            director.steps[i].checkpointX = float.IsNaN(checkpoints[i]) ? 0f : checkpoints[i];
        }

        // --- Guardar y dejar Tutorial como primera escena del juego
        EditorSceneManager.SaveScene(tutorial, TutorialPath);
        EditorSceneManager.CloseScene(level, true);

        List<EditorBuildSettingsScene> scenes =
            new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(TutorialPath, true)
            };

        bool hasLevel1 = false;
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s.path == TutorialPath) continue;
            if (s.path == Level1Path) hasLevel1 = true;
            scenes.Add(s);
        }
        if (!hasLevel1)
            scenes.Insert(1, new EditorBuildSettingsScene(Level1Path, true));

        EditorBuildSettings.scenes = scenes.ToArray();

        EditorSceneManager.OpenScene(TutorialPath, OpenSceneMode.Single);
        Debug.Log("Escena Tutorial creada en " + TutorialPath +
                  " y puesta primera en Build Settings.");
    }

    // ------------------------------------------------------------ utilidades

    static GameObject Clone(string name, Vector3 offset, string newName = null)
    {
        if (!index.TryGetValue(name, out GameObject source))
        {
            Debug.LogWarning("Tutorial: no se encontró '" + name + "' en Nivel1.");
            return null;
        }

        GameObject copy = Object.Instantiate(source,
            source.transform.position + offset, source.transform.rotation);
        copy.transform.localScale = source.transform.lossyScale;
        copy.name = newName ?? name;

        if (copy.scene != tutorial)
            SceneManager.MoveGameObjectToScene(copy, tutorial);

        return copy;
    }

    /// Las paredes se llaman "Wall (T#)" para que GuardDoorAndKey
    /// les ponga colisión, igual que en Nivel1.
    static GameObject CloneWall(string name, Vector3 offset)
    {
        return Clone(name, offset, "Wall (T" + (++wallCount) + ")");
    }

    /// Desplazamiento en X para que la pieza 'name' quede en x.
    static Vector3 DX(string name, float x)
    {
        return new Vector3(x - Pos(name, Vector3.zero).x, 0f, 0f);
    }

    static Vector3 Pos(string name, Vector3 fallback)
    {
        return index.TryGetValue(name, out GameObject go)
            ? go.transform.position : fallback;
    }

    static float SizeX(string name, float fallback)
    {
        if (!index.TryGetValue(name, out GameObject go)) return fallback;

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return fallback;

        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b.size.x > 0.1f ? b.size.x : fallback;
    }

    static void Parent(GameObject child, Transform parent)
    {
        if (child != null) child.transform.SetParent(parent, true);
    }

    /// Altura del piso (Floor_Modular) en x,z; si no lo encuentra, fallback.
    static float GroundY(float x, float z, float fallback)
    {
        Physics.SyncTransforms();
        Vector3 from = new Vector3(x, fallback + 3f, z);
        float best = float.NegativeInfinity;

        foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, 8f,
                     Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            bool isFloor = false;
            for (Transform t = hit.collider.transform; t != null; t = t.parent)
                if (t.name.StartsWith("Floor_Modular")) { isFloor = true; break; }
            if (!isFloor) continue;
            if (hit.point.y > best) best = hit.point.y;
        }

        return float.IsNegativeInfinity(best) ? fallback : best;
    }

    static Bounds Bounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.5f);
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    static GameObject PropRotated(string prefabName, Quaternion extra, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + prefabName + ".prefab");
        if (prefab == null)
        {
            Debug.LogWarning("Tutorial: falta el prefab " + prefabName);
            return null;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, tutorial);
        go.transform.rotation = extra * prefab.transform.rotation;
        foreach (Collider c in go.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(c);
        if (parent) go.transform.SetParent(parent, true);
        return go;
    }

    /// Coloca un prefab apoyado en el suelo (bottomCenter) con la altura pedida.
    static GameObject Prop(string prefabName, Vector3 bottomCenter, float yaw,
        float height, Transform parent, bool solid = false)
    {
        GameObject go = PropRotated(prefabName, Quaternion.Euler(0f, yaw, 0f), parent);
        if (go == null) return null;

        Bounds b = Bounds(go);
        go.transform.localScale *= height / Mathf.Max(0.001f, b.size.y);
        b = Bounds(go);
        // Apoyado sobre el piso real (no a la altura aproximada).
        bottomCenter.y = GroundY(bottomCenter.x, bottomCenter.z, bottomCenter.y);
        go.transform.position += bottomCenter - new Vector3(b.center.x, b.min.y, b.center.z);

        if (solid)
        {
            b = Bounds(go);
            Solid("Colision_" + prefabName, b.center, b.size, parent, true);
        }
        return go;
    }

    /// Caja de colisión invisible (alineada a los ejes).
    static GameObject Solid(string name, Vector3 center, Vector3 size, Transform parent, bool ignoreCamera)
    {
        GameObject go = new GameObject(name);
        if (go.scene != tutorial) SceneManager.MoveGameObjectToScene(go, tutorial);
        go.transform.position = center;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = size;
        // La cámara también choca con los obstáculos: nunca los atraviesa.
        if (parent) go.transform.SetParent(parent, true);
        return go;
    }
}
