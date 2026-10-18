using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú  Juego > Diagnóstico > Crear mano monstruosa
/// Genera Assets/04_Prefabs/MonsterHand.prefab: mano huesuda con antebrazo,
/// nudillos marcados, dedos largos articulados (Dedo{n}_{falange}) y garras
/// negras curvas, con piel gris pálida agrietada (textura generada).
/// La raíz es la muñeca; los dedos apuntan a +Z y la palma mira hacia -Y.
/// </summary>
public static class MonsterHandBuilder
{
    const string Folder = "Assets/04_Prefabs/MonsterHand";
    const string PrefabPath = "Assets/04_Prefabs/MonsterHand.prefab";

    static Material skin, claw;
    static Mesh clawMesh;

    [MenuItem("Juego/Diagnóstico/Crear mano monstruosa")]
    public static GameObject Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/04_Prefabs", "MonsterHand");

        skin = MakeMaterial("ManoPiel", new Color(0.80f, 0.77f, 0.72f), 0.22f, SkinTexture());
        claw = MakeMaterial("ManoGarra", new Color(0.06f, 0.05f, 0.05f), 0.78f, null);
        clawMesh = ClawMesh();

        GameObject root = new GameObject("ManoMonstruo");

        // Antebrazo: largo, delgado y con tendones (sale de la oscuridad).
        Part(root.transform, "Antebrazo", PrimitiveType.Capsule, new Vector3(0f, -0.01f, -0.85f),
            new Vector3(90f, 0f, 0f), new Vector3(0.2f, 0.9f, 0.17f), skin);
        Part(root.transform, "Muneca", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.02f),
            Vector3.zero, new Vector3(0.24f, 0.15f, 0.2f), skin);
        for (int i = -1; i <= 1; i++)
            Part(root.transform, "TendonBrazo", PrimitiveType.Capsule,
                new Vector3(i * 0.05f, 0.065f, -0.55f), new Vector3(90f, i * 2f, 0f),
                new Vector3(0.035f, 0.55f, 0.035f), skin);

        // Palma (aquí se engancha lo que agarra).
        Transform palm = new GameObject("Palma").transform;
        palm.SetParent(root.transform, false);
        Part(palm, "PalmaMalla", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.15f),
            Vector3.zero, new Vector3(0.36f, 0.11f, 0.34f), skin);
        for (int i = 0; i < 4; i++)
        {
            float x = Mathf.Lerp(-0.12f, 0.12f, i / 3f);
            Part(palm, "TendonMano", PrimitiveType.Capsule, new Vector3(x * 0.8f, 0.045f, 0.15f),
                new Vector3(90f, x * 40f, 0f), new Vector3(0.03f, 0.14f, 0.03f), skin);
        }

        // Dedos: 1 = índice ... 4 = meñique (largos y torcidos).
        float[] lengths = { 1.0f, 1.12f, 1.05f, 0.85f };
        for (int f = 0; f < 4; f++)
        {
            float x = Mathf.Lerp(-0.13f, 0.13f, f / 3f);
            float s = lengths[f];
            Vector3 basePos = new Vector3(x, 0f, 0.29f - Mathf.Abs(x) * 0.25f);
            Quaternion spread = Quaternion.Euler(4f, x * 55f, 0f);
            Finger(palm, f + 1, basePos, spread,
                new[] { 0.21f * s, 0.17f * s, 0.13f * s }, 0.036f);
        }

        // Pulgar, hacia un lado y hacia abajo.
        Finger(palm, 0, new Vector3(-0.17f, -0.02f, 0.09f), Quaternion.Euler(18f, -52f, -25f),
            new[] { 0.16f, 0.13f }, 0.04f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("Mano creada en " + PrefabPath);
        return prefab;
    }

    static void Finger(Transform palm, int index, Vector3 basePos, Quaternion rot,
        float[] segments, float radius)
    {
        Transform parent = palm;
        Vector3 pos = basePos;
        Quaternion localRot = rot;

        for (int j = 0; j < segments.Length; j++)
        {
            float r = radius * (1f - j * 0.17f);
            Transform joint = new GameObject("Dedo" + index + "_" + (j + 1)).transform;
            joint.SetParent(parent, false);
            joint.localPosition = pos;
            // Pequeña torsión natural en cada falange.
            joint.localRotation = localRot * Quaternion.Euler(6f, 0f, (index % 2 == 0 ? 3f : -3f));

            // Nudillo marcado + hueso.
            Part(joint, "Nudillo", PrimitiveType.Sphere, Vector3.zero, Vector3.zero,
                Vector3.one * r * 2.7f, skin);
            Part(joint, "Falange", PrimitiveType.Capsule, new Vector3(0f, 0f, segments[j] * 0.5f),
                new Vector3(90f, 0f, 0f), new Vector3(r * 2f, segments[j] * 0.5f + r * 0.6f, r * 1.8f), skin);

            parent = joint;
            pos = new Vector3(0f, 0f, segments[j]);
            localRot = Quaternion.identity;

            if (j == segments.Length - 1)
            {
                GameObject c = new GameObject("Garra");
                c.transform.SetParent(joint, false);
                c.transform.localPosition = new Vector3(0f, 0f, segments[j] + r * 0.4f);
                c.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
                c.transform.localScale = Vector3.one * (r / 0.036f);
                c.AddComponent<MeshFilter>().sharedMesh = clawMesh;
                c.AddComponent<MeshRenderer>().sharedMaterial = claw;
            }
        }
    }

    static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos,
        Vector3 euler, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    // Garra: cono curvo hacia abajo, a lo largo de +Z.
    static Mesh ClawMesh()
    {
        const int rings = 9, sides = 10;
        const float length = 0.13f, radius = 0.03f, bend = 0.06f;
        List<Vector3> v = new List<Vector3>();
        List<int> tri = new List<int>();

        for (int i = 0; i < rings; i++)
        {
            float s = i / (float)(rings - 1);
            float r = radius * (1f - s) * (1f - s * 0.15f);
            Vector3 c = new Vector3(0f, -bend * s * s, length * s);
            for (int k = 0; k < sides; k++)
            {
                float a = k / (float)sides * Mathf.PI * 2f;
                v.Add(c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.8f, 0f));
            }
        }
        for (int i = 0; i < rings - 1; i++)
            for (int k = 0; k < sides; k++)
            {
                int a = i * sides + k, b = i * sides + (k + 1) % sides;
                int c2 = a + sides, d = b + sides;
                tri.AddRange(new[] { a, c2, b, b, c2, d });
            }

        Mesh m = new Mesh { name = "GarraCurva" };
        m.SetVertices(v);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();

        string path = Folder + "/GarraCurva.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // Piel pálida con grietas oscuras (como en las imágenes de referencia).
    static Texture2D SkinTexture()
    {
        const int size = 512;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        Color baseCol = new Color(0.84f, 0.81f, 0.76f);
        Color[] px = new Color[size * size];

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.012f, y * 0.012f) * 0.6f +
                          Mathf.PerlinNoise(x * 0.05f + 50f, y * 0.05f) * 0.4f;
                px[y * size + x] = baseCol * (0.86f + n * 0.2f);
            }

        // Grietas: caminos aleatorios oscuros que se ramifican.
        Random.InitState(7);
        for (int c = 0; c < 70; c++)
        {
            Vector2 p = new Vector2(Random.Range(0, size), Random.Range(0, size));
            float ang = Random.Range(0f, Mathf.PI * 2f);
            int steps = Random.Range(25, 90);
            for (int s = 0; s < steps; s++)
            {
                ang += Random.Range(-0.5f, 0.5f);
                p += new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 1.6f;
                int ix = ((int)p.x % size + size) % size, iy = ((int)p.y % size + size) % size;
                px[iy * size + ix] *= 0.55f;
                px[iy * size + (ix + 1) % size] *= 0.8f;
            }
        }

        tex.SetPixels(px);
        tex.Apply();

        string path = Folder + "/ManoPiel.png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material MakeMaterial(string name, Color color, float smoothness, Texture2D map)
    {
        string path = Folder + "/" + name + ".mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetColor("_BaseColor", color);
        if (map != null) mat.SetTexture("_BaseMap", map);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
