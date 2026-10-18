using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class SupportProfile
{
    [MenuItem("Juego/Diagnóstico/Perfil Support")]
    public static void Run()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        var o = new List<string>();
        foreach (string n in new[] { "Support_Right", "Wall_ArchRound", "Doors_RoundArch_L" })
        {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/04_Prefabs/" + n + ".prefab");
        var g = (GameObject)PrefabUtility.InstantiatePrefab(p);
        g.transform.position = Vector3.zero;
        o.Add("== " + n);
        // Rayos horizontales a lo largo de X a distintas alturas, por delante (z) para hallar los tramos sólidos.
        var mcs = new List<MeshCollider>();
        foreach (var mf in g.GetComponentsInChildren<MeshFilter>()) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mcs.Add(mc); }
        Physics.SyncTransforms();
        for (float y = 0.1f; y < 4.3f; y += 0.2f)
        {
            string line = "y=" + y.ToString("0.0") + ": ";
            bool prev = false; float start = 0;
            for (float x = -2.2f; x <= 2.3f; x += 0.02f)
            {
                bool hit = Physics.Raycast(new Vector3(x, y, -3f), Vector3.forward, 6f) || Physics.Raycast(new Vector3(x, y, 3f), Vector3.back, 6f);
                if (hit && !prev) start = x;
                if (!hit && prev) line += "[" + start.ToString("0.00") + "," + x.ToString("0.00") + "] ";
                prev = hit;
            }
            if (prev) line += "[" + start.ToString("0.00") + ",end]";
            o.Add(line);
        }
        Object.DestroyImmediate(g);
        }
        System.IO.File.WriteAllLines("informe_perfil.txt", o);
        Debug.Log("perfil ok");
    }
}
