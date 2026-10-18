using UnityEditor;
using UnityEngine;

/// Herramienta de diagnóstico: mide prefabs del pack de ruinas.
public static class PrefabMeasure
{
    static readonly string[] Names =
    {
        "Wall_ArchRound", "Wall_ArchRound_Overgrown",
        "Doors_RoundArch_L", "Doors_RoundArch_R",
        "Doors_GothicArch_L", "Doors_GothicArch_R", "Wall_ArchGothic",
    };

    static System.Text.StringBuilder log = new System.Text.StringBuilder();
    static void Log(string m) { Debug.Log(m); log.AppendLine(m); }

    [MenuItem("Juego/Diagnóstico/Medir arcos y puertas")]
    public static void Measure()
    {
        log.Clear();
        foreach (string n in Names)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/04_Prefabs/" + n + ".prefab");
            if (prefab == null) { Log("MEDIDA " + n + ": no existe"); continue; }

            MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
            Mesh mesh = mf ? mf.sharedMesh : null;
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = new Vector3(0, -100, 0);
            Renderer r = go.GetComponentInChildren<Renderer>();
            Bounds wb = r.bounds;

            string extra = "";
            if (mesh != null)
            {
                // Opening test: vertices in the lower-central region (local mesh space).
                Bounds lb = mesh.bounds;
                extra = " meshLocal=" + lb.size.ToString("F3") + " meshCenter=" + lb.center.ToString("F3") +
                        " verts=" + mesh.vertexCount;
            }

            Log("MEDIDA " + n + ": rot=" + prefab.transform.rotation.eulerAngles.ToString("F0") +
                      " scale=" + prefab.transform.localScale.ToString("F2") +
                      " mundo=" + wb.size.ToString("F3") + extra);

            // Corte horizontal: ancho del hueco a varias alturas (para arcos).
            if (n.StartsWith("Wall_Arch"))
            {
                MeshCollider col = go.AddComponent<MeshCollider>();
                Physics.SyncTransforms();
                for (float t = 0.1f; t < 1f; t += 0.15f)
                {
                    float y = wb.min.y + wb.size.y * t;
                    // Barre de un lado al otro por el eje más ancho (horizontal).
                    bool alongX = wb.size.x >= wb.size.z;
                    int hits = 0; float openStart = float.NaN, openEnd = float.NaN;
                    float a0 = alongX ? wb.min.x : wb.min.z, a1 = alongX ? wb.max.x : wb.max.z;
                    bool prevSolid = true;
                    for (float a = a0; a <= a1; a += (a1 - a0) / 80f)
                    {
                        Vector3 from = alongX ? new Vector3(a, y, wb.center.z - 5f) : new Vector3(wb.center.x - 5f, y, a);
                        Vector3 dir = alongX ? Vector3.forward : Vector3.right;
                        bool solid = Physics.Raycast(from, dir, 10f);
                        if (!solid && prevSolid && float.IsNaN(openStart)) openStart = a;
                        if (solid && !prevSolid && !float.IsNaN(openStart)) openEnd = a;
                        if (solid) hits++;
                        prevSolid = solid;
                    }
                    Log("MEDIDA " + n + " corte t=" + t.ToString("F2") + " hueco=" +
                              (openEnd - openStart).ToString("F3") + " (" + (openStart - a0).ToString("F2") +
                              ".." + (openEnd - a0).ToString("F2") + " de " + (a1 - a0).ToString("F2") + ")");
                }
            }

            Object.DestroyImmediate(go);
        }
        System.IO.File.WriteAllText("medidas_prefabs.txt", log.ToString());
    }
}
