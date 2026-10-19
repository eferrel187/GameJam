using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Juego > Preparar historia (niveles 4-6):
///  1. Orden del Build: Tutorial, Nivel1 ... Nivel6 (sin duplicados).
///  2. Resources/StoryAssets con los prefabs y sonidos de la historia.
///  3. Resources/JugadorHistoria: copia del personaje y la cámara del Nivel4
///     (los niveles 5 y 6 lo colocan solos al empezar).
///  4. Animaciones del jugador con Walking.fbx para las escenas.
/// No modifica las escenas: todo lo demás se monta al jugar.
/// </summary>
public static class StorySetup
{
    const string AssetsPath = "Assets/Resources/StoryAssets.asset";
    const string RigPath = "Assets/Resources/JugadorHistoria.prefab";
    const string Prefabs = "Assets/04_Prefabs/";
    const string Sounds = "Assets/07_sonido/historia/";

    [MenuItem("Juego/Preparar historia (niveles 4-6)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Sal del modo Play antes de preparar la historia.");
            return;
        }
        var report = new List<string>();
        FixBuildOrder(report);
        StoryAssets assets = FillAssets(report);
        BuildPlayerRig(assets, report);
        if (File.Exists("Assets/03_Animations/Walking.fbx"))
        {
            PlayerAnimationSetup.Setup();
            report.Add("Animaciones del jugador: Walking.fbx como 'Caminar_Escena'.");
        }
        AssetDatabase.SaveAssets();
        string text = "HISTORIA PREPARADA\n" + string.Join("\n", report);
        Directory.CreateDirectory("Diag");
        File.WriteAllText("Diag/historia_preparada.txt", text);
        Debug.Log(text);
    }

    static void FixBuildOrder(List<string> report)
    {
        string[] order = { "Tutorial", "Nivel1", "Nivel2", "Nivel3", "Nivel4", "Nivel5", "Nivel6" };
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (string n in order)
        {
            string path = "Assets/00_Scenes/" + n + ".unity";
            if (!File.Exists(path)) { report.Add("FALTA la escena " + path); continue; }
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        report.Add("Build: " + string.Join(" → ", scenes.Select(s => Path.GetFileNameWithoutExtension(s.path))));
    }

    static T Load<T>(string path) where T : Object
    {
        T a = AssetDatabase.LoadAssetAtPath<T>(path);
        return a;
    }

    static StoryAssets FillAssets(List<string> report)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        StoryAssets a = Load<StoryAssets>(AssetsPath);
        if (!a)
        {
            a = ScriptableObject.CreateInstance<StoryAssets>();
            AssetDatabase.CreateAsset(a, AssetsPath);
        }
        a.doorLeft = Load<GameObject>(Prefabs + "Doors_RoundArch_L.prefab");
        a.doorRight = Load<GameObject>(Prefabs + "Doors_RoundArch_R.prefab");
        a.doorFrame = Load<GameObject>(Prefabs + "Support_Right.prefab");
        a.skull = Load<GameObject>(Prefabs + "Skull.prefab");
        a.brokenPots = new[] { "Pot1_Broken", "Pot2_Broken", "Pot3_Broken" }
            .Select(n => Load<GameObject>(Prefabs + n + ".prefab")).Where(g => g).ToArray();
        a.bookcaseFull = Load<GameObject>(Prefabs + "Bookcase_Full.prefab");
        a.bookcaseEmpty = Load<GameObject>(Prefabs + "Bookcase_Empty.prefab");
        a.key = Load<GameObject>("Assets/05_models/Key4.fbx");
        a.monsterHand = Load<GameObject>(Prefabs + "MonsterHand.prefab");
        a.hazardCylinder = Load<GameObject>("Assets/05_models/Hazard_Cylinder.fbx");
        a.spikesPlatform = Load<GameObject>("Assets/05_models/Spikes_Platform.fbx");
        a.trees = new[] { "Tree_1", "Tree_2", "Tree_3" }.Select(n => Load<GameObject>(Prefabs + n + ".prefab")).Where(g => g).ToArray();
        a.bushes = new[] { "Bush_1x1", "Bush_2x1", "Bush_Round", "Bush_Large" }.Select(n => Load<GameObject>(Prefabs + n + ".prefab")).Where(g => g).ToArray();
        a.grass = Load<GameObject>(Prefabs + "Grass.prefab");
        a.candles = Load<GameObject>(Prefabs + "Candles_1.prefab");
        a.wood = Load<Material>("Assets/06_textura/Material_wall/Wood.mat");
        a.darkWood = Load<Material>("Assets/06_textura/Material_wall/DarkWood.mat");
        a.darkness = Load<Material>(Prefabs + "MonsterHand/Oscuridad.mat");

        a.maullido = Load<AudioClip>(Sounds + "maullido.wav");
        a.maullidoSuave = Load<AudioClip>(Sounds + "maullido_suave.wav");
        a.maullidoAsustado = Load<AudioClip>(Sounds + "maullido_asustado.wav");
        a.ronroneo = Load<AudioClip>(Sounds + "ronroneo.wav");
        a.musicaBaile = Load<AudioClip>(Sounds + "musica_baile.wav");
        a.ambienteCampo = Load<AudioClip>(Sounds + "ambiente_campo.wav");
        a.musicaTranquila = Load<AudioClip>(Sounds + "musica_tranquila.wav");
        a.latido = Load<AudioClip>(Sounds + "latido.wav");
        a.zumbidoMano = Load<AudioClip>(Sounds + "zumbido_mano.wav");
        a.estatuaGira = Load<AudioClip>(Sounds + "estatua_gira.wav");
        a.acierto = Load<AudioClip>(Sounds + "acierto.wav");
        a.papel = Load<AudioClip>(Sounds + "papel.wav");
        a.crujido = Load<AudioClip>(Sounds + "crujido.wav");
        a.rotor = Load<AudioClip>(Sounds + "rotor.wav");
        a.jaulaRompe = Load<AudioClip>(Sounds + "jaula_rompe.wav");
        a.serpienteSiseo = Load<AudioClip>(Sounds + "serpiente_siseo.wav");
        a.serpienteMordida = Load<AudioClip>(Sounds + "serpiente_mordida.wav");
        a.tic = Load<AudioClip>(Sounds + "tic.wav");
        a.libros = Load<AudioClip>(Sounds + "libros.wav");
        a.golpePuerta = Load<AudioClip>(Sounds + "golpe_puerta.wav");

        // Música y ambiente largos: en streaming para no cargar todo en memoria.
        foreach (string loop in new[] { "musica_baile", "ambiente_campo", "musica_tranquila" })
        {
            AudioImporter imp = AssetImporter.GetAtPath(Sounds + loop + ".wav") as AudioImporter;
            if (!imp) continue;
            AudioImporterSampleSettings s = imp.defaultSampleSettings;
            if (s.loadType != AudioClipLoadType.Streaming)
            {
                s.loadType = AudioClipLoadType.Streaming;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
        }

        EditorUtility.SetDirty(a);
        var missing = new List<string>();
        foreach (var f in typeof(StoryAssets).GetFields())
        {
            object v = f.GetValue(a);
            if (v == null || (v is Object o && !o) || (v is System.Array arr && arr.Length == 0)) missing.Add(f.Name);
        }
        missing.Remove("playerRig");
        report.Add("StoryAssets listo" + (missing.Count > 0 ? " · falta: " + string.Join(", ", missing) : " (completo)."));
        return a;
    }

    static void BuildPlayerRig(StoryAssets assets, List<string> report)
    {
        const string source = "Assets/00_Scenes/Nivel4.unity";
        Scene s = SceneManager.GetSceneByPath(source);
        bool opened = false;
        if (!s.isLoaded)
        {
            s = EditorSceneManager.OpenScene(source, OpenSceneMode.Additive);
            opened = true;
        }
        try
        {
            Transform[] all = s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            GameObject player = all.Select(t => t.GetComponent<GuardPlayerMovement>())
                .Where(m => m && m.name == "Player").Select(m => m.gameObject).FirstOrDefault();
            Camera cam = all.Select(t => t.GetComponent<Camera>()).FirstOrDefault(c => c && c.GetComponent<CameraIntroFollow>());
            if (!player || !cam) { report.Add("NO se encontró Player/cámara en Nivel4: no se creó el jugador."); return; }

            GameObject root = new GameObject("JugadorHistoria");
            SceneManager.MoveGameObjectToScene(root, s);
            GameObject p = Object.Instantiate(player, root.transform);
            p.name = "Player";
            p.transform.localPosition = Vector3.zero;
            p.transform.localRotation = Quaternion.identity;
            p.transform.localScale = player.transform.lossyScale;
            GameObject c = Object.Instantiate(cam.gameObject, root.transform);
            c.name = "Main Camera";
            c.tag = "MainCamera";
            c.transform.localPosition = new Vector3(0f, 1.8f, -3.2f);
            c.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            c.GetComponent<Camera>().enabled = true;

            var follow = new SerializedObject(c.GetComponent<CameraIntroFollow>());
            follow.FindProperty("player").objectReferenceValue = p.transform;
            SerializedProperty route = follow.FindProperty("routePoints");
            if (route != null) route.arraySize = 0;
            follow.ApplyModifiedPropertiesWithoutUndo();
            var move = new SerializedObject(p.GetComponent<GuardPlayerMovement>());
            SerializedProperty camProp = move.FindProperty("cameraTransform");
            if (camProp != null) camProp.objectReferenceValue = c.transform;
            move.ApplyModifiedPropertiesWithoutUndo();
            GuardHealth h = p.GetComponent<GuardHealth>();
            if (h) h.respawnPoint = null;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RigPath);
            Object.DestroyImmediate(root);
            assets.playerRig = prefab;
            EditorUtility.SetDirty(assets);
            report.Add("Jugador de la historia: " + RigPath);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(s, true);
        }
    }
}
