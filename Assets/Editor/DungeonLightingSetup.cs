using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Menú  Juego > Iluminar mazmorra (antorchas)
///
/// Para Tutorial, Nivel1 y Nivel2:
///  - Ambiente de calabozo: sin cielo, luz ambiente fría y muy baja, niebla
///    oscura que se traga el fondo de los pasillos.
///  - Cada antorcha ("Torch...") recibe:
///      LuzAntorcha  : luz puntual cálida con sombras suaves que parpadea (TorchFlicker)
///      FuegoAntorcha: llamas animadas (partículas) + chispas que suben
///  - El URP admite 8 luces por objeto (las paredes ven varias antorchas).
/// Se puede ejecutar varias veces: rehace lo anterior.
/// </summary>
public static class DungeonLightingSetup
{
    static readonly string[] Scenes =
    {
        "Assets/00_Scenes/Tutorial.unity",
        "Assets/00_Scenes/Nivel1.unity",
        "Assets/00_Scenes/Nivel2.unity",
    };

    const string Folder = "Assets/04_Prefabs/Antorcha";
    const string LightName = "LuzAntorcha";
    const string FireName = "FuegoAntorcha";

    static readonly Color Ambient = new Color(0.06f, 0.07f, 0.11f);
    static readonly Color FogColor = new Color(0.015f, 0.014f, 0.02f);

    static Material fireMat, emberMat;
    static readonly List<string> report = new List<string>();

    [MenuItem("Juego/Iluminar mazmorra (antorchas)")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Apply();
        EditorSceneManager.OpenScene(Scenes[1], OpenSceneMode.Single);
    }

    /// Sin preguntas (la llaman otros constructores al terminar).
    public static void Apply()
    {
        report.Clear();
        PrepareMaterials();
        RaiseLightLimit();

        foreach (string path in Scenes)
        {
            if (!System.IO.File.Exists(path)) continue;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int torches = LightScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.Add(System.IO.Path.GetFileNameWithoutExtension(path) + ": " + torches + " antorchas encendidas");
        }

        System.IO.File.WriteAllLines("informe_luces.txt", report);
        Debug.Log("Mazmorra iluminada:\n" + string.Join("\n", report));
    }

    // ------------------------------------------------------------ escena

    static int LightScene(Scene scene)
    {
        // Ambiente de calabozo.
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Ambient;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.reflectionIntensity = 0.25f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.055f;
        RenderSettings.fogColor = FogColor;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }
            // Sin sol en un calabozo.
            foreach (Light l in root.GetComponentsInChildren<Light>(true))
                if (l.type == LightType.Directional) l.enabled = false;
        }

        // Antorchas.
        List<Transform> torches = new List<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Torch") && t.GetComponent<MeshFilter>() != null)
                    torches.Add(t);

        int index = 0;
        foreach (Transform torch in torches)
        {
            foreach (Transform old in torch.Cast<Transform>().ToArray())
                if (old.name == LightName || old.name == FireName)
                    Object.DestroyImmediate(old.gameObject);

            Vector3 flame = FlamePoint(torch, out float flameSize);
            Vector3 away = AwayFromWall(flame);

            // Llamas (pegadas a la malla de la llama).
            GameObject fire = new GameObject(FireName);
            fire.transform.SetParent(torch, true);
            fire.transform.SetPositionAndRotation(flame - Vector3.up * flameSize * 0.2f, Quaternion.identity);
            fire.transform.localScale = InverseScale(torch);
            BuildFire(fire, flameSize);

            // Luz: un poco separada de la pared para que ilumine el pasillo.
            GameObject lightGo = new GameObject(LightName);
            lightGo.transform.SetParent(torch, true);
            lightGo.transform.position = flame + away * 0.3f + Vector3.up * 0.05f;
            lightGo.transform.localScale = InverseScale(torch);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.28f);
            light.intensity = 2.6f;
            light.range = 6.2f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.4f;
            light.renderMode = LightRenderMode.ForcePixel;
            light.lightmapBakeType = LightmapBakeType.Realtime;

            TorchFlicker flicker = lightGo.AddComponent<TorchFlicker>();
            flicker.baseIntensity = 2.6f;
            flicker.baseRange = 6.2f;
            flicker.speed = 3f + (index % 5) * 0.3f;
            flicker.flicker = 0.14f;
            flicker.jitter = 0f;

            index++;
        }
        LightTraps(scene);
        return torches.Count;
    }

    /// Luz rojiza dentro de cada foso de púas (se ve al hundirse el suelo y al
    /// caer) y una luz tenue sobre cada cepo para distinguirlo en el suelo.
    static void LightTraps(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                bool pit = t.name.StartsWith("Spikes_Platform");
                bool bear = t.name.StartsWith("BearTrap");
                if (!pit && !bear) continue;

                foreach (Transform old in t.Cast<Transform>().ToArray())
                    if (old.name == "LuzTrampa") Object.DestroyImmediate(old.gameObject);

                Renderer[] rs = t.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds;
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);

                GameObject go = new GameObject("LuzTrampa");
                go.transform.SetParent(t, true);
                go.transform.position = new Vector3(b.center.x, b.max.y + (pit ? 0.75f : 0.7f), b.center.z);
                go.transform.localScale = InverseScale(t);

                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.renderMode = LightRenderMode.ForcePixel;
                light.lightmapBakeType = LightmapBakeType.Realtime;
                if (pit)
                {
                    // Brasa roja en el fondo del foso: se ven las púas.
                    light.color = new Color(1f, 0.32f, 0.14f);
                    light.intensity = 3.2f;
                    light.range = 3.8f;
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = 0.9f;
                    TorchFlicker f = go.AddComponent<TorchFlicker>();
                    f.baseIntensity = 3.2f;
                    f.baseRange = 3.8f;
                    f.flicker = 0.12f;
                    f.speed = 2.2f;
                    f.jitter = 0f;
                    f.hotColor = new Color(1f, 0.42f, 0.18f);
                    f.coolColor = new Color(0.9f, 0.22f, 0.1f);
                }
                else
                {
                    light.color = new Color(1f, 0.55f, 0.3f);
                    light.intensity = 1.1f;
                    light.range = 1.8f;
                    light.shadows = LightShadows.None;
                }
            }
    }

    static Vector3 InverseScale(Transform t)
    {
        Vector3 s = t.lossyScale;
        return new Vector3(1f / Mathf.Max(0.0001f, s.x), 1f / Mathf.Max(0.0001f, s.y), 1f / Mathf.Max(0.0001f, s.z));
    }

    /// Centro de la llama: media de los vértices más altos de la malla.
    static Vector3 FlamePoint(Transform torch, out float size)
    {
        MeshFilter mf = torch.GetComponent<MeshFilter>();
        Renderer r = torch.GetComponent<Renderer>();
        Bounds b = r.bounds;
        size = Mathf.Clamp(b.size.y * 0.25f, 0.08f, 0.4f);

        Vector3[] verts = mf.sharedMesh.vertices;
        float topY = b.max.y - b.size.y * 0.18f;
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (Vector3 v in verts)
        {
            Vector3 w = torch.TransformPoint(v);
            if (w.y >= topY) { sum += w; n++; }
        }
        return n > 0 ? sum / n : new Vector3(b.center.x, b.max.y - size * 0.5f, b.center.z);
    }

    /// Dirección horizontal que se aleja de la pared más cercana.
    static Vector3 AwayFromWall(Vector3 p)
    {
        Vector3 best = Vector3.zero; float bestDist = 1.2f;
        foreach (Vector3 d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            if (Physics.Raycast(p, d, out RaycastHit hit, bestDist, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                bestDist = hit.distance;
                best = -d;
            }
        return best;
    }

    // ------------------------------------------------------------ fuego

    static void BuildFire(GameObject go, float size)
    {
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.45f, size * 0.7f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.8f, 0.35f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 40;
        main.gravityModifier = -0.05f;

        var emission = ps.emission;
        emission.rateOverTime = 34f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6f;
        shape.radius = size * 0.25f;
        shape.rotation = new Vector3(-90f, 0f, 0f);   // hacia arriba

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.45f),
                    new GradientColorKey(new Color(0.6f, 0.12f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.15f),
                    new GradientAlphaKey(0.3f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.15f)));

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.05f;
        noise.frequency = 1.2f;
        noise.scrollSpeed = 0.6f;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = fireMat;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.sortingFudge = 2f;
        rend.maxParticleSize = 0.08f;

        // Chispas que suben de vez en cuando.
        GameObject sparks = new GameObject("Chispas");
        sparks.transform.SetParent(go.transform, false);
        ParticleSystem sp = sparks.AddComponent<ParticleSystem>();
        sp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var sm = sp.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
        sm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f), new Color(1f, 0.85f, 0.4f));
        sm.simulationSpace = ParticleSystemSimulationSpace.World;
        sm.scalingMode = ParticleSystemScalingMode.Hierarchy;
        sm.maxParticles = 12;
        var se = sp.emission;
        se.rateOverTime = 1.2f;
        var ss = sp.shape;
        ss.shapeType = ParticleSystemShapeType.Cone;
        ss.angle = 20f;
        ss.radius = size * 0.2f;
        ss.rotation = new Vector3(-90f, 0f, 0f);
        var sn = sp.noise;
        sn.enabled = true;
        sn.strength = 0.5f;
        sn.frequency = 2f;
        var sc = sp.colorOverLifetime;
        sc.enabled = true;
        Gradient sg = new Gradient();
        sg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        sc.color = sg;
        var sr = sparks.GetComponent<ParticleSystemRenderer>();
        sr.sharedMaterial = emberMat;
        sr.shadowCastingMode = ShadowCastingMode.Off;
        sr.receiveShadows = false;
    }

    // ------------------------------------------------------------ materiales

    static void PrepareMaterials()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/04_Prefabs", "Antorcha");

        Texture2D soft = SoftTexture("LlamaSuave", 64, 1.6f);
        Texture2D dot = SoftTexture("Chispa", 16, 3f);
        fireMat = AdditiveMaterial("FuegoAntorcha", soft);
        emberMat = AdditiveMaterial("ChispaAntorcha", dot);
    }

    static Texture2D SoftTexture(string name, int size, float power)
    {
        string path = Folder + "/" + name + ".png";
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                // Llama: algo más alta que ancha.
                float d = Mathf.Sqrt(dx * dx + dy * dy * 0.7f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), power);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.mipmapEnabled = false;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material AdditiveMaterial(string name, Texture2D tex)
    {
        string path = Folder + "/" + name + ".mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);

        // Transparente aditivo (brilla sobre lo que hay detrás).
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ------------------------------------------------------------ URP

    static void RaiseLightLimit()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            SerializedObject so = new SerializedObject(asset);
            SerializedProperty limit = so.FindProperty("m_AdditionalLightsPerObjectLimit");
            if (limit != null) limit.intValue = 8;
            SerializedProperty count = so.FindProperty("m_MaxAdditionalLightsCount");
            if (count != null) count.intValue = Mathf.Max(count.intValue, 16);
            // Sombras de todas las antorchas sin perder resolución.
            SerializedProperty atlas = so.FindProperty("m_AdditionalLightsShadowmapResolution");
            if (atlas != null) atlas.intValue = 4096;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
    }
}
