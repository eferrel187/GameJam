using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Prepara la distribucion de test-integracion-diseno sin reconstruir niveles anteriores.</summary>
public static class LevelFourSetup
{
    const string SourcePath = "Assets/00_Scenes/Nivel1.unity";
    const string Level4Path = "Assets/00_Scenes/Nivel4.unity";
    const string Level5Path = "Assets/00_Scenes/Nivel5.unity";
    const string GeneratedName = "Nivel4_Jugable";
    const string LandingName = "Nivel5_EntradaJugable";
    const string Prefabs = "Assets/04_Prefabs/";
    static readonly List<string> report = new List<string>();

    [MenuItem("Juego/Preparar Nivel4")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string[] required =
        {
            SourcePath, Level4Path, Level5Path, Prefabs + "Doors_GothicArch_Covered.prefab",
            Prefabs + "MonsterHand.prefab", Prefabs + "MonsterHand/Oscuridad.mat",
            "Assets/05_models/torture_device_011.fbx", "Assets/Resources/LevelVisualAssets.asset",
            "Assets/Resources/SoundLibrary.asset"
        };
        foreach (string path in required)
            if (!File.Exists(path)) throw new InvalidOperationException("Nivel4: falta " + path);

        report.Clear();
        string backup = "Backups_Nivel4/" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fffffff");
        foreach (string path in new[] { Level4Path, Level5Path, "ProjectSettings/EditorBuildSettings.asset" })
        {
            string destination = Path.Combine(backup, path);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(path, destination, false);
        }

        Scene source = default;
        EditorBuildSettingsScene[] originalOrder = EditorBuildSettings.scenes;
        bool closeSource = !SceneManager.GetSceneByPath(SourcePath).isLoaded;
        bool success = false;
        try
        {
            source = Open(SourcePath);
            GameObject sourcePlayer = All(source).FirstOrDefault(t => t.name == "Player")?.gameObject;
            Camera sourceCamera = All(source).Select(t => t.GetComponent<Camera>()).FirstOrDefault(c => c && c.GetComponent<CameraIntroFollow>());
            if (!sourcePlayer || !sourceCamera) throw new InvalidOperationException("No encontre Player y la camara jugable en Nivel1.");
            GameObject sourceCage = All(source).FirstOrDefault(t => t.name == "torture_device_01")?.gameObject;

            LevelVisualAssetsSetup.UpdateResources();
            Scene scene = Open(Level4Path);
            SceneManager.SetActiveScene(scene);
            PrepareLevel4(scene, sourcePlayer, sourceCamera.gameObject, sourceCage);
            Scene landing = Open(Level5Path);
            PrepareLanding(landing, sourcePlayer, sourceCamera.gameObject);
            AddBuildOrder();
            Validate(scene);
            if (!EditorSceneManager.SaveScene(scene) || !EditorSceneManager.SaveScene(landing))
                throw new IOException("Unity no pudo guardar las escenas preparadas.");
            AssetDatabase.SaveAssets();
            // Deja solo Nivel4 cargado: probar dos escenas juntas duplicaria jugador y camara.
            scene = EditorSceneManager.OpenScene(Level4Path, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            Selection.activeGameObject = scene.GetRootGameObjects().First(g => g.name == GeneratedName);
            report.Add("Respaldo de escenas y Build Settings: " + backup);
            File.WriteAllLines(Path.Combine(backup, "resultado.txt"), report);
            success = true;
            Debug.Log("Nivel4 preparado. Pulsa Play en Nivel4.\n" + string.Join("\n", report));
        }
        catch (Exception error)
        {
            Open(SourcePath); // Mantiene una escena cargada mientras restaura las otras.
            foreach (string path in new[] { Level4Path, Level5Path })
            {
                Scene opened = SceneManager.GetSceneByPath(path);
                if (opened.IsValid() && opened.isLoaded) EditorSceneManager.CloseScene(opened, true);
                File.Copy(Path.Combine(backup, path), path, true);
            }
            EditorBuildSettings.scenes = originalOrder;
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(Level4Path, OpenSceneMode.Single);
            Debug.LogError("No se completo Nivel4: " + error.Message + "\nOriginales en " + backup);
            throw;
        }
        finally
        {
            if (closeSource && source.IsValid() && source.isLoaded) EditorSceneManager.CloseScene(source, true);
            if (!success) Debug.LogWarning("Conserva el respaldo indicado antes de repetir el montaje.");
        }
    }

    static Scene Open(string path)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        return scene.IsValid() && scene.isLoaded ? scene : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    }

    static Transform[] All(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();

    static void PrepareLevel4(Scene scene, GameObject sourcePlayer, GameObject sourceCamera, GameObject sourceCage)
    {
        Transform[] original = All(scene);
        Transform oldGroup = original.FirstOrDefault(t => t.name == GeneratedName);
        Level4Rules previous = oldGroup ? oldGroup.GetComponent<Level4Rules>() : null;
        GameObject[] keys = original.Where(t => (!oldGroup || !t.IsChildOf(oldGroup)) &&
                (t.name.StartsWith("Key4", StringComparison.OrdinalIgnoreCase) || t.name == "Llave_Falsa_N4"))
            .Select(t => t.gameObject).OrderBy(g => g.transform.position.z).ToArray();
        GameObject realKey = previous && previous.realKey ? previous.realKey : keys.LastOrDefault();
        GameObject fakeKey = previous && previous.fakeKey ? previous.fakeKey : keys.FirstOrDefault();
        if (!realKey || !fakeKey || realKey == fakeKey)
            throw new InvalidOperationException("Nivel4 necesita las dos llaves de las celdas derechas.");
        Transform portal = original.Where(t => t.name == "Portal_de_paso").OrderByDescending(t => t.position.z).FirstOrDefault();
        if (!portal || portal.position.z < 30f) throw new InvalidOperationException("No encontre el portal de salida de Nivel4.");
        if (oldGroup) Object.DestroyImmediate(oldGroup.gameObject);
        GameObject generated = New(scene, GeneratedName);
        Level4Rules rules = generated.AddComponent<Level4Rules>();

        float ground = GroundAt(scene, 0f, 2f);
        Transform entry = All(scene).FirstOrDefault(t => t.name == "ENTRADA_desde_Nivel3");
        Vector3 spawn = (entry ? entry.position : Vector3.zero) + Vector3.forward * 1.8f;
        spawn.y = ground;
        GameObject player = CopyPlayerAndCamera(scene, generated.transform, sourcePlayer, sourceCamera, spawn);
        rules.player = player.transform;

        // Las dos llaves tienen la misma aparicion; la izquierda de las dos celdas es la verdadera.
        Quaternion keyRotation = fakeKey.transform.rotation;
        PrepareKey(realKey, "Key4", new Vector3(20.1f, GroundAt(scene, 20f, 16f) + 1.15f, 16.2f), keyRotation, false);
        PrepareKey(fakeKey, "Llave_Falsa_N4", new Vector3(20.1f, GroundAt(scene, 20f, 10f) + 1.15f, 9.9f), keyRotation, true);
        rules.realKey = realKey;
        rules.fakeKey = fakeKey;

        Transform threshold = Child(generated.transform, "Umbral_Salida_Celda_Falsa");
        threshold.SetPositionAndRotation(new Vector3(17.35f, ground, 10f), Quaternion.LookRotation(Vector3.left));
        rules.fakeCellExit = threshold;

        // Se preservan sus posiciones en la escena. Solo las puas de la celda falsa dependen de la llave.
        Transform[] spikes = All(scene).Where(t => t.name.StartsWith("Pinchos_suelo_estaticos") && t.GetComponentInChildren<MeshFilter>()).ToArray();
        Transform falseTrap = spikes.OrderBy(t => (new Vector2(t.position.x, t.position.z) - new Vector2(16.373f, 10.075f)).sqrMagnitude).FirstOrDefault();
        if (spikes.Length < 2 || !falseTrap) throw new InvalidOperationException("Faltan los pinchos del suelo en Nivel4.");
        var trapList = new List<RisingSpikes>();
        foreach (Transform spike in spikes)
        {
            // Una instancia venia tumbada de lado: pone las puntas hacia arriba.
            if (Mathf.Abs(Quaternion.Dot(spike.rotation, Quaternion.identity)) > 0.99f)
                spike.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Bounds b = Measure(spike.gameObject);
            spike.position += Vector3.up * (GroundAt(scene, b.center.x, b.center.z) + 0.005f - b.min.y);
            foreach (Collider collider in spike.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            RisingSpikes trap = spike.GetComponent<RisingSpikes>() ?? spike.gameObject.AddComponent<RisingSpikes>();
            trap.activateOnStep = spike != falseTrap;
            trap.warningSeconds = spike == falseTrap ? 0.01f : 0.12f;
            trap.riseSeconds = spike == falseTrap ? 0.1f : 0.16f;
            trap.raisedSeconds = spike == falseTrap ? 3.5f : 1.5f;
            trapList.Add(trap);
        }
        rules.floorSpikes = trapList.ToArray();
        rules.fakeKeySpikes = new[] { falseTrap.GetComponent<RisingSpikes>() };

        int bears = 0;
        foreach (Transform trap in All(scene).Where(t => t.name.StartsWith("Trampa_oso_estatica")))
        {
            KillZone zone = trap.GetComponent<KillZone>() ?? trap.gameObject.AddComponent<KillZone>();
            zone.bearTrap = zone.armed = true;
            zone.inset = 0.06f;
            foreach (Collider collider in trap.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            bears++;
        }
        foreach (Transform trap in All(scene).Where(t => t.name.StartsWith("Pinchos_techo_estaticos")))
        {
            KillZone zone = trap.GetComponent<KillZone>() ?? trap.gameObject.AddComponent<KillZone>();
            zone.bearTrap = false;
            zone.armed = true;
        }

        Transform rollerTransform = All(scene).FirstOrDefault(t => t.name == "Rodillo_estatico");
        if (!rollerTransform) throw new InvalidOperationException("No encontre Rodillo_estatico.");
        HazardCylinder roller = rollerTransform.GetComponent<HazardCylinder>() ?? rollerTransform.gameObject.AddComponent<HazardCylinder>();
        roller.MeasureShape();
        Vector3 center = roller.Center;
        float rollerGround = GroundAt(scene, center.x, center.z);
        float radius = Mathf.Max(0.35f, roller.Radius / 0.92f);
        float high = Mathf.Min(rollerGround + 3.35f, CeilingAt(scene, center.x, center.z) - radius - 0.08f);
        float low = rollerGround + radius * 0.85f + 0.02f;
        high = Mathf.Max(high, low + 1f);
        roller.SetRestCenter(new Vector3(center.x, high, center.z));
        roller.moveAxis = Vector3.up;
        roller.minOffset = low - high;
        roller.maxOffset = 0f;
        roller.moveSpeed = 1.15f;
        roller.spinSpeed = 210f;
        roller.startDirection = -1f;
        roller.playSound = true;
        roller.spinOnlyWhenActive = false;
        foreach (Collider collider in roller.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        rules.roller = roller;

        BuildCatRoom(scene, generated.transform, portal, sourceCage, ground, rules);
        PrepareTorchSounds(scene);
        BakeEnvironmentColliders(scene);
        report.Add("Nivel4: " + trapList.Count + " sectores de puas moviles, " + bears + " cepos y rodillo vertical.");
        report.Add("Llave real: celda izquierda z=16.2. Llave falsa: celda derecha z=9.9; se activa al salir.");
        EditorSceneManager.MarkSceneDirty(scene);
    }

    static GameObject CopyPlayerAndCamera(Scene scene, Transform parent, GameObject sourcePlayer, GameObject sourceCamera, Vector3 feet)
    {
        foreach (Camera camera in All(scene).Select(t => t.GetComponent<Camera>()).Where(c => c)) camera.enabled = false;
        foreach (AudioListener listener in All(scene).Select(t => t.GetComponent<AudioListener>()).Where(l => l)) listener.enabled = false;

        GameObject player = Object.Instantiate(sourcePlayer, parent);
        player.name = "Player";
        player.transform.localScale = sourcePlayer.transform.lossyScale;
        player.transform.SetPositionAndRotation(feet, Quaternion.identity);
        CharacterController cc = player.GetComponent<CharacterController>();
        if (!cc || !player.GetComponent<GuardPlayerMovement>() || !player.GetComponent<GuardHealth>())
            throw new InvalidOperationException("El Player de Nivel1 no tiene movimiento, CharacterController y vidas.");
        float bottom = player.transform.TransformPoint(cc.center).y - cc.height * Mathf.Abs(player.transform.lossyScale.y) * 0.5f;
        player.transform.position += Vector3.up * (feet.y - bottom + 0.03f);
        Transform spawn = Child(parent, "Inicio_Jugador");
        spawn.SetPositionAndRotation(player.transform.position, player.transform.rotation);
        player.GetComponent<GuardHealth>().respawnPoint = spawn;

        GameObject cameraObject = Object.Instantiate(sourceCamera, parent);
        cameraObject.name = "Main Camera";
        cameraObject.tag = "MainCamera";
        cameraObject.transform.localScale = Vector3.one;
        cameraObject.transform.position = player.transform.position + new Vector3(0f, 1.8f, -3.2f);
        cameraObject.transform.rotation = Quaternion.LookRotation(player.transform.position + Vector3.up * 1.1f - cameraObject.transform.position);
        Camera cameraComponent = cameraObject.GetComponent<Camera>();
        cameraComponent.enabled = true;
        cameraComponent.nearClipPlane = 0.04f;
        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.backgroundColor = Color.black;
        AudioListener listenerComponent = cameraObject.GetComponent<AudioListener>() ?? cameraObject.AddComponent<AudioListener>();
        listenerComponent.enabled = true;
        CameraIntroFollow follow = cameraObject.GetComponent<CameraIntroFollow>();
        var cameraData = new SerializedObject(follow);
        cameraData.FindProperty("player").objectReferenceValue = player.transform;
        cameraData.FindProperty("routePoints").arraySize = 0;
        cameraData.FindProperty("distance").floatValue = 3.2f;
        cameraData.FindProperty("cameraHeight").floatValue = 1.8f;
        cameraData.ApplyModifiedPropertiesWithoutUndo();
        var movementData = new SerializedObject(player.GetComponent<GuardPlayerMovement>());
        movementData.FindProperty("cameraTransform").objectReferenceValue = cameraObject.transform;
        movementData.ApplyModifiedPropertiesWithoutUndo();
        if (!cameraObject.GetComponent<GuardCameraCollision>()) cameraObject.AddComponent<GuardCameraCollision>();
        player.SetActive(true);
        cameraObject.SetActive(true);
        return player;
    }

    static void PrepareKey(GameObject key, string name, Vector3 center, Quaternion rotation, bool fake)
    {
        key.name = name;
        key.SetActive(true);
        key.transform.rotation = rotation;
        Bounds bounds = Measure(key);
        float span = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (span > 0.000001f) key.transform.localScale *= 0.55f / span;
        bounds = Measure(key);
        key.transform.position += center - bounds.center;
        foreach (Renderer renderer in key.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
        foreach (Collider collider in key.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        FakeKey component = key.GetComponent<FakeKey>();
        if (fake && !component) key.AddComponent<FakeKey>();
        if (!fake && component) Object.DestroyImmediate(component);
        if (!key.GetComponent<KeyFloat>()) key.AddComponent<KeyFloat>();
    }

    static void BuildCatRoom(Scene scene, Transform parent, Transform portal, GameObject sourceCage, float ground, Level4Rules rules)
    {
        Transform room = Child(parent, "Sala_del_Gato");
        float firstZ = portal.position.z;
        float lastZ = firstZ + 7.2f;
        float centerX = 0f;
        GameObject floorSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/05_models/Floor_Modular.fbx");
        GameObject wallSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/05_models/Wall.fbx");
        FittedPiece(scene, room, floorSource, "Suelo_Umbral", new Vector3(centerX, ground - 0.12f, firstZ - 0.08f),
            new Vector3(6.02f, 0.24f, 0.24f), Quaternion.Euler(-90f, 0f, 0f));
        for (int x = -1; x <= 1; x++)
            for (int z = 0; z < 5; z++)
                FittedPiece(scene, room, floorSource, "Suelo_Sala", new Vector3(centerX + x * 2f, ground - 0.12f, firstZ + 0.9f + z * 1.8f), new Vector3(2.02f, 0.24f, 1.82f), Quaternion.Euler(-90f, 0f, 0f));
        for (int z = 0; z < 4; z++)
        {
            float midZ = firstZ + 0.9f + z * 1.8f;
            for (int side = -1; side <= 1; side += 2)
                FittedPiece(scene, room, wallSource, "Muro_Sala", new Vector3(centerX + side * 3.05f, ground + 2.1f, midZ), new Vector3(0.32f, 4.2f, 1.82f), Quaternion.Euler(-90f, 90f, 0f));
        }
        for (int x = -1; x <= 1; x++)
            for (int z = 0; z < 4; z++)
                FittedPiece(scene, room, floorSource, "Techo_Sala", new Vector3(centerX + x * 2f, ground + 4.3f, firstZ + 0.9f + z * 1.8f), new Vector3(2.02f, 0.2f, 1.82f), Quaternion.Euler(-90f, 0f, 0f));

        GameObject rearPortal = Object.Instantiate(portal.gameObject, room);
        rearPortal.name = "Portal_Oscuro_N4";
        rearPortal.transform.position += Vector3.forward * (lastZ - firstZ);
        foreach (MeshFilter mesh in rearPortal.GetComponentsInChildren<MeshFilter>())
        {
            if (!mesh.sharedMesh || mesh.GetComponent<Collider>()) continue;
            mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
        }
        // Replica el marco que ya estrecha la primera abertura al ancho de la hoja.
        Transform[] frames = All(scene).Where(t => !t.IsChildOf(parent) &&
            (t.name.StartsWith("Support_") || t.name.StartsWith("Wall_Half")) &&
            Mathf.Abs(t.position.z - firstZ) < 1f).ToArray();
        foreach (Transform frame in frames)
        {
            GameObject copy = Object.Instantiate(frame.gameObject, room);
            copy.name = frame.name + "_MarcoOscuro_N4";
            copy.transform.position += Vector3.forward * (lastZ - firstZ);
        }
        ArchDoor locked = BuildDoor(scene, room, "Puerta_Llave", firstZ, ground);
        ArchDoor dark = BuildDoor(scene, room, "Puerta_Oscura_N4", lastZ, ground);
        rules.exitDoor = locked;

        GameObject darkness = GameObject.CreatePrimitive(PrimitiveType.Cube);
        darkness.name = "Oscuridad_Nivel4";
        darkness.transform.SetParent(room, false);
        darkness.transform.position = new Vector3(0f, ground + 2.2f, lastZ + 2.5f);
        darkness.transform.localScale = new Vector3(6.5f, 4.6f, 4f);
        darkness.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Prefabs + "MonsterHand/Oscuridad.mat");
        Object.DestroyImmediate(darkness.GetComponent<Collider>());

        GameObject cage = sourceCage ? Object.Instantiate(sourceCage, room) :
            (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/05_models/torture_device_011.fbx"), scene);
        cage.transform.SetParent(room, true);
        cage.name = "torture_device_01_Nivel4";
        cage.SetActive(true);
        if (sourceCage) cage.transform.localScale = sourceCage.transform.lossyScale;
        else
        {
            Bounds original = Measure(cage);
            cage.transform.localScale *= 1.55f / Mathf.Max(0.001f, original.size.y);
        }
        Bounds cageBounds = Measure(cage);
        cage.transform.position += new Vector3(0f, ground, lastZ - 1.1f) -
            new Vector3(cageBounds.center.x, cageBounds.min.y, cageBounds.center.z);
        LevelVisualAssets.EnsureCat(cage, Vector3.back);
        foreach (MeshFilter mesh in cage.GetComponentsInChildren<MeshFilter>()) AddBox(mesh);

        GameObject sequenceObject = Child(room, "Secuencia_Gato_Nivel4").gameObject;
        KidnapSequence sequence = sequenceObject.AddComponent<KidnapSequence>();
        sequence.firstDoor = locked;
        sequence.darkDoor = dark;
        sequence.targetOverride = cage.transform;
        sequence.targetName = cage.name;
        sequence.triggerDistance = 3.15f;
        sequence.exitCenter = new Vector3(0f, ground + 1.2f, lastZ + 0.35f);
        sequence.exitSize = new Vector3(2.65f, 20f, 0.5f);
        GameObject hand = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "MonsterHand.prefab"), scene);
        hand.name = "ManoMonstruo_Nivel4";
        hand.transform.SetParent(sequenceObject.transform, false);
        hand.transform.localScale = Vector3.one * 1.6f;
        sequence.hand = hand.transform;
        rules.catSequence = sequence;

        Transform litTorch = All(scene).FirstOrDefault(t => !t.IsChildOf(parent) && t.name.StartsWith("Torch") && t.GetComponentInChildren<TorchFlicker>());
        if (litTorch)
        {
            foreach (Vector3 position in new[] { new Vector3(-2.78f, ground + 2f, firstZ + 2.3f), new Vector3(2.78f, ground + 2f, firstZ + 5.2f) })
            {
                GameObject torch = Object.Instantiate(litTorch.gameObject, room);
                torch.name = "Torch_Sala_N4";
                Bounds tb = Measure(torch);
                torch.transform.position += position - tb.center;
                foreach (Light light in torch.GetComponentsInChildren<Light>()) light.shadows = LightShadows.Soft;
            }
        }
        report.Add("Salida con llave en z=" + firstZ.ToString("0.00") + ", sala del gato, mano y puerta oscura hacia Nivel5.");
    }

    static ArchDoor BuildDoor(Scene scene, Transform parent, string name, float z, float ground)
    {
        Transform group = Child(parent, name);
        GameObject leaf = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Doors_GothicArch_Covered.prefab"), scene);
        leaf.name = "Hoja_" + name;
        leaf.transform.SetParent(group, false);
        leaf.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        leaf.transform.localScale = Vector3.one * 105f;
        Bounds b = Measure(leaf);
        leaf.transform.position += new Vector3(0f, ground + 0.01f, z) - new Vector3(b.center.x, b.min.y, b.center.z);
        foreach (MeshFilter mesh in leaf.GetComponentsInChildren<MeshFilter>()) AddBox(mesh);
        ArchDoor door = group.gameObject.AddComponent<ArchDoor>();
        door.leftLeaf = leaf.transform;
        door.openTowards = Vector3.forward;
        door.openSeconds = 1.3f;
        return door;
    }

    static void PrepareLanding(Scene scene, GameObject sourcePlayer, GameObject sourceCamera)
    {
        SceneManager.SetActiveScene(scene);
        Transform existingPlayer = All(scene).FirstOrDefault(t => t.name == "Player");
        Transform old = All(scene).FirstOrDefault(t => t.name == LandingName);
        if (existingPlayer && (!old || !existingPlayer.IsChildOf(old)))
        {
            BakeEnvironmentColliders(scene);
            report.Add("Nivel5 conserva su Player existente.");
            EditorSceneManager.MarkSceneDirty(scene);
            return;
        }
        if (old) Object.DestroyImmediate(old.gameObject);
        Transform entry = All(scene).FirstOrDefault(t => t.name == "ENTRADA_desde_Nivel4");
        Vector3 spawn = (entry ? entry.position : Vector3.zero) + Vector3.forward * 1.8f;
        spawn.y = GroundAt(scene, spawn.x, spawn.z);
        GameObject group = New(scene, LandingName);
        CopyPlayerAndCamera(scene, group.transform, sourcePlayer, sourceCamera, spawn);
        BakeEnvironmentColliders(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        report.Add("Nivel5: llegada con personaje, camara, vidas y colisiones; conserva el diseno del mapa.");
    }

    static GameObject FittedPiece(Scene scene, Transform parent, GameObject asset, string name, Vector3 center, Vector3 size, Quaternion rotation)
    {
        if (!asset) throw new InvalidOperationException("Falta un modelo de la sala.");
        Transform wrapper = Child(parent, name);
        GameObject piece = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        piece.transform.SetParent(wrapper, false);
        piece.transform.rotation = rotation;
        Bounds b = Measure(piece);
        wrapper.localScale = new Vector3(size.x / Mathf.Max(0.00001f, b.size.x), size.y / Mathf.Max(0.00001f, b.size.y), size.z / Mathf.Max(0.00001f, b.size.z));
        b = Measure(piece);
        wrapper.position += center - b.center;
        foreach (MeshFilter mesh in piece.GetComponentsInChildren<MeshFilter>())
        {
            MeshCollider collider = mesh.GetComponent<MeshCollider>() ?? mesh.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh.sharedMesh;
        }
        return wrapper.gameObject;
    }

    static void BakeEnvironmentColliders(Scene scene)
    {
        int count = 0;
        foreach (MeshFilter mesh in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true)))
        {
            if (!mesh.sharedMesh || !mesh.gameObject.activeInHierarchy) continue;
            if (mesh.GetComponentInParent<GuardPlayerMovement>() || mesh.GetComponentInParent<RisingSpikes>() ||
                mesh.GetComponentInParent<KillZone>() || mesh.GetComponentInParent<HazardCylinder>() ||
                mesh.GetComponentInParent<ArchDoor>() || mesh.GetComponentInParent<KidnapSequence>()) continue;
            bool environment = false;
            for (Transform t = mesh.transform; t; t = t.parent)
                if (t.name.StartsWith("01_Suelos") || t.name.StartsWith("02_Muros") || t.name.StartsWith("03_Techos") ||
                    t.name.StartsWith("Wall") || t.name.StartsWith("Support") || t.name.StartsWith("Doors_GothicArch") ||
                    t.name.StartsWith("Window_") || t.name.StartsWith("Column_") ||
                    t.name == "Trampilla_estatica" || t.name == "Soporte_rodillo") environment = true;
            if (!environment || mesh.GetComponents<Collider>().Any(c => c.enabled && !c.isTrigger)) continue;
            MeshCollider collider = mesh.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh.sharedMesh;
            collider.convex = false;
            count++;
        }
        report.Add(scene.name + ": " + count + " colisiones nuevas, con huecos reales en arcos y celdas.");
    }

    static void AddBox(MeshFilter mesh)
    {
        if (!mesh.sharedMesh || mesh.GetComponents<Collider>().Any(c => c.enabled && !c.isTrigger)) return;
        BoxCollider collider = mesh.gameObject.AddComponent<BoxCollider>();
        collider.center = mesh.sharedMesh.bounds.center;
        collider.size = mesh.sharedMesh.bounds.size;
    }

    static void PrepareTorchSounds(Scene scene)
    {
        // Incluye las luces del diseno nuevo en el sonido 3D y el presupuesto de sombras.
        foreach (Light light in All(scene).Select(t => t.GetComponent<Light>()).Where(l => l && l.name == "LuzAntorcha"))
        {
            if (light.GetComponent<TorchFlicker>()) continue;
            TorchFlicker flicker = light.gameObject.AddComponent<TorchFlicker>();
            flicker.baseIntensity = light.intensity;
            flicker.baseRange = light.range;
            flicker.flicker = 0.12f;
            flicker.speed = 3f;
            flicker.jitter = 0f;
        }
    }

    static float GroundAt(Scene scene, float x, float z)
    {
        Transform floor = All(scene).FirstOrDefault(t => t.name.StartsWith("01_Suelos"));
        if (!floor) throw new InvalidOperationException(scene.name + ": no encontre el grupo de suelos.");
        float result = float.NegativeInfinity;
        foreach (Renderer renderer in floor.GetComponentsInChildren<Renderer>())
        {
            Bounds b = renderer.bounds;
            if (x >= b.min.x - 0.02f && x <= b.max.x + 0.02f && z >= b.min.z - 0.02f && z <= b.max.z + 0.02f)
                result = Mathf.Max(result, b.max.y);
        }
        return float.IsNegativeInfinity(result) ? 0f : result;
    }

    static float CeilingAt(Scene scene, float x, float z)
    {
        Transform ceiling = All(scene).FirstOrDefault(t => t.name.StartsWith("03_Techos"));
        if (ceiling)
            foreach (Renderer renderer in ceiling.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = renderer.bounds;
                if (x >= b.min.x && x <= b.max.x && z >= b.min.z && z <= b.max.z) return b.min.y;
            }
        return GroundAt(scene, x, z) + 4.2f;
    }

    static Bounds Measure(GameObject gameObject) => LevelVisualAssets.Measure(gameObject);

    static GameObject New(Scene scene, string name)
    {
        GameObject gameObject = new GameObject(name);
        SceneManager.MoveGameObjectToScene(gameObject, scene);
        return gameObject;
    }

    static Transform Child(Transform parent, string name)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        return gameObject.transform;
    }

    static void AddBuildOrder()
    {
        string[] order = Enumerable.Range(1, 7).Select(n => "Assets/00_Scenes/Nivel" + n + ".unity")
            .Prepend("Assets/00_Scenes/Tutorial.unity").Where(File.Exists).ToArray();
        var scenes = order.Select(path => new EditorBuildSettingsScene(path, true)).ToList();
        scenes.AddRange(EditorBuildSettings.scenes.Where(s => !order.Contains(s.path)));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static void Validate(Scene scene)
    {
        Transform[] all = All(scene);
        Level4Rules rules = all.Select(t => t.GetComponent<Level4Rules>()).FirstOrDefault(c => c);
        if (!rules || !rules.player || !rules.realKey || !rules.fakeKey || !rules.exitDoor || !rules.catSequence || !rules.roller)
            throw new InvalidOperationException("Falta una referencia de Nivel4.");
        if (!rules.catSequence.targetOverride || !rules.catSequence.hand || !rules.catSequence.darkDoor)
            throw new InvalidOperationException("La secuencia del gato esta incompleta.");
        if (all.Count(t => t.name == "Player") != 1 || all.Select(t => t.GetComponent<Camera>()).Count(c => c && c.enabled) != 1)
            throw new InvalidOperationException("Debe haber un solo Player y una sola camara activa.");
        LevelVisualAssets assets = LevelVisualAssets.Load();
        if (!assets || !assets.closedBearTrap || !assets.catModel)
            throw new InvalidOperationException("Faltan los recursos del gato o del cepo cerrado.");
        if (EditorSceneManager.DetectCrossSceneReferences(scene)) throw new InvalidOperationException("Se detectaron referencias a otra escena.");
        report.Add("Validacion: referencias locales, una camara, un Player y recursos de gato/cepo preparados.");
    }
}
